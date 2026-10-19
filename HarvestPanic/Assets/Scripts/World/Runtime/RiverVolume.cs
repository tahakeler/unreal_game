using System.Collections;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.AI;

namespace HarvestPanic.World
{
    /// <summary>
    /// River trigger (Water layer, marker River). Server only:
    /// players: <see cref="IKnockable.SendToBarn"/> (Drowned, fee). Fallback for a player without IKnockable: respawn fee via <see cref="IEconomyService.ChargeRespawnFee"/>, then the "OnHarvestPanicDrowned(Vector3 barnRespawn)"
    /// message is sent so the Player team can respawn properly; if nothing moved the player out after 3 s the river teleports it.
    /// goods and critters: carried downstream at the surface (bobbing, kinematic) and dropped at the RiverPile marker after 15-25 s;
    /// goods take a Soaked hit (IGoods.ApplyBruise with DroppedInRiver).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RiverVolume : MonoBehaviour
    {
        /// <summary>Message sent upwards to a drowned player root (argument: barn respawn position).</summary>
        public const string DrownedMessage = "OnHarvestPanicDrowned";

        [SerializeField] float surfaceY = -4.2f;
        [SerializeField] Vector2 sweepSeconds = new Vector2(15f, 25f);
        [SerializeField, Tooltip("Impulse handed to IGoods.ApplyBruise as the Soaked hit (N*s)")] float soakImpulse = 600f;
        [SerializeField] float playerFallbackDelay = 3f;
        [SerializeField, Tooltip("Downstream path points (world); the pile marker is appended at runtime")] Vector3[] path = new Vector3[0];

        readonly HashSet<int> _busy = new HashSet<int>();

        /// <summary>Editor/build-time setup.</summary>
        public void Configure(float waterSurfaceY, Vector3[] downstreamPath) { surfaceY = waterSurfaceY; path = downstreamPath; }

        /// <summary>Number of bodies being swept right now.</summary>
        public int ActiveSweeps => _busy.Count;

        void OnTriggerEnter(Collider other)
        {
            if (!WorldAuthority.IsServerOrOffline) return;
            if (other.gameObject.layer == LayerMask.NameToLayer(GameLayers.Bridge)) return;
            if (Knockables.TryGetFor(other.gameObject, out var knock)) { HandleKnockable(knock, other); return; }
            var hooks = other.GetComponentInParent<IPlayerHooks>();
            if (hooks != null) { HandlePlayer(hooks, other); return; }

            var goods = other.GetComponentInParent<IGoods>();
            var carry = other.GetComponentInParent<ICarryable>();
            bool critter = other.gameObject.layer == LayerMask.NameToLayer(GameLayers.Critter);
            Transform root = goods?.GoodsTransform ?? carry?.CarryTransform ?? (critter || other.attachedRigidbody != null ? (other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root) : null);
            if (root == null || !_busy.Add(root.GetInstanceID())) return;
            Splash(root.position, goods?.LastHandlerId ?? PlayerIds.None);
            if (goods != null) goods.ApplyBruise(soakImpulse, goods.LastHandlerId, BlameVerb.DroppedInRiver);
            StartCoroutine(Sweep(root));
        }

        void HandleKnockable(IKnockable knock, Collider col)
        {
            var root = knock is Component c ? c.transform : col.transform.root;
            if (!_busy.Add(root.GetInstanceID())) return;
            var hooks = col.GetComponentInParent<IPlayerHooks>();
            Splash(root.position, hooks != null ? hooks.ClientId : PlayerIds.None);
            knock.SendToBarn(BlameVerb.Drowned, true); // Player team teleports and charges the fee
            StartCoroutine(Release(root, 1.5f));
        }

        IEnumerator Release(Transform root, float delay)
        {
            int id = root.GetInstanceID();
            yield return new WaitForSeconds(delay);
            _busy.Remove(id);
        }

        void HandlePlayer(IPlayerHooks hooks, Collider col)
        {
            var root = ((Component)hooks).transform;
            if (!_busy.Add(root.GetInstanceID())) return;
            Splash(root.position, hooks.ClientId);
            if (Services.TryGet(out IEconomyService eco)) eco.ChargeRespawnFee(hooks.ClientId, BlameVerb.Drowned);
            Vector3 barn = BarnRespawnPosition();
            root.gameObject.SendMessage(DrownedMessage, barn, SendMessageOptions.DontRequireReceiver);
            StartCoroutine(PlayerFallback(root, barn));
        }

        IEnumerator PlayerFallback(Transform root, Vector3 barn)
        {
            yield return new WaitForSeconds(playerFallbackDelay);
            if (root != null && root.position.y < surfaceY + 1.5f && Contains(root.position)) Teleport(root, barn);
            if (root != null) _busy.Remove(root.GetInstanceID());
        }

        IEnumerator Sweep(Transform body)
        {
            int id = body.GetInstanceID();
            var rb = body.GetComponent<Rigidbody>();
            var agent = body.GetComponent<NavMeshAgent>();
            bool wasKinematic = rb != null && rb.isKinematic;
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.isKinematic = true; }
            if (agent != null) agent.enabled = false;

            var pts = BuildPath(body.position);
            float duration = Random.Range(sweepSeconds.x, sweepSeconds.y);
            float total = 0f;
            for (int i = 1; i < pts.Count; i++) total += Vector3.Distance(pts[i - 1], pts[i]);
            float t = 0f;
            while (t < duration && body != null)
            {
                t += Time.deltaTime;
                Vector3 p = Along(pts, total, t / duration);
                if (t / duration < 0.92f) p.y = surfaceY + 0.15f + Mathf.Sin(t * 3f + id) * 0.12f;
                body.SetPositionAndRotation(p, body.rotation * Quaternion.Euler(0f, 40f * Time.deltaTime, 0f));
                yield return null;
            }
            if (body != null)
            {
                Vector3 pile = pts[pts.Count - 1] + new Vector3(Random.Range(-1.5f, 1.5f), 1f, Random.Range(-1.5f, 1.5f));
                if (rb != null) { rb.isKinematic = wasKinematic; }
                Teleport(body, pile);
                if (agent != null) { agent.enabled = true; agent.Warp(pile); }
            }
            _busy.Remove(id);
        }

        List<Vector3> BuildPath(Vector3 start)
        {
            var pts = new List<Vector3> { start };
            foreach (var p in path) if (p.z < start.z - 0.5f) pts.Add(p);
            var piles = LevelMarker.FindAll(MarkerRole.RiverPile);
            pts.Add(piles.Count > 0 ? piles[0].transform.position : (path.Length > 0 ? path[path.Length - 1] : start));
            return pts;
        }

        static Vector3 Along(List<Vector3> pts, float total, float u)
        {
            float d = Mathf.Clamp01(u) * total;
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = Vector3.Distance(pts[i - 1], pts[i]);
                if (d <= seg || i == pts.Count - 1) return Vector3.Lerp(pts[i - 1], pts[i], seg > 0f ? Mathf.Clamp01(d / seg) : 1f);
                d -= seg;
            }
            return pts[pts.Count - 1];
        }

        static Vector3 BarnRespawnPosition()
        {
            var barn = LevelMarker.FindAll(MarkerRole.BarnRespawn);
            return barn.Count > 0 ? barn[0].transform.position : Vector3.up;
        }

        bool Contains(Vector3 p) => GetComponent<BoxCollider>().bounds.Contains(p);

        static void Teleport(Transform root, Vector3 pos)
        {
            var cc = root.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            var rb = root.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.position = pos; }
            root.position = pos;
            if (cc != null) cc.enabled = true;
        }

        void Splash(Vector3 p, ulong blamed)
        {
            if (Services.TryGet(out INoiseService noise)) noise.Emit(NoiseSource.Hazard, 0.6f, p, 0, blamed, NoiseTag.Splash);
        }
    }
}
