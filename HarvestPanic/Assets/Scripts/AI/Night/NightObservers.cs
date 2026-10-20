using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI.Night
{
    /// <summary>Extra view source (test bots, spectator cams). Return false to contribute no view this frame.</summary>
    public interface INightViewerSource { bool TryGetView(out NightViewer view); }

    /// <summary>
    /// Server-side list of views that can "observe" night creatures: every connected player's replicated head
    /// (child named "HeadPivot", pitch replicated by the Player team; fallback HeadAttachPoint) minus downed and
    /// AFK players (no look/move change for AfkSeconds), plus any registered <see cref="INightViewerSource"/>.
    /// </summary>
    public static class NightObservers
    {
        static readonly List<INightViewerSource> Extra = new List<INightViewerSource>();
        static readonly List<NightViewer> Views = new List<NightViewer>();
        static readonly Dictionary<ulong, (Vector3 pos, Quaternion rot, float since)> Idle = new Dictionary<ulong, (Vector3, Quaternion, float)>();
        static int _frame = -1;

        public static void Register(INightViewerSource s) { if (!Extra.Contains(s)) Extra.Add(s); }
        public static void Unregister(INightViewerSource s) => Extra.Remove(s);
        public static void ClearExtra() => Extra.Clear();

        /// <summary>Views this frame (cached per frame).</summary>
        public static IReadOnlyList<NightViewer> Current(NightTuning t)
        {
            if (_frame == Time.frameCount) return Views;
            _frame = Time.frameCount;
            Views.Clear();
            foreach (var s in Extra) if (s != null && s.TryGetView(out var v)) Views.Add(v);
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return Views;
            foreach (var c in nm.ConnectedClientsList)
            {
                var po = c.PlayerObject;
                if (po == null) continue;
                if (po.TryGetComponent(out IKnockable k) && k.IsDown) continue;
                var head = FindHead(po.transform);
                var view = new NightViewer { Eye = head.position, Rotation = head.rotation, VerticalFov = t.DefaultVerticalFov, Aspect = t.DefaultAspect };
                var cam = po.GetComponentInChildren<Camera>(true);
                if (cam != null) view.VerticalFov = cam.fieldOfView;
                if (IsAfk(c.ClientId, head, t.AfkSeconds)) continue;
                Views.Add(view);
            }
            return Views;
        }

        public static Transform FindHead(Transform root)
        {
            var t = FindDeep(root, "HeadPivot");
            if (t != null) return t;
            var hooks = root.GetComponent<IPlayerHooks>();
            return hooks != null && hooks.HeadAttachPoint != null ? hooks.HeadAttachPoint : root;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        static bool IsAfk(ulong id, Transform head, float afkSeconds)
        {
            float now = Time.time;
            if (!Idle.TryGetValue(id, out var s) || (s.pos - head.position).sqrMagnitude > 0.01f || Quaternion.Angle(s.rot, head.rotation) > 1f)
            { Idle[id] = (head.position, head.rotation, now); return false; }
            return now - s.since > afkSeconds;
        }
    }
}
