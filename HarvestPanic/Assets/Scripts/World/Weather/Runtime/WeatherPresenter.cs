using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.Weather
{
    /// <summary>
    /// Local-only weather + day-phase visuals (art bible §4.3): blends toward <see cref="Target"/> and pushes it through
    /// <see cref="WeatherLookApplier"/> (sun, sky copy, ambient, linear fog, runtime global Volume priority 20, stylized
    /// shading globals); rain streaks and wind leaves follow the local camera.
    /// Cheap by design: two particle systems with hard caps, no lights, no extra cameras. Restores the scene on destroy.
    /// </summary>
    public sealed class WeatherPresenter : MonoBehaviour
    {
        const int RainCap = 1200, StormCap = 2000, LeafCap = 60;

        WeatherDirector _dir;
        Material _rainMat, _leafMat;
        readonly WeatherLookApplier _applier = new WeatherLookApplier();
        WeatherLook _look;
        bool _hasLook;
        ParticleSystem _rain, _leaves;

        /// <summary>The blended look currently shown (QA/tests).</summary>
        public WeatherLook CurrentLook => _look;
        /// <summary>Rain emitter (null until first rain).</summary>
        public ParticleSystem Rain => _rain;
        /// <summary>Leaf emitter (null until first wind).</summary>
        public ParticleSystem Leaves => _leaves;

        /// <summary>Called once by the director.</summary>
        public void Init(WeatherDirector dir, Material rain, Material leaf) { _dir = dir; _rainMat = rain; _leafMat = leaf; }

        void Start() { _applier.Capture(transform); }

        void OnDestroy() { _applier.Restore(); }

        /// <summary>Target look for the current phase and weather on this peer.</summary>
        public static WeatherLook Target(IWeatherService w)
        {
            DayClockPhase phase = DayClockPhase.None; float prog = 0f;
            if (Services.TryGet(out IDayClockService clock)) { phase = clock.Phase; prog = clock.PhaseProgress01; }
            float fog = Services.TryGet(out IDayModifierService d) ? d.Resolve(DayParams.WeatherFogDistance, 0f) : 0f;
            return WeatherLook.WithWeather(WeatherLook.ForPhase(phase, prog), w != null ? w.Current : WeatherKind.Clear, fog);
        }

        /// <summary>Snaps to the target look now (screenshots, scene loads).</summary>
        public void Snap() { _look = Target(_dir); _hasLook = true; Apply(); UpdateParticles(true); }

        void LateUpdate()
        {
            if (_dir == null) return;
            var target = Target(_dir);
            _look = _hasLook ? WeatherLook.Lerp(_look, target, 1f - Mathf.Exp(-Time.deltaTime * 1.2f)) : target;
            _hasLook = true;
            Apply();
            UpdateParticles(false);
        }

        void Apply()
        {
            if (!_applier.Captured) _applier.Capture(transform);
            _applier.Apply(_look);
        }

        void UpdateParticles(bool snap)
        {
            var cam = Camera.main;
            var k = _dir.Current;
            bool raining = k == WeatherKind.Rain || k == WeatherKind.Storm;
            Vector3 wind = _dir.Wind;
            bool windy = wind.sqrMagnitude > 0.25f;
            if (raining && _rain == null) _rain = MakeRain();
            if (windy && _leaves == null) _leaves = MakeLeaves();
            Vector3 at = cam != null ? cam.transform.position : Vector3.zero;
            if (_rain != null)
            {
                _rain.transform.position = at + Vector3.up * 12f - wind * 0.4f;
                var em = _rain.emission; em.rateOverTime = raining ? (k == WeatherKind.Storm ? 1600f : 900f) : 0f;
                var main = _rain.main; main.maxParticles = k == WeatherKind.Storm ? StormCap : RainCap;
                var vel = _rain.velocityOverLifetime; vel.x = wind.x; vel.z = wind.z;
                if (snap && !raining) _rain.Clear();
                if (snap && raining) { _rain.Clear(); _rain.Simulate(1.2f, true, true); _rain.Play(); }
            }
            if (_leaves != null)
            {
                _leaves.transform.position = at - wind.normalized * 14f + Vector3.up * 3f;
                _leaves.transform.rotation = windy ? Quaternion.LookRotation(wind.normalized) : Quaternion.identity;
                var em = _leaves.emission; em.rateOverTime = windy ? Mathf.Clamp(wind.magnitude * 2.5f, 5f, 30f) : 0f;
                var main = _leaves.main; main.startSpeed = new ParticleSystem.MinMaxCurve(wind.magnitude * 0.8f, wind.magnitude * 1.2f);
                if (snap && !windy) _leaves.Clear();
                if (snap && windy) { _leaves.Clear(); _leaves.Simulate(3f, true, true); _leaves.Play(); }
            }
        }

        static Material Fallback(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh);
            m.SetColor("_BaseColor", c); m.color = c;
            return m;
        }

        ParticleSystem MakeRain()
        {
            var go = new GameObject("Weather Rain");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.1f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.startColor = new Color(0.78f, 0.84f, 0.92f, 0.55f); main.maxParticles = RainCap;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(36f, 1f, 36f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = 0f; vel.y = -16f; vel.z = 0f;
            var col = ps.collision; col.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 2.5f; r.maxParticleSize = 0.004f; // keep near-camera streaks thin
            r.sharedMaterial = _rainMat != null ? _rainMat : Fallback(new Color(0.8f, 0.86f, 0.95f, 0.5f));
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.enableGPUInstancing = true;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeLeaves()
        {
            var go = new GameObject("Weather Leaves");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 3.5f; main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startRotation3D = true; main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f); main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.66f, 0.27f), new Color(0.86f, 0.62f, 0.22f));
            main.maxParticles = LeafCap; main.gravityModifier = 0.05f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(26f, 5f, 1f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true; rot.x = 4f; rot.y = 3f; rot.z = 5f;
            var noise = ps.noise; noise.enabled = true; noise.strength = 1.2f; noise.frequency = 0.4f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard; r.alignment = ParticleSystemRenderSpace.World;
            r.sharedMaterial = _leafMat != null ? _leafMat : Fallback(new Color(0.7f, 0.6f, 0.25f, 1f));
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }
}
