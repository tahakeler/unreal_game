using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.AI.Night
{
    /// <summary>
    /// Client-local night presentation on the director: the sky itself darkens through WeatherPresenter (IDayClockService
    /// Dusk); this adds a warm hand-lantern glow on the local camera and lit lantern posts (Lantern Posts upgrade).
    /// </summary>
    public sealed class NightPresenter : MonoBehaviour
    {
        [SerializeField] GameObject lanternPostPrefab;
        NightDirector _dir;
        Light _hand;
        readonly List<GameObject> _posts = new List<GameObject>();
        int _shownPosts = -1;

        void Awake() { _dir = GetComponent<NightDirector>(); }

        void LateUpdate()
        {
            bool night = _dir != null && _dir.IsNight;
            var cam = Camera.main;
            if (night && cam != null)
            {
                if (_hand == null)
                {
                    var go = new GameObject("NightHandLantern");
                    _hand = go.AddComponent<Light>();
                    _hand.type = LightType.Point; _hand.range = 11f; _hand.intensity = 2.2f; _hand.color = new Color(1f, 0.78f, 0.45f); _hand.shadows = LightShadows.None;
                }
                _hand.transform.SetParent(cam.transform, false);
                _hand.transform.localPosition = new Vector3(0.35f, -0.35f, 0.4f);
                _hand.enabled = true;
            }
            else if (_hand != null) _hand.enabled = false;

            int want = night ? _dir.LanternPosts : 0;
            if (want == _shownPosts) return;
            _shownPosts = want;
            foreach (var p in _posts) if (p != null) Destroy(p);
            _posts.Clear();
            for (int i = 0; i < want && i < _dir.Tuning.LanternPostPositions.Length; i++) _posts.Add(MakePost(NightDirector.Ground(_dir.Tuning.LanternPostPositions[i])));
        }

        GameObject MakePost(Vector3 at)
        {
            var root = new GameObject("LanternPost");
            root.transform.position = at;
            NightVisuals.Part(PrimitiveType.Cylinder, root.transform, "Post", new Vector3(0, 1.2f, 0), new Vector3(0.14f, 1.2f, 0.14f), NightVisuals.Mat(new Color(0.3f, 0.2f, 0.12f)));
            if (lanternPostPrefab != null) { var l = Instantiate(lanternPostPrefab, root.transform); l.transform.localPosition = new Vector3(0, 2.4f, 0); }
            else NightVisuals.Part(PrimitiveType.Cube, root.transform, "Lamp", new Vector3(0, 2.55f, 0), Vector3.one * 0.3f, NightVisuals.Mat(new Color(1f, 0.85f, 0.4f)));
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(root.transform, false); light.transform.localPosition = new Vector3(0, 2.6f, 0);
            light.type = LightType.Point; light.range = _dir.Tuning.LanternRadius * 1.6f; light.intensity = 3f; light.color = new Color(1f, 0.75f, 0.4f);
            return root;
        }

        void OnDestroy() { foreach (var p in _posts) if (p != null) Destroy(p); if (_hand != null) Destroy(_hand.gameObject); }
    }
}
