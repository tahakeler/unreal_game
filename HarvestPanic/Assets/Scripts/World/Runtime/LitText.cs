using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.World
{
    /// <summary>
    /// Client-local presentation for painted sign lettering: swaps a <see cref="TextMesh"/>'s unlit font material for a
    /// lit copy of <see cref="template"/> (shader HarvestPanic/SignText) bound to the font's dynamic atlas, so signs dim
    /// at dusk/night like the boards they sit on. One shared material per font; rebinds when the atlas is rebuilt.
    /// The saved scene keeps the font's own material as the fallback.
    /// </summary>
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class LitText : MonoBehaviour
    {
        [SerializeField] Material template;

        static readonly Dictionary<Font, Material> Shared = new Dictionary<Font, Material>();
        static bool _hooked;

        /// <summary>Build-time setup.</summary>
        public void Configure(Material litTemplate) => template = litTemplate;

        void OnEnable() => Apply();

        /// <summary>Binds the lit material (also callable from editor captures, which never save the scene).</summary>
        public void Apply()
        {
            var tm = GetComponent<TextMesh>();
            if (template == null || tm == null || tm.font == null || tm.font.material == null) return;
            if (!_hooked) { Font.textureRebuilt += OnRebuilt; _hooked = true; }
            if (!Shared.TryGetValue(tm.font, out var m) || m == null)
            {
                m = new Material(template) { name = template.name + " (" + tm.font.name + ")", hideFlags = HideFlags.DontSave };
                m.mainTexture = tm.font.material.mainTexture;
                Shared[tm.font] = m;
            }
            GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        static void OnRebuilt(Font f)
        {
            if (f != null && Shared.TryGetValue(f, out var m) && m != null) m.mainTexture = f.material.mainTexture;
        }

        /// <summary>Applies every sign in the loaded scenes (editor QA captures).</summary>
        public static int ApplyAll()
        {
            var all = FindObjectsByType<LitText>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var t in all) t.Apply();
            return all.Length;
        }
    }
}
