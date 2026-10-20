using System.Text;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.AI.Editor
{
    /// <summary>
    /// Polish-2: the goat uses the Quaternius sheep mesh as a stand-in and read as a sheep. This adds a goat silhouette on
    /// the existing prefab: two swept-back primitive horns and a chin beard on the head bone, and dark lower-leg sleeves on
    /// the leg bones (colliderless, no shadows-casting changes to gameplay). Idempotent (rebuilds its "GoatParts").
    /// Run: -executeMethod HarvestPanic.AI.Editor.GoatSilhouetteBuilder.Build
    /// </summary>
    public static class GoatSilhouetteBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Critters/Critter_Goat.prefab";
        public const string MatDir = "Assets/Prefabs/Critters/Materials";

        public const string PartsName = "GoatParts";

        public static void Build()
        {
            var horn = Mat("M_Goat_Horn", new Color(0.52f, 0.46f, 0.36f), 0.35f);
            var beard = Mat("M_Goat_Beard", new Color(0.36f, 0.32f, 0.27f), 0.05f);
            var leg = Mat("M_Goat_Leg", new Color(0.17f, 0.14f, 0.12f), 0.1f);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && t.name.StartsWith(PartsName)) Object.DestroyImmediate(t.gameObject);
                Transform head = Find(root.transform, "Head");
                if (head == null) throw new System.InvalidOperationException("Goat Head bone not found");
                // World-space layout measured from the sheep stand-in (eye marker at y 0.51, z 0.42; legs 0.24 -> 0.01).
                var hp = Group(head, PartsName + "_Head");
                for (int side = -1; side <= 1; side += 2)
                {
                    // Swept-back horns: two segments so the tip curls back over the neck.
                    Part(hp, "Horn" + side, PrimitiveType.Capsule, new Vector3(0.05f * side, 0.66f, 0.36f), Quaternion.Euler(-35f, 0f, -12f * side), new Vector3(0.045f, 0.08f, 0.045f), horn);
                    Part(hp, "HornTip" + side, PrimitiveType.Capsule, new Vector3(0.065f * side, 0.73f, 0.27f), Quaternion.Euler(-75f, 0f, -18f * side), new Vector3(0.032f, 0.06f, 0.032f), horn);
                }
                Part(hp, "Beard", PrimitiveType.Cube, new Vector3(0f, 0.34f, 0.55f), Quaternion.Euler(12f, 0f, 0f), new Vector3(0.06f, 0.12f, 0.045f), beard);
                foreach (var legName in new[] { "FrontLeg.L", "FrontLeg.R", "BackLeg.L", "BackLeg.R" })
                {
                    var b = Find(root.transform, legName);
                    if (b == null) continue;
                    var g = Group(b, PartsName + "_" + legName);
                    Vector3 p = b.position; p.y = 0.08f;
                    Part(g, "Shin", PrimitiveType.Cube, p, b.rotation, new Vector3(0.125f, 0.16f, 0.125f), leg);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Goat] silhouette parts rebuilt on " + PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = Find(c, name); if (r != null) return r; }
            return null;
        }

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static void Part(Transform parent, string name, PrimitiveType type, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            Vector3 ls = parent.lossyScale;
            go.transform.localScale = new Vector3(worldScale.x / ls.x, worldScale.y / ls.y, worldScale.z / ls.z);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        static Material Mat(string name, Color c, float smooth)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.color = c; m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static void Dump()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var sb = new StringBuilder();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                sb.AppendLine($"{Path(t, root.transform)} pos={t.position} scale={t.lossyScale}");
            Debug.Log("[GoatDump]\n" + sb);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static string Path(Transform t, Transform root) => t == root ? t.name : Path(t.parent, root) + "/" + t.name;
    }
}
