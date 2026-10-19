using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// 0.3 s "poof" of hay others see when a farmer confirms a new loadout (GDD Wardrobe: "other players see a 0.3 s poof of hay").
    /// Cheap: a dozen shared-material cubes that fly out and shrink, then the object destroys itself.
    /// </summary>
    public sealed class HayPoof : MonoBehaviour
    {
        const int Bits = 14;
        Transform[] _bits;
        Vector3[] _vel;
        float _life, _age;

        /// <summary>Spawns a poof at <paramref name="position"/> lasting <paramref name="duration"/> seconds.</summary>
        public static HayPoof Spawn(Vector3 position, float duration, Material baseMaterial)
        {
            var go = new GameObject("HayPoof");
            go.transform.position = position;
            var poof = go.AddComponent<HayPoof>();
            poof.Init(Mathf.Max(0.05f, duration), baseMaterial);
            return poof;
        }

        void Init(float duration, Material baseMaterial)
        {
            _life = duration;
            _bits = new Transform[Bits];
            _vel = new Vector3[Bits];
            Color[] hay = { new Color(0.91f, 0.78f, 0.45f), new Color(0.8f, 0.64f, 0.3f), new Color(0.96f, 0.88f, 0.6f) };
            for (int i = 0; i < Bits; i++)
            {
                var part = new CosmeticPart { shape = PartShape.Cube, size = new Vector3(0.04f, 0.04f, 0.22f), color = hay[i % hay.Length], euler = Random.rotation.eulerAngles };
                _bits[i] = AccessoryBuilder.BuildPart(part, transform, baseMaterial, default).transform;
                _vel[i] = Random.onUnitSphere * Random.Range(2.5f, 4.5f) + Vector3.up;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            float k = 1f - Mathf.Clamp01(_age / _life);
            for (int i = 0; i < _bits.Length; i++)
            {
                _bits[i].localPosition += _vel[i] * dt;
                _bits[i].localScale = new Vector3(0.04f, 0.04f, 0.22f) * k;
            }
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
