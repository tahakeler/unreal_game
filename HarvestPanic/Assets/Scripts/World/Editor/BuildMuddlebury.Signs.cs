using UnityEngine;

namespace HarvestPanic.World.Editor
{
    public static partial class BuildMuddlebury
    {
        const int SignFontSize = 64;

        /// <summary>
        /// Painted lettering (Lilita One TextMesh, lit by <see cref="LitText"/>) centred on <paramref name="centre"/>, readable
        /// by someone standing in direction <paramref name="facing"/> (yaw, degrees; 0 = reader to the +Z side). The text is
        /// fitted into width x height. The saved renderer keeps the font's own material as a fallback.
        /// </summary>
        static GameObject Lettering(string text, Vector3 centre, float facing, float width, float height, Transform parent, Color ink)
        {
            if (Mats.SignFont == null) return null;
            var go = new GameObject("Text " + text.Replace("\n", " "));
            go.transform.SetParent(parent, false);
            var f = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
            go.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(-f, Vector3.up));
            var tm = go.AddComponent<TextMesh>();
            tm.font = Mats.SignFont;
            tm.fontSize = SignFontSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = ink;
            tm.text = text;
            int lines = 1, longest = 0, cur = 0;
            foreach (char c in text) { if (c == '\n') { lines++; longest = Mathf.Max(longest, cur); cur = 0; } else cur++; }
            longest = Mathf.Max(longest, cur);
            // Lilita One: ~0.56 em average advance, ~1.15 em line pitch. Line height in metres = fontSize * characterSize / 10.
            float lineH = Mathf.Min(height * 0.86f / (lines * 1.15f), width * 0.9f / Mathf.Max(1, longest * 0.56f));
            tm.characterSize = lineH / (SignFontSize * 0.1f);
            tm.lineSpacing = 1f;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Mats.SignFont.material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<LitText>().Configure(Mats.SignText);
            return go;
        }

        /// <summary>A painted board with lettering (cube board: instanced, cheap). <paramref name="twoSided"/> letters both faces.</summary>
        static GameObject SignBoard(string text, Vector3 centre, float facing, float width, float height, Transform parent, Material board, Color ink, bool twoSided = false, float thickness = 0.08f)
        {
            var b = Block("Sign " + text.Replace("\n", " "), centre, new Vector3(width, height, thickness), board, parent, facing, collider: false);
            b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var f = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
            Lettering(text, centre + f * (thickness * 0.5f + 0.012f), facing, width * 0.92f, height * 0.9f, b.transform.parent, ink);
            if (twoSided) Lettering(text, centre - f * (thickness * 0.5f + 0.012f), facing + 180f, width * 0.92f, height * 0.9f, b.transform.parent, ink);
            return b;
        }

        /// <summary>Board on a wooden post (ground at <paramref name="foot"/>), readable from direction <paramref name="facing"/>.</summary>
        static GameObject SignPost(string text, Vector3 foot, float facing, float width, float height, Transform parent, Material board, Color ink, float postHeight = 1.25f, bool twoSided = false)
        {
            var g = new GameObject("SignPost " + text.Replace("\n", " ")).transform;
            g.SetParent(parent, false);
            float gy = GroundY(foot.x, foot.z);
            var f = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
            var postGo = Block("Post", new Vector3(foot.x, gy + (postHeight + height) * 0.5f, foot.z) - f * 0.07f, new Vector3(0.14f, postHeight + height, 0.14f), Mats.WoodDark, g, facing);
            _ = postGo;
            SignBoard(text, new Vector3(foot.x, gy + postHeight + height * 0.5f, foot.z), facing, width, height, g, board, ink, twoSided);
            return g.gameObject;
        }
    }
}
