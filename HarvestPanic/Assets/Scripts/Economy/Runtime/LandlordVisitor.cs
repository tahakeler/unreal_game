using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Landlord Sunday walk-in (week-quota Sunday, GDD §11.3.1). Local presentation on every peer, never networked:
    /// a primitive figure (plum coat, stovepipe hat 40% of his height, clipboard) walks to the farmhouse door
    /// (BarnRespawn marker), speaks GDD-toned lines in a world-space bubble and acts out PAID (hat tip) or
    /// EVICTED (hat pops off and tumbles). Timeline logic lives in <see cref="LandlordVisitScript"/>.
    /// </summary>
    public sealed class LandlordVisitor : MonoBehaviour
    {
        static readonly Color Plum = new Color32(0x5B, 0x2A, 0x6E, 0xFF);

        /// <summary>The visitor currently on the farm, if any.</summary>
        public static LandlordVisitor Current { get; private set; }

        public LandlordVisitScript Script { get; private set; }
        /// <summary>True until the verdict beat finished (HUD holds the Sunday receipt meanwhile).</summary>
        public bool IsPlaying => Script != null && Script.State != LandlordVisitState.Done;

        EconomyDirector _dir;
        Vector3 _door;
        Transform _hat, _bubble, _stamp;
        TextMesh _text, _stampText;
        Transform _bubbleBg;
        string _shownLine;
        float _beatT, _speed;
        Vector3 _hatRest;

        /// <summary>Spawns the figure near the farmhouse and starts walking.</summary>
        public static LandlordVisitor Spawn(EconomyDirector dir)
        {
            var t = dir.Tuning;
            Vector3 door = Vector3.zero; Quaternion face = Quaternion.identity;
            foreach (var m in LevelMarker.FindAll(MarkerRole.BarnRespawn)) { door = m.transform.position; break; }
            Vector3 from = door + new Vector3(t.VisitWalkDistance, 0f, 0f);
            var go = new GameObject("LandlordVisitor");
            go.transform.position = from;
            var v = go.AddComponent<LandlordVisitor>();
            v._dir = dir; v._door = door + new Vector3(1.5f, 0f, 0f); v._speed = Mathf.Max(0.1f, t.VisitWalkSpeed);
            v.Script = new LandlordVisitScript(t.VisitGreetSeconds, t.VisitCountSeconds, t.VisitVerdictSeconds);
            v.Build();
            return v;
        }

        void OnEnable() => Current = this;
        void OnDestroy() { if (Current == this) Current = null; }

        static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c)
        {
            var g = GameObject.CreatePrimitive(type);
            var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>(); r.material.color = c;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return g.transform;
        }

        void Build()
        {
            var root = transform;
            Prim(PrimitiveType.Capsule, root, new Vector3(0f, 0.95f, 0f), new Vector3(0.55f, 0.95f, 0.45f), Plum);              // coat
            Prim(PrimitiveType.Sphere, root, new Vector3(0f, 2.05f, 0f), Vector3.one * 0.42f, new Color(0.96f, 0.82f, 0.68f));    // head
            Prim(PrimitiveType.Cube, root, new Vector3(0f, 2.06f, 0.2f), new Vector3(0.28f, 0.04f, 0.04f), new Color(0.2f, 0.12f, 0.1f)); // moustache
            _hat = new GameObject("Hat").transform; _hat.SetParent(root, false); _hat.localPosition = new Vector3(0f, 2.24f, 0f);
            _hatRest = _hat.localPosition;
            Prim(PrimitiveType.Cylinder, _hat, new Vector3(0f, 0.02f, 0f), new Vector3(0.62f, 0.02f, 0.62f), new Color(0.08f, 0.06f, 0.08f)); // brim
            Prim(PrimitiveType.Cylinder, _hat, new Vector3(0f, 0.45f, 0f), new Vector3(0.38f, 0.45f, 0.38f), new Color(0.08f, 0.06f, 0.08f)); // stovepipe
            Prim(PrimitiveType.Cylinder, _hat, new Vector3(0f, 0.12f, 0f), new Vector3(0.39f, 0.05f, 0.39f), Plum);                     // band
            var clip = Prim(PrimitiveType.Cube, root, new Vector3(0.33f, 1.15f, 0.3f), new Vector3(0.32f, 0.42f, 0.03f), new Color(0.55f, 0.4f, 0.25f));
            clip.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            Prim(PrimitiveType.Cube, clip, new Vector3(0f, -0.05f, 0.6f), new Vector3(0.85f, 0.8f, 0.5f), new Color(0.97f, 0.95f, 0.88f)); // paper

            _bubble = new GameObject("Bubble").transform; _bubble.SetParent(root, false); _bubble.localPosition = new Vector3(0f, 3.55f, 0f);
            var bg = Prim(PrimitiveType.Cube, _bubble, new Vector3(0f, 0f, 0.03f), new Vector3(3.6f, 0.8f, 0.02f), new Color(0.97f, 0.92f, 0.83f));
            var bgr = bg.GetComponent<Renderer>();
            bgr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null) { bgr.material = new Material(unlit); bgr.material.SetColor("_BaseColor", new Color(0.99f, 0.95f, 0.86f)); }
            _bubbleBg = bg;
            _text = MakeText(_bubble, new Color(0.17f, 0.13f, 0.09f), 0.03f);
            // Polish-2: was 4.4 m above his head, where it sat under the HUD wallet card; now beside him below the bubble, tilted like a rubber stamp.
            _stamp = new GameObject("Stamp").transform; _stamp.SetParent(root, false); _stamp.localPosition = new Vector3(2.9f, 1.7f, -0.1f); _stamp.localRotation = Quaternion.Euler(0f, 0f, 14f);
            _stampText = MakeText(_stamp, Color.white, 0.09f);
            _stamp.gameObject.SetActive(false);
            _bubble.gameObject.SetActive(false);
        }

        static TextMesh MakeText(Transform parent, Color c, float size)
        {
            var g = new GameObject("Text");
            g.transform.SetParent(parent, false);
            var tm = g.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            g.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.fontSize = 64; tm.characterSize = size; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = c;
            return tm;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Script.State == LandlordVisitState.Walking)
            {
                Vector3 to = _door - transform.position; to.y = 0f;
                if (to.magnitude < 0.05f) Script.Arrived();
                else
                {
                    transform.position += Vector3.ClampMagnitude(to.normalized * _speed * dt, to.magnitude);
                    transform.rotation = Quaternion.LookRotation(to.normalized);
                    transform.position = new Vector3(transform.position.x, _door.y + Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.06f, transform.position.z); // jaunty bob
                }
            }
            if (_dir != null) Script.SetVerdict(VerdictFrom(_dir));
            Script.Tick(dt);
            Present(dt);
        }

        /// <summary>Maps the authoritative phase + Sunday receipt to a verdict.</summary>
        public static LandlordVerdict VerdictFrom(EconomyDirector d)
        {
            var r = d.LastReceipt;
            if (d.Phase == DayPhase.Evicted) return LandlordVerdict.Evicted;
            if (d.Phase == DayPhase.Collateral) return LandlordVerdict.Collateral;
            if (r == null || !r.IsSunday || d.Phase != DayPhase.Sunday) return LandlordVerdict.None;
            if (!r.Paid) return LandlordVerdict.Evicted;
            return r.Carryover == 0 ? LandlordVerdict.PaidExact : LandlordVerdict.Paid;
        }

        void Present(float dt)
        {
            bool talking = Script.State != LandlordVisitState.Walking && !string.IsNullOrEmpty(Script.Line);
            _bubble.gameObject.SetActive(talking);
            if (talking && Script.Line != _shownLine)
            {
                _shownLine = Script.Line;
                _text.text = Wrap(Script.Line, 30);
                int lines = 1, longest = 0, cur = 0;
                foreach (char ch in _text.text) { if (ch == '\n') { lines++; cur = 0; } else longest = Mathf.Max(longest, ++cur); }
                _bubbleBg.localScale = new Vector3(longest * 0.135f + 0.5f, lines * 0.34f + 0.3f, 0.02f);
            }
            bool stamp = !string.IsNullOrEmpty(Script.Stamp) && (Script.State == LandlordVisitState.Verdict || Script.State == LandlordVisitState.Done);
            _stamp.gameObject.SetActive(stamp);
            if (stamp)
            {
                _stampText.text = Script.Stamp;
                _stampText.color = Script.Stamp == "PAID" ? new Color(0.23f, 0.6f, 0.23f) : new Color(0.9f, 0.28f, 0.23f);
            }
            // Comedy beat: PAID = hat tip; EVICTED = hat pops off and tumbles to the ground.
            if (Script.State == LandlordVisitState.Verdict || Script.State == LandlordVisitState.Done)
            {
                _beatT += dt;
                if (Script.Verdict == LandlordVerdict.Evicted)
                {
                    float t = Mathf.Min(_beatT, 1.2f);
                    _hat.localPosition = _hatRest + new Vector3(0.9f * t, 2.2f * t - 3.4f * t * t, 0f);
                    if (_hat.localPosition.y < 0.15f) _hat.localPosition = new Vector3(_hat.localPosition.x, 0.15f, _hat.localPosition.z);
                    _hat.localRotation = Quaternion.Euler(0f, 0f, -110f * Mathf.Min(1f, _beatT));
                }
                else if (Script.Verdict == LandlordVerdict.Paid || Script.Verdict == LandlordVerdict.PaidExact)
                    _hat.localPosition = _hatRest + Vector3.up * (0.25f * Mathf.Max(0f, Mathf.Sin(Mathf.Min(_beatT, 1f) * Mathf.PI)));
            }
            var cam = Camera.main;
            if (cam != null)
            {
                foreach (var b in new[] { _bubble, _stamp })
                    b.rotation = Quaternion.LookRotation(b.position - cam.transform.position, Vector3.up);
            }
        }

        static string Wrap(string s, int width)
        {
            var sb = new System.Text.StringBuilder();
            int col = 0;
            foreach (var word in s.Split(' '))
            {
                if (col > 0 && col + word.Length + 1 > width) { sb.Append('\n'); col = 0; }
                else if (col > 0) { sb.Append(' '); col++; }
                sb.Append(word); col += word.Length;
            }
            return sb.ToString();
        }
    }
}
