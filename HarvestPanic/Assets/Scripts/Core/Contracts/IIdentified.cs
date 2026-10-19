using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>Data asset addressable by a stable string id.</summary>
    public interface IIdentified
    {
        /// <summary>Unique, stable id.</summary>
        string Id { get; }
    }

    /// <summary>
    /// Lazy id lookup over data assets stored under <c>Assets/Resources/Data/&lt;Folder&gt;/</c>. This is how marker
    /// keys ("Pig", "Livestock.Pig") become definitions without cross-assembly references.
    /// </summary>
    /// <typeparam name="T">A ScriptableObject that implements <see cref="IIdentified"/>.</typeparam>
    public sealed class ContentCatalog<T> where T : ScriptableObject, IIdentified
    {
        readonly string _resourcesFolder;
        Dictionary<string, T> _byId;

        /// <summary>Creates a catalog over a Resources-relative folder, e.g. <c>"Data/Goods"</c>.</summary>
        public ContentCatalog(string resourcesFolder) { _resourcesFolder = resourcesFolder; }

        /// <summary>All loaded assets.</summary>
        public IEnumerable<T> All { get { Load(); return _byId.Values; } }

        /// <summary>Finds an asset by id (case-insensitive).</summary>
        public bool TryGet(string id, out T asset) { Load(); return _byId.TryGetValue(id ?? string.Empty, out asset); }

        /// <summary>Forces a reload on next access (tests, new content).</summary>
        public void Invalidate() => _byId = null;

        void Load()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, T>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var a in Resources.LoadAll<T>(_resourcesFolder))
                if (a != null && !string.IsNullOrEmpty(a.Id)) _byId[a.Id] = a;
        }
    }

    /// <summary>Shared catalogs for the data folders every team authors into.</summary>
    public static class Content
    {
        /// <summary>Goods definitions: <c>Resources/Data/Goods</c>.</summary>
        public static readonly ContentCatalog<GoodsDefinition> Goods = new ContentCatalog<GoodsDefinition>("Data/Goods");
        /// <summary>Critter personalities: <c>Resources/Data/Critters</c>.</summary>
        public static readonly ContentCatalog<CritterPersonality> Critters = new ContentCatalog<CritterPersonality>("Data/Critters");
        /// <summary>Day modifiers: <c>Resources/Data/DayModifiers</c>.</summary>
        public static readonly ContentCatalog<DayModifier> DayModifiers = new ContentCatalog<DayModifier>("Data/DayModifiers");
    }
}
