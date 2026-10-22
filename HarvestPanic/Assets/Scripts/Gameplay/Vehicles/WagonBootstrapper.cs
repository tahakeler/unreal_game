using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Spawns the farm wagon and a hitching post on the server once a farm scene is loaded (MarketPen + BarnRespawn
    /// markers present), next to the barn yard (wagon-vehicles: "one wagon per farm"). Each new day a wagon that was
    /// tipped, swept down the river or abandoned far away is returned to the barn (wagon-vehicles section 5; the $40
    /// recovery fee is not charged yet). Set <see cref="Enabled"/> false to keep a scene wagon-free (perf tests).
    /// </summary>
    public sealed class WagonBootstrapper : MonoBehaviour
    {
        const string ListPath = "NetworkPrefabs/Player";
        /// <summary>Master switch (tests).</summary>
        public static bool Enabled = true;
        /// <summary>Wagon home in the BarnRespawn marker's frame (Muddlebury: 9.5 m east, 6.5 m north of it, facing east).</summary>
        public static Vector3 HomeOffset = new Vector3(-6.5f, 0f, 9.5f);
        /// <summary>Hitching post in the marker's frame.</summary>
        public static Vector3 PostOffset = new Vector3(-9.5f, 0f, 13f);
        /// <summary>Beyond this distance from home a wagon is brought back in the morning (m).</summary>
        public static float ReturnDistance = 25f;

        float _next;
        int _day = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Enabled = true;
            var go = new GameObject("[WagonBootstrapper]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<WagonBootstrapper>();
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            var nm = NetworkManager.Singleton;
            if (!Enabled || nm == null || !nm.IsListening || !nm.IsServer) { _day = -1; return; }
            var barns = LevelMarker.FindAll(MarkerRole.BarnRespawn);
            if (barns.Count == 0 || LevelMarker.FindAll(MarkerRole.MarketPen).Count == 0) return;
            var barn = barns[0].transform;
            Pose home = Home(barn, HomeOffset);

            if (WagonCart.All.Count == 0)
            {
                var wagon = FindPrefab<WagonCart>();
                if (wagon == null) { _next += 10f; return; }
                NetworkObject.InstantiateAndSpawn(wagon, nm, position: home.position, rotation: home.rotation);
                Debug.Log($"[Wagon] spawned at {home.position} (barn {barn.position})");
                var post = FindPrefab<HitchingPost>();
                if (post != null)
                {
                    Pose p = Home(barn, PostOffset);
                    NetworkObject.InstantiateAndSpawn(post, nm, position: p.position, rotation: p.rotation);
                }
                return;
            }

            // Morning return (day index changes).
            int day = Services.TryGet(out IDayModifierService mods) ? mods.DayIndex : 0;
            if (_day < 0) { _day = day; return; }
            if (day == _day) return;
            _day = day;
            foreach (var w in WagonCart.All)
            {
                if (w == null || !w.IsSpawned) continue;
                bool far = (w.transform.position - home.position).sqrMagnitude > ReturnDistance * ReturnDistance;
                if (far || w.IsTipped) w.ServerPlace(home.position, home.rotation);
            }
        }

        /// <summary>World pose for an offset in the barn marker's frame, dropped onto the ground.</summary>
        public static Pose Home(Transform barn, Vector3 offset)
        {
            Vector3 p = barn.TransformPoint(offset);
            Quaternion r = Quaternion.Euler(0f, barn.eulerAngles.y, 0f);
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 60f, Masks.Ground, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.05f;
            return new Pose(p, r);
        }

        /// <summary>The first prefab in the Player network prefab list carrying <typeparamref name="T"/>.</summary>
        public static GameObject FindPrefab<T>() where T : Component
        {
            var list = Resources.Load<NetworkPrefabsList>(ListPath);
            if (list == null) return null;
            foreach (var p in list.PrefabList) if (p.Prefab != null && p.Prefab.GetComponentInChildren<T>(true) != null) return p.Prefab;
            return null;
        }
    }
}
