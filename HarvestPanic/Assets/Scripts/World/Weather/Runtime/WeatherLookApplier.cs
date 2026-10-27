using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.Weather
{
    /// <summary>
    /// Pushes a <see cref="WeatherLook"/> into the scene: sun (pitch, colour, intensity), trilight ambient (x Ambient),
    /// linear fog, the stylized sky material (a per-scene copy), a runtime global Volume (grade, bloom, vignette, lift,
    /// white balance) and the stylized-shading globals (_HP_ShadowShift, _HP_LookParams). The ONE mapping used by the
    /// live <see cref="WeatherPresenter"/> and by every QA capture path, so screenshots show exactly what players see.
    /// Capture() remembers the scene state; Restore() puts it back.
    /// </summary>
    public sealed class WeatherLookApplier
    {
        public static readonly int ShadowShiftId = Shader.PropertyToID("_HP_ShadowShift");
        public static readonly int LookParamsId = Shader.PropertyToID("_HP_LookParams");
        static readonly int ZenithId = Shader.PropertyToID("_ZenithColor"), HorizonId = Shader.PropertyToID("_HorizonColor"),
            GroundId = Shader.PropertyToID("_GroundColor"), SkyTintId = Shader.PropertyToID("_SkyTint"), ExposureId = Shader.PropertyToID("_Exposure"),
            SunDiscId = Shader.PropertyToID("_SunDiscColor"), SunGlowColId = Shader.PropertyToID("_SunGlowColor"), SunGlowId = Shader.PropertyToID("_SunGlow"),
            CloudCoverId = Shader.PropertyToID("_CloudCover"), CloudColId = Shader.PropertyToID("_CloudColor"), CloudShadeId = Shader.PropertyToID("_CloudShadeColor"),
            StarsId = Shader.PropertyToID("_StarIntensity"), DiscSizeId = Shader.PropertyToID("_SunDiscSize");

        /// <summary>Key-light shadow strength while a look is applied (stylized shadows: the ambient fills them, not the light).</summary>
        public const float SunShadowStrength = 0.92f;

        Light _sun;
        Quaternion _sunBaseRot;
        Color _sunBaseColor, _ambSky, _ambEq, _ambGround, _fogBaseColor;
        float _sunBaseIntensity, _sunBaseShadow, _fogStartBase, _fogEndBase;
        bool _fogBase, _captured;
        FogMode _fogModeBase;
        Material _sky, _sceneSky;
        GameObject _volumeGo;
        VolumeProfile _profile;
        ColorAdjustments _ca;
        Bloom _bloom;
        Vignette _vignette;
        LiftGammaGain _lgg;
        WhiteBalance _wb;

        public bool Captured => _captured;
        /// <summary>The runtime global volume (priority 20) or null before Capture.</summary>
        public Volume Volume => _volumeGo != null ? _volumeGo.GetComponent<Volume>() : null;

        /// <summary>Remembers the scene's lighting and creates the runtime volume under <paramref name="parent"/> (null = scene root).</summary>
        public void Capture(Transform parent)
        {
            if (_captured) return;
            _sun = RenderSettings.sun;
            if (_sun == null) foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { _sun = l; break; }
            if (_sun != null) { _sunBaseRot = _sun.transform.rotation; _sunBaseColor = _sun.color; _sunBaseIntensity = _sun.intensity; _sunBaseShadow = _sun.shadowStrength; }
            _ambSky = RenderSettings.ambientSkyColor; _ambEq = RenderSettings.ambientEquatorColor; _ambGround = RenderSettings.ambientGroundColor;
            _fogBase = RenderSettings.fog; _fogModeBase = RenderSettings.fogMode; _fogBaseColor = RenderSettings.fogColor;
            _fogStartBase = RenderSettings.fogStartDistance; _fogEndBase = RenderSettings.fogEndDistance;
            _sceneSky = RenderSettings.skybox;
            if (_sceneSky != null) { _sky = new Material(_sceneSky) { name = _sceneSky.name + " (weather)" }; RenderSettings.skybox = _sky; }

            _volumeGo = new GameObject("Weather Volume");
            if (parent != null) _volumeGo.transform.SetParent(parent, false);
            var volume = _volumeGo.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 20;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "Weather Look (runtime)";
            _ca = _profile.Add<ColorAdjustments>(true);
            _bloom = _profile.Add<Bloom>(true);
            _vignette = _profile.Add<Vignette>(true);
            _lgg = _profile.Add<LiftGammaGain>(true);
            _wb = _profile.Add<WhiteBalance>(true);
            volume.profile = _profile;
            _captured = true;
        }

        /// <summary>Applies the look (call Capture first).</summary>
        public void Apply(in WeatherLook l)
        {
            if (!_captured) return;
            if (_sun != null)
            {
                var e = _sunBaseRot.eulerAngles;
                _sun.transform.rotation = Quaternion.Euler(l.SunPitch, e.y, 0f);
                _sun.color = l.SunColor;
                _sun.intensity = l.SunIntensity;
                _sun.shadowStrength = SunShadowStrength;
            }
            var a = l.Ambient;
            RenderSettings.ambientSkyColor = _ambSky * a; RenderSettings.ambientEquatorColor = _ambEq * a; RenderSettings.ambientGroundColor = _ambGround * a;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = l.FogColor; RenderSettings.fogStartDistance = l.FogStart; RenderSettings.fogEndDistance = l.FogEnd;

            if (_sky != null) ApplySky(_sky, l);

            // Stylized shading globals: shadow tint as "1 - tint" so an unset global (zero) means no tint.
            var st = l.ShadowTint;
            Shader.SetGlobalVector(ShadowShiftId, new Vector4(1f - st.r, 1f - st.g, 1f - st.b, 0f));
            Shader.SetGlobalVector(LookParamsId, new Vector4(l.RimBoost, 0f, l.AmbientFloor, 0f));

            _ca.saturation.Override(l.Saturation);
            _ca.postExposure.Override(l.PostExposure);
            _ca.contrast.Override(l.Contrast);
            _bloom.intensity.Override(l.BloomIntensity);
            _bloom.threshold.Override(l.BloomThreshold);
            _bloom.scatter.Override(0.62f);
            _vignette.intensity.Override(l.Vignette);
            _vignette.smoothness.Override(0.45f);
            _lgg.lift.Override(new Vector4(l.Lift.r, l.Lift.g, l.Lift.b, l.Lift.a));
            _wb.temperature.Override(l.Temperature);
        }

        /// <summary>Sets the stylized sky material's gradient, disc, glow, clouds and stars from a look.</summary>
        public static void ApplySky(Material sky, in WeatherLook l)
        {
            if (sky.HasProperty(ZenithId))
            {
                sky.SetColor(ZenithId, l.SkyTint);
                sky.SetColor(HorizonId, l.Horizon);
                sky.SetColor(GroundId, Color.Lerp(l.Horizon, l.FogColor, 0.5f) * 0.85f);
                sky.SetColor(SkyTintId, Color.white);
                sky.SetFloat(ExposureId, l.SkyExposure);
                sky.SetColor(SunDiscId, l.SunDisc);
                sky.SetFloat(DiscSizeId, l.Stars > 0.5f ? 2.6f : 1.6f); // the moon reads bigger than the sun
                var glow = Color.Lerp(l.SunColor, l.Horizon, 0.35f);
                sky.SetColor(SunGlowColId, glow);
                sky.SetFloat(SunGlowId, Mathf.Clamp(l.SunIntensity * 0.25f, 0.05f, 0.6f));
                sky.SetFloat(CloudCoverId, l.CloudCover);
                float key = Mathf.Clamp01(l.SunIntensity / 2f);
                var lit = Color.Lerp(l.Horizon, Color.Lerp(Color.white, l.SunColor, 0.35f), 0.25f + 0.75f * key) * Mathf.Lerp(0.55f, 1.02f, key);
                var shade = Color.Lerp(l.SkyTint, l.Horizon, 0.6f) * Mathf.Lerp(0.7f, 0.92f, key);
                lit.a = shade.a = 1f;
                sky.SetColor(CloudColId, lit);
                sky.SetColor(CloudShadeId, shade);
                sky.SetFloat(StarsId, l.Stars);
            }
            else
            {
                // URP procedural skybox fallback (scenes not yet converted)
                if (sky.HasProperty(SkyTintId)) sky.SetColor(SkyTintId, l.SkyTint);
                if (sky.HasProperty(ExposureId)) sky.SetFloat(ExposureId, l.SkyExposure);
                if (sky.HasProperty(GroundId)) sky.SetColor(GroundId, Color.Lerp(l.FogColor, l.SkyTint, 0.3f));
            }
        }

        /// <summary>Puts the captured scene state back and destroys the runtime volume and sky copy.</summary>
        public void Restore()
        {
            if (!_captured) return;
            if (_sun != null) { _sun.transform.rotation = _sunBaseRot; _sun.color = _sunBaseColor; _sun.intensity = _sunBaseIntensity; _sun.shadowStrength = _sunBaseShadow; }
            RenderSettings.ambientSkyColor = _ambSky; RenderSettings.ambientEquatorColor = _ambEq; RenderSettings.ambientGroundColor = _ambGround;
            RenderSettings.fog = _fogBase; RenderSettings.fogMode = _fogModeBase; RenderSettings.fogColor = _fogBaseColor;
            RenderSettings.fogStartDistance = _fogStartBase; RenderSettings.fogEndDistance = _fogEndBase;
            if (_sceneSky != null) RenderSettings.skybox = _sceneSky;
            Shader.SetGlobalVector(ShadowShiftId, Vector4.zero);
            Shader.SetGlobalVector(LookParamsId, Vector4.zero);
            DestroyAny(_sky);
            DestroyAny(_profile);
            DestroyAny(_volumeGo);
            _sky = null; _profile = null; _volumeGo = null;
            _captured = false;
        }

        static void DestroyAny(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
