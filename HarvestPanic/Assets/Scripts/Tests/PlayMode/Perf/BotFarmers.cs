using System.Collections.Generic;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Tests.PlayMode.Perf
{
    /// <summary>
    /// Scripted stand-in farmers for perf capture and soak tests. NGO runs one NetworkManager per process, so the
    /// extra farmers are unspawned copies of the real player prefab (remote-view body, same renderers/colliders)
    /// driven kinematically by the server: walk to the nearest free goods, carry it overhead across the bridge,
    /// drop it in the Sale Pen, shout now and then. Goods are server-owned NetworkObjects, so sales go through
    /// the real MarketPen / EconomyDirector path.
    /// </summary>
    public sealed class BotFarmer
    {
        public enum Step { Seek, Carry, Idle }
        public readonly GameObject Body;
        public readonly int Index;
        public Step State = Step.Seek;
        public Goods Target;
        public int Delivered, Shouts;
        float _nextShout, _idleUntil;
        readonly System.Random _rng;
        public float Speed = 7f;
        static readonly RaycastHit[] Hits = new RaycastHit[8];

        public static readonly HashSet<Goods> Claimed = new HashSet<Goods>();

        public BotFarmer(GameObject prefab, Vector3 pos, int index, int seed)
        {
            Index = index;
            _rng = new System.Random(seed);
            Body = Object.Instantiate(prefab, pos, Quaternion.identity);
            Body.name = "BotFarmer_" + index;
            var pc = Body.GetComponent<PlayerCharacter>();
            if (pc != null) pc.ConfigureOffline(false);
            // input/motor/camera are owner-only; keep them off on the dummy
            var input = Body.GetComponent<PlayerInputReader>(); if (input != null) input.enabled = false;
            var motor = Body.GetComponent<PlayerMotor>(); if (motor != null) motor.enabled = false;
            foreach (var cam in Body.GetComponentsInChildren<Camera>(true)) cam.gameObject.SetActive(false);
            foreach (var al in Body.GetComponentsInChildren<AudioListener>(true)) al.enabled = false;
            var cc = Body.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
            _nextShout = Time.time + (float)_rng.NextDouble() * 4f;
        }

        public Vector3 Position => Body.transform.position;

        /// <summary>One bot tick. <paramref name="penCentre"/> is the Sale Pen drop point.</summary>
        public void Tick(float dt, Vector3 penCentre, bool marketOpen)
        {
            if (Body == null) return;
            if (Time.time >= _nextShout)
            {
                _nextShout = Time.time + 3f + (float)_rng.NextDouble() * 6f;
                if (NoiseService.TryEmit(NoiseSource.Shout, 1f, Position + Vector3.up * 1.6f, PlayerIds.None, PlayerIds.None)) Shouts++;
            }
            switch (State)
            {
                case Step.Idle:
                    if (Time.time >= _idleUntil) State = Step.Seek;
                    break;
                case Step.Seek:
                    if (Target == null || !Target.IsSpawned || Target.IsHeld) { Release(); Target = FindNearest(); }
                    if (Target == null) { Wander(dt); break; }
                    if (MoveTowards(Target.transform.position, dt) < 1.4f) State = Step.Carry;
                    break;
                case Step.Carry:
                    if (Target == null || !Target.IsSpawned) { Release(); State = Step.Seek; break; }
                    float d = MoveTowards(penCentre, dt);
                    Hold(Target, Position + Vector3.up * 2.2f);
                    if (d < 1.2f && marketOpen)
                    {
                        Drop(Target, penCentre + Vector3.up * 0.6f);
                        Delivered++;
                        Release();
                        State = Step.Idle; _idleUntil = Time.time + 0.5f;
                    }
                    break;
            }
        }

        void Release() { if (Target != null) Claimed.Remove(Target); Target = null; }

        Goods FindNearest()
        {
            Goods best = null; float bestD = float.MaxValue; var p = Position;
            foreach (var g in Goods.All)
            {
                if (g == null || !g.IsSpawned || g.IsHeld || Claimed.Contains(g)) continue;
                float d = (g.transform.position - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = g; }
            }
            if (best != null) Claimed.Add(best);
            return best;
        }

        void Wander(float dt)
        {
            var t = Body.transform;
            t.Rotate(0f, 45f * dt, 0f);
            MoveTowards(t.position + t.forward * 2f, dt);
        }

        /// <summary>Straight-line XZ move, Y snapped to ground below. Returns remaining flat distance.</summary>
        float MoveTowards(Vector3 dest, float dt)
        {
            var t = Body.transform;
            Vector3 p = t.position, to = dest - p; to.y = 0f;
            float dist = to.magnitude;
            if (dist > 0.01f)
            {
                Vector3 step = to / dist * Mathf.Min(dist, Speed * dt);
                p += step;
                t.rotation = Quaternion.LookRotation(to / dist, Vector3.up);
            }
            int n = Physics.RaycastNonAlloc(new Vector3(p.x, p.y + 20f, p.z), Vector3.down, Hits, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.collider.transform.IsChildOf(t)) continue;
                if (Target != null && h.collider.transform.IsChildOf(Target.transform)) continue;
                if (h.point.y > best && h.point.y < p.y + 2.5f) best = h.point.y;
            }
            if (best > float.MinValue) p.y = best + 1f; // capsule centre ~1 m above ground
            t.position = p;
            return dist;
        }

        static void Hold(Goods g, Vector3 pos)
        {
            var rb = g.GetComponent<Rigidbody>();
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.position = pos; }
            g.transform.position = pos;
        }

        static void Drop(Goods g, Vector3 pos)
        {
            Hold(g, pos);
            Physics.SyncTransforms();
        }

        public void Destroy() { Release(); if (Body != null) Object.Destroy(Body); }
    }
}
