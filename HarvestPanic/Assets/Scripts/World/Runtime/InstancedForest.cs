using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.World
{
    /// <summary>
    /// Client-local scenery forest beyond the playable area, drawn with GPU instancing instead of thousands of
    /// GameObjects or terrain trees (terrain mesh trees cost one draw call each: ~5,000 draws measured).
    /// Instances are grouped into square chunks per prototype; each chunk is one <see cref="Graphics.RenderMeshInstanced"/>
    /// call per submesh with chunk world bounds, so every camera frustum-culls whole chunks (shadows included).
    /// Submitted from <see cref="RenderPipelineManager.beginCameraRendering"/>, so it also shows in editor captures.
    /// No colliders, no gameplay: the world-edge colliders keep everyone inside.
    /// </summary>
    [ExecuteAlways]
    public sealed class InstancedForest : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Prototype
        {
            public Mesh mesh;
            [Tooltip("Cheaper mesh used beyond the forest's lodDistance (null = always mesh)")] public Mesh farMesh;
            public Material[] materials = new Material[0];
            [Tooltip("x, y, z = base position (world), w = uniform scale")] public Vector4[] instances = new Vector4[0];
        }

        [SerializeField] Prototype[] prototypes = new Prototype[0];
        [SerializeField, Min(20f)] float chunkSize = 96f;
        [SerializeField, Min(50f)] float maxDistance = 560f;
        [SerializeField, Min(20f)] float lodDistance = 220f;

        sealed class Chunk { public int proto; public Matrix4x4[] matrices; public Bounds bounds; }
        readonly List<Chunk> _chunks = new List<Chunk>();

        /// <summary>Build-time setup.</summary>
        public void Configure(Prototype[] protos, float chunk, float distance) { prototypes = protos; chunkSize = chunk; maxDistance = distance; Rebuild(); }

        /// <summary>Total instances (tests, perf notes).</summary>
        public int InstanceCount { get { int n = 0; foreach (var p in prototypes) n += p.instances.Length; return n; } }

        /// <summary>Deterministic yaw from the position (keeps the serialized data at 4 floats per tree).</summary>
        public static float YawFor(Vector4 v) => Mathf.Repeat(Mathf.Sin(v.x * 12.9898f + v.z * 78.233f) * 43758.5453f, 1f) * 360f;

        void OnEnable() { Rebuild(); RenderPipelineManager.beginCameraRendering += OnBeginCamera; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= OnBeginCamera; _chunks.Clear(); }

        void Rebuild()
        {
            _chunks.Clear();
            for (int p = 0; p < prototypes.Length; p++)
            {
                var proto = prototypes[p];
                if (proto.mesh == null) continue;
                var cells = new Dictionary<Vector2Int, List<Matrix4x4>>();
                var mb = proto.mesh.bounds;
                foreach (var v in proto.instances)
                {
                    var key = new Vector2Int(Mathf.FloorToInt(v.x / chunkSize), Mathf.FloorToInt(v.z / chunkSize));
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Matrix4x4>();
                    list.Add(Matrix4x4.TRS(new Vector3(v.x, v.y, v.z), Quaternion.Euler(0f, YawFor(v), 0f), Vector3.one * v.w));
                }
                foreach (var kv in cells)
                {
                    var b = new Bounds();
                    bool first = true;
                    foreach (var m in kv.Value)
                    {
                        var c = m.MultiplyPoint3x4(mb.center);
                        float s = m.lossyScale.x;
                        var e = new Bounds(c, new Vector3(mb.size.x, mb.size.y, mb.size.z) * s * 1.42f);
                        if (first) { b = e; first = false; } else b.Encapsulate(e);
                    }
                    _chunks.Add(new Chunk { proto = p, matrices = kv.Value.ToArray(), bounds = b });
                }
            }
        }

        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            Vector3 eye = cam.transform.position;
            // Past linear fog's end everything is fog colour (night: 150 m, rain: 220 m), so stop drawing there.
            float max = RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear ? Mathf.Min(maxDistance, RenderSettings.fogEndDistance * 1.15f + 40f) : maxDistance;
            float max2 = max * max, lod2 = lodDistance * lodDistance;
            foreach (var ch in _chunks)
            {
                float d2 = ch.bounds.SqrDistance(eye);
                if (d2 > max2) continue;
                var proto = prototypes[ch.proto];
                var mesh = d2 > lod2 && proto.farMesh != null ? proto.farMesh : proto.mesh;
                int subs = Mathf.Min(mesh.subMeshCount, proto.materials.Length);
                for (int s = 0; s < subs; s++)
                {
                    var mat = proto.materials[s];
                    if (mat == null || !mat.enableInstancing) continue;
                    var rp = new RenderParams(mat)
                    {
                        camera = cam, worldBounds = ch.bounds, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true,
                        layer = gameObject.layer,
                    };
                    Graphics.RenderMeshInstanced(rp, mesh, s, ch.matrices);
                }
            }
        }
    }
}
