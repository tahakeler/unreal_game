using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.World
{
    /// <summary>
    /// Owns the bridge's planks: finds them, repairs them every morning (day index change) on the server.
    /// </summary>
    public sealed class BridgeController : MonoBehaviour
    {
        readonly List<BridgePlank> _planks = new List<BridgePlank>();
        int _lastDay = -1;

        /// <summary>Planks ordered west to east.</summary>
        public IReadOnlyList<BridgePlank> Planks { get { if (_planks.Count == 0) Collect(); return _planks; } }

        void Awake() => Collect();

        void Collect()
        {
            _planks.Clear();
            _planks.AddRange(GetComponentsInChildren<BridgePlank>(true));
            _planks.Sort((a, b) => a.Index.CompareTo(b.Index));
        }

        void Update()
        {
            if (!WorldAuthority.IsServerOrOffline || !Services.TryGet(out IDayModifierService day)) return;
            if (day.DayIndex != _lastDay)
            {
                if (_lastDay >= 0) RepairAll();
                _lastDay = day.DayIndex;
            }
        }

        /// <summary>Server: repairs every plank (free morning repair).</summary>
        public void RepairAll()
        {
            foreach (var p in Planks) p.Repair();
        }
    }
}
