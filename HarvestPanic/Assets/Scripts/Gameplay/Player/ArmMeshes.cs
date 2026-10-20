using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Procedural rounded meshes for the first-person arms (art-bible §3: rounded shapes, visible bevels).
    /// Generated once per size and cached; smooth normals, no UVs needed (flat-colour materials).
    /// </summary>
    public static class ArmMeshes
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>
        /// Capsule along +Y from y=0 (radius <paramref name="r0"/>) to y=<paramref name="length"/> (radius <paramref name="r1"/>),
        /// with hemispherical caps that extend past both ends.
        /// </summary>
        public static Mesh TaperedCapsule(float r0, float r1, float length, int radial = 18, int capRings = 5)
        {
            string key = $"cap{r0:0.0000}_{r1:0.0000}_{length:0.0000}_{radial}_{capRings}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            // Profile (radius, y, normal-y) from bottom pole to top pole.
            var prof = new List<Vector3>();
            for (int i = 0; i <= capRings; i++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * 0.5f * i / capRings;
                prof.Add(new Vector3(Mathf.Cos(a) * r0, Mathf.Sin(a) * r0, Mathf.Sin(a)));
            }
            for (int i = 0; i <= capRings; i++)
            {
                float a = Mathf.PI * 0.5f * i / capRings;
                prof.Add(new Vector3(Mathf.Cos(a) * r1, length + Mathf.Sin(a) * r1, Mathf.Sin(a)));
            }
            var mesh = Lathe(prof, radial);
            mesh.name = "ArmCapsule";
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Ellipsoid with the given full size, centred at the origin.</summary>
        public static Mesh Ellipsoid(Vector3 size, int radial = 18, int rings = 10)
        {
            string key = $"ell{size.x:0.0000}_{size.y:0.0000}_{size.z:0.0000}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
            Vector3 h = size * 0.5f;
            for (int r = 0; r <= rings; r++)
            {
                float v = Mathf.PI * r / rings; // 0..pi from +Y
                for (int s = 0; s <= radial; s++)
                {
                    float u = Mathf.PI * 2f * s / radial;
                    var unit = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                    verts.Add(Vector3.Scale(unit, h));
                    norms.Add(new Vector3(unit.x / h.x, unit.y / h.y, unit.z / h.z).normalized);
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < radial; s++)
                {
                    int a = r * (radial + 1) + s, b = a + radial + 1;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(b); tris.Add(a + 1); tris.Add(b + 1);
                }
            var mesh = new Mesh { name = "ArmEllipsoid" };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        static Mesh Lathe(List<Vector3> profile, int radial)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
            for (int p = 0; p < profile.Count; p++)
            {
                float r = profile[p].x, y = profile[p].y, ny = profile[p].z;
                float nr = Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny));
                for (int s = 0; s <= radial; s++)
                {
                    float u = Mathf.PI * 2f * s / radial;
                    float c = Mathf.Cos(u), sn = Mathf.Sin(u);
                    verts.Add(new Vector3(c * r, y, sn * r));
                    norms.Add(new Vector3(c * nr, ny, sn * nr).normalized);
                }
            }
            for (int p = 0; p < profile.Count - 1; p++)
                for (int s = 0; s < radial; s++)
                {
                    int a = p * (radial + 1) + s, b = a + radial + 1;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            var mesh = new Mesh();
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
