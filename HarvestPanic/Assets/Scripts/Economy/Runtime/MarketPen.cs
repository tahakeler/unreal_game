using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Sale pen trigger (day-loop "Market zone"): an item is accepted after resting inside, not held, for
    /// Market.AcceptSeconds continuously. Added at runtime to every MarketPen marker by the director; uses the
    /// marker's own collider if World authored one, else a default box. Server/offline only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarketPen : MonoBehaviour
    {
        public static readonly Vector3 DefaultSize = new Vector3(6f, 3f, 6f);

        sealed class Tracked { public int Colliders; public float Rest; public bool InsideAtClose; }
        readonly Dictionary<IGoods, Tracked> _inside = new Dictionary<IGoods, Tracked>();
        readonly List<IGoods> _scratch = new List<IGoods>();

        /// <summary>Trigger collider.</summary>
        public Collider Trigger { get; private set; }

        /// <summary>Items currently tracked.</summary>
        public int Count => _inside.Count;

        void Awake()
        {
            Trigger = GetComponent<Collider>();
            if (Trigger == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = DefaultSize;
                box.center = new Vector3(0f, DefaultSize.y * 0.5f, 0f);
                Trigger = box;
            }
            Trigger.isTrigger = true;
            int layer = LayerMask.NameToLayer(GameLayers.Interactable);
            if (layer >= 0) gameObject.layer = layer;
        }

        void OnTriggerEnter(Collider other)
        {
            var g = other.GetComponentInParent<IGoods>();
            if (g == null) return;
            if (!_inside.TryGetValue(g, out var t)) _inside[g] = t = new Tracked();
            t.Colliders++;
        }

        void OnTriggerExit(Collider other)
        {
            var g = other.GetComponentInParent<IGoods>();
            if (g == null || !_inside.TryGetValue(g, out var t)) return;
            if (--t.Colliders <= 0) _inside.Remove(g);
        }

        /// <summary>Market just closed: items inside keep their grace window.</summary>
        public void MarkClose()
        {
            foreach (var t in _inside.Values) t.InsideAtClose = true;
        }

        void Update()
        {
            var dir = EconomyDirector.Instance;
            if (dir == null || !dir.HasAuthority) return;
            float accept = dir.ResolveParam(DayModifierStack.Params.AcceptSeconds, dir.Tuning.MarketAcceptSeconds);
            _scratch.Clear();
            foreach (var kv in _inside)
            {
                var item = kv.Key; var t = kv.Value;
                if (item == null || (item is Object o && o == null)) { _scratch.Add(item); continue; }
                bool open = dir.IsMarketOpen || (t.InsideAtClose && dir.InSundownGrace);
                bool held = item is Goods g && g.IsHeld;
                if (!open || held) { t.Rest = 0f; continue; }
                t.Rest += Time.deltaTime;
                if (t.Rest >= accept)
                {
                    _scratch.Add(item);
                    dir.SellFromPen(item, this);
                }
            }
            foreach (var r in _scratch) _inside.Remove(r);
        }
    }
}
