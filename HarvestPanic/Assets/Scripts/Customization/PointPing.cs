using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>World ping placed by the Point emote (GDD: "Places a 3 s world ping at the aimed location (≤60 m), visible to all players").</summary>
    public sealed class PointPing : MonoBehaviour
    {
        float _life, _age;
        Transform _ball;

        /// <summary>Spawns a ping at <paramref name="position"/> in the pointer's name-tag colour.</summary>
        public static PointPing Spawn(Vector3 position, Color color, float lifetime, Material unlit)
        {
            var go = new GameObject("PointPing");
            go.transform.position = position;
            var ping = go.AddComponent<PointPing>();
            ping._life = Mathf.Max(0.1f, lifetime);
            AccessoryBuilder.BuildPart(new CosmeticPart { shape = PartShape.Cylinder, position = new Vector3(0, 1f, 0), size = new Vector3(0.06f, 2f, 0.06f), color = color }, go.transform, unlit, default);
            AccessoryBuilder.BuildPart(new CosmeticPart { shape = PartShape.Cylinder, position = new Vector3(0, 0.01f, 0), size = new Vector3(0.9f, 0.02f, 0.9f), color = color }, go.transform, unlit, default);
            ping._ball = AccessoryBuilder.BuildPart(new CosmeticPart { shape = PartShape.Cone, position = new Vector3(0, 2.6f, 0), euler = new Vector3(180, 0, 0), size = new Vector3(0.4f, 0.5f, 0.4f), color = color }, go.transform, unlit, default).transform;
            return ping;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_ball != null) _ball.localPosition = new Vector3(0, 2.6f + Mathf.Abs(Mathf.Sin(_age * 6f)) * 0.3f, 0);
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
