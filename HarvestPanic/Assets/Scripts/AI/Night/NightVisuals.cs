using UnityEngine;

namespace HarvestPanic.AI.Night
{
    /// <summary>Builds night creature silhouettes from primitives (no colliders) at runtime on every peer.</summary>
    public static class NightVisuals
    {
        static Shader _lit;
        public static Material Mat(Color c, float smooth = 0.15f)
        {
            if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(_lit);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        public static Transform Part(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale; go.transform.localEulerAngles = euler;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }

        /// <summary>Mr. Stitches: pole, crossbar arms in a plaid shirt, burlap sack head with button eyes and a stitched grin, floppy straw hat, straw tufts.</summary>
        public static Transform BuildStitches(Transform root)
        {
            var v = new GameObject("StitchesVisual").transform; v.SetParent(root, false);
            Material wood = Mat(new Color(0.36f, 0.25f, 0.15f)), plaid = Mat(new Color(0.55f, 0.12f, 0.1f)), plaid2 = Mat(new Color(0.18f, 0.16f, 0.3f)),
                sack = Mat(new Color(0.72f, 0.6f, 0.4f), 0.05f), straw = Mat(new Color(0.9f, 0.76f, 0.35f)), dark = Mat(new Color(0.04f, 0.03f, 0.03f), 0.6f),
                hat = Mat(new Color(0.62f, 0.5f, 0.25f)), denim = Mat(new Color(0.22f, 0.3f, 0.42f));
            Part(PrimitiveType.Cylinder, v, "Pole", new Vector3(0, 1.1f, 0), new Vector3(0.12f, 1.1f, 0.12f), wood);
            Part(PrimitiveType.Cylinder, v, "Crossbar", new Vector3(0, 1.75f, 0), new Vector3(0.08f, 0.95f, 0.08f), wood, new Vector3(0, 0, 90));
            Part(PrimitiveType.Cube, v, "Shirt", new Vector3(0, 1.5f, 0), new Vector3(0.62f, 0.7f, 0.32f), plaid);
            for (int i = 0; i < 3; i++) Part(PrimitiveType.Cube, v, "Stripe" + i, new Vector3(0, 1.28f + i * 0.2f, 0.165f), new Vector3(0.63f, 0.05f, 0.01f), plaid2);
            Part(PrimitiveType.Cube, v, "Patch", new Vector3(0.14f, 1.38f, 0.17f), new Vector3(0.16f, 0.16f, 0.01f), denim);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(PrimitiveType.Cube, v, "Sleeve", new Vector3(s * 0.62f, 1.74f, 0), new Vector3(0.66f, 0.22f, 0.24f), plaid, new Vector3(0, 0, s * -8f));
                for (int k = 0; k < 4; k++)
                    Part(PrimitiveType.Cube, v, "HandStraw", new Vector3(s * (0.98f + k * 0.03f), 1.68f + k * 0.04f, (k - 1.5f) * 0.05f), new Vector3(0.22f, 0.025f, 0.025f), straw, new Vector3(0, k * 12f - 18f, s * (20f - k * 14f)));
            }
            Part(PrimitiveType.Cube, v, "Trousers", new Vector3(0, 1.0f, 0), new Vector3(0.5f, 0.35f, 0.28f), denim);
            for (int k = 0; k < 6; k++)
                Part(PrimitiveType.Cube, v, "WaistStraw", new Vector3((k - 2.5f) * 0.09f, 0.8f, 0.05f), new Vector3(0.02f, 0.22f, 0.02f), straw, new Vector3(0, 0, (k - 2.5f) * 9f));
            Part(PrimitiveType.Sphere, v, "SackHead", new Vector3(0, 2.18f, 0), new Vector3(0.5f, 0.56f, 0.46f), sack);
            Part(PrimitiveType.Cylinder, v, "SackTie", new Vector3(0, 1.92f, 0), new Vector3(0.3f, 0.03f, 0.3f), straw);
            Part(PrimitiveType.Cylinder, v, "EyeL", new Vector3(-0.1f, 2.24f, 0.215f), new Vector3(0.1f, 0.01f, 0.1f), dark, new Vector3(90, 0, 0));
            Part(PrimitiveType.Cylinder, v, "EyeR", new Vector3(0.11f, 2.21f, 0.215f), new Vector3(0.075f, 0.01f, 0.075f), dark, new Vector3(90, 0, 0));
            Part(PrimitiveType.Cube, v, "Grin", new Vector3(0, 2.07f, 0.205f), new Vector3(0.26f, 0.015f, 0.01f), dark, new Vector3(0, 0, -6f));
            for (int k = 0; k < 6; k++)
                Part(PrimitiveType.Cube, v, "Stitch" + k, new Vector3(-0.11f + k * 0.045f, 2.07f - (k - 2.5f) * 0.0045f, 0.21f), new Vector3(0.012f, 0.07f, 0.01f), dark);
            Part(PrimitiveType.Cylinder, v, "HatBrim", new Vector3(0, 2.43f, 0), new Vector3(0.8f, 0.02f, 0.8f), hat, new Vector3(6, 0, -9));
            Part(PrimitiveType.Cylinder, v, "HatCrown", new Vector3(0.02f, 2.55f, 0), new Vector3(0.36f, 0.12f, 0.36f), hat, new Vector3(6, 0, -9));
            for (int k = 0; k < 5; k++)
                Part(PrimitiveType.Cube, v, "NeckStraw", new Vector3((k - 2) * 0.07f, 1.86f, 0.12f), new Vector3(0.02f, 0.14f, 0.02f), straw, new Vector3(20, 0, (k - 2) * 15f));
            return v;
        }

        /// <summary>Fox: orange body, white chest, pointed ears and snout, bushy white-tipped tail.</summary>
        public static Transform BuildFox(Transform root)
        {
            var v = new GameObject("FoxVisual").transform; v.SetParent(root, false);
            Material fur = Mat(new Color(0.85f, 0.38f, 0.1f)), white = Mat(new Color(0.95f, 0.92f, 0.86f)), dark = Mat(new Color(0.08f, 0.05f, 0.04f), 0.6f);
            Part(PrimitiveType.Capsule, v, "Body", new Vector3(0, 0.38f, 0), new Vector3(0.3f, 0.36f, 0.3f), fur, new Vector3(90, 0, 0));
            Part(PrimitiveType.Sphere, v, "Chest", new Vector3(0, 0.34f, 0.2f), new Vector3(0.22f, 0.24f, 0.2f), white);
            Part(PrimitiveType.Sphere, v, "Head", new Vector3(0, 0.55f, 0.4f), new Vector3(0.24f, 0.22f, 0.24f), fur);
            Part(PrimitiveType.Cube, v, "Snout", new Vector3(0, 0.51f, 0.55f), new Vector3(0.09f, 0.08f, 0.16f), white);
            Part(PrimitiveType.Sphere, v, "Nose", new Vector3(0, 0.53f, 0.63f), new Vector3(0.04f, 0.04f, 0.04f), dark);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(PrimitiveType.Cube, v, "Ear", new Vector3(s * 0.07f, 0.7f, 0.38f), new Vector3(0.07f, 0.12f, 0.03f), fur, new Vector3(0, 0, s * -15f));
                Part(PrimitiveType.Sphere, v, "Eye", new Vector3(s * 0.065f, 0.58f, 0.51f), new Vector3(0.035f, 0.035f, 0.02f), dark);
                Part(PrimitiveType.Cylinder, v, "LegF", new Vector3(s * 0.09f, 0.12f, 0.18f), new Vector3(0.06f, 0.12f, 0.06f), dark);
                Part(PrimitiveType.Cylinder, v, "LegB", new Vector3(s * 0.09f, 0.12f, -0.18f), new Vector3(0.06f, 0.12f, 0.06f), dark);
            }
            Part(PrimitiveType.Capsule, v, "Tail", new Vector3(0, 0.42f, -0.45f), new Vector3(0.16f, 0.25f, 0.16f), fur, new Vector3(-60, 0, 0));
            Part(PrimitiveType.Sphere, v, "TailTip", new Vector3(0, 0.55f, -0.66f), new Vector3(0.13f, 0.13f, 0.13f), white);
            return v;
        }
    }
}
