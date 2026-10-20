using System.IO;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.Weather.Editor
{
    /// <summary>
    /// Builds weather data: Mod.Weather.Day.* Day Modifiers (Resources/Data/DayModifiers/Weather), rain/leaf
    /// materials, the WeatherDirector network prefab and Resources/NetworkPrefabs/Weather.asset.
    /// Unity -batchmode -nographics -projectPath HarvestPanic -executeMethod HarvestPanic.Weather.Editor.WeatherAssetBuilder.Build -quit -logFile /abs/log
    /// </summary>
    public static class WeatherAssetBuilder
    {
        const string ModDir = "Assets/Resources/Data/DayModifiers/Weather";
        const string ArtDir = "Assets/Art/World/Weather";
        const string PrefabPath = "Assets/Prefabs/World/Weather/WeatherDirector.prefab";
        const string ListPath = "Assets/Resources/NetworkPrefabs/Weather.asset";

        struct Fx { public string P; public DayParamOp Op; public float V; public Fx(string p, DayParamOp o, float v) { P = p; Op = o; V = v; } }

        [MenuItem("Harvest Panic/World/Build Weather Assets")]
        public static void Build()
        {
            foreach (var d in new[] { ModDir, ArtDir, Path.GetDirectoryName(PrefabPath) }) Directory.CreateDirectory(d);
            const DayParamOp Mul = DayParamOp.Mul, Ov = DayParamOp.Override;
            Mod(WeatherKind.Clear, DayModifierWeight.Minor, "Clear skies. Grandpa says: no excuses.");
            Mod(WeatherKind.Rain, DayModifierWeight.Minor, "Rain today. Pig will be muddy. Bridge will be slick. We are doomed.",
                new Fx(DayParams.MudSpeedMult, Mul, 0.85f), new Fx(DayParams.WeatherGroundSpeedMult, Ov, 0.92f), new Fx(DayParams.WeatherSoakSeconds, Ov, 45f),
                new Fx(DayParams.RiverCurrentMult, Mul, 1.4f), new Fx(DayParams.BridgeSafeLoadMult, Mul, 0.85f), new Fx(DayParams.NoiseEnvMult, Mul, 0.9f));
            Mod(WeatherKind.Wind, DayModifierWeight.Minor, "Gusty. Hold onto your chickens. And your hat. And Dave.",
                new Fx(DayParams.WeatherWindSpeed, Ov, 8f), new Fx(DayParams.NoiseEnvMult, Mul, 0.9f));
            Mod(WeatherKind.Fog, DayModifierWeight.Minor, "Fog. Shout Marco. If someone shouts Polo, it might be the goat.",
                new Fx(DayParams.WeatherFogDistance, Ov, 22f), new Fx(DayParams.NoiseEnvMult, Mul, 0.8f));
            Mod(WeatherKind.Heatwave, DayModifierWeight.Minor, "Heatwave. The pumpkins are sweating. So are we.",
                new Fx("Goods.FreshnessDecayMult", Mul, 1.5f), new Fx("Player.StaminaMult", Mul, 0.8f));
            Mod(WeatherKind.Storm, DayModifierWeight.Major, "Storm today. Bridge will leave. Do not follow bridge.",
                new Fx(DayParams.MudSpeedMult, Mul, 0.8f), new Fx(DayParams.WeatherGroundSpeedMult, Ov, 0.9f), new Fx(DayParams.WeatherSoakSeconds, Ov, 25f),
                new Fx(DayParams.RiverCurrentMult, Mul, 1.8f), new Fx(DayParams.BridgeSafeLoadMult, Mul, 0.7f), new Fx(DayParams.WeatherWindSpeed, Ov, 10f),
                new Fx(DayParams.NoiseEnvMult, Mul, 0.6f));
            Content.DayModifiers.Invalidate();

            var rain = Mat(ArtDir + "/M_Weather_Rain.mat", new Color(0.8f, 0.86f, 0.95f, 0.45f));
            var leaf = Mat(ArtDir + "/M_Weather_Leaf.mat", Color.white);

            var go = new GameObject("WeatherDirector");
            go.AddComponent<NetworkObject>();
            var dir = go.AddComponent<WeatherDirector>();
            var so = new SerializedObject(dir);
            so.FindProperty("rainMaterial").objectReferenceValue = rain;
            so.FindProperty("leafMaterial").objectReferenceValue = leaf;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);

            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(ListPath);
            if (list == null) { list = ScriptableObject.CreateInstance<NetworkPrefabsList>(); AssetDatabase.CreateAsset(list, ListPath); }
            bool has = false;
            foreach (var p in list.PrefabList) if (p.Prefab == prefab) has = true;
            if (!has) list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log("[WeatherAssetBuilder] built weather modifiers, materials, prefab and list");
        }

        static Material Mat(string path, Color c)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Mod(WeatherKind k, DayModifierWeight w, string text, params Fx[] fx)
        {
            string path = $"{ModDir}/Mod_Weather_Day_{k}.asset";
            var m = AssetDatabase.LoadAssetAtPath<DayModifier>(path);
            if (m == null) { m = ScriptableObject.CreateInstance<DayModifier>(); AssetDatabase.CreateAsset(m, path); }
            var so = new SerializedObject(m);
            void S(string n, System.Action<SerializedProperty> set) => set(so.FindProperty($"<{n}>k__BackingField"));
            S("ModifierId", p => p.stringValue = WeatherDeck.ModifierId(k));
            S("Category", p => p.enumValueIndex = (int)DayModifierCategory.Weather);
            S("Weight", p => p.enumValueIndex = (int)w);
            S("StackRule", p => p.enumValueIndex = (int)DayModifierStackRule.ReplaceGroup);
            S("GroupTag", p => p.stringValue = "Group.Weather");
            S("Duration", p => p.enumValueIndex = (int)DayModifierDuration.Day);
            S("CardText", p => p.stringValue = text);
            S("Announced", p => p.boolValue = true);
            var e = so.FindProperty("<Effects>k__BackingField");
            e.arraySize = fx.Length;
            for (int i = 0; i < fx.Length; i++)
            {
                var el = e.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("ParamName").stringValue = fx[i].P;
                el.FindPropertyRelative("Op").enumValueIndex = (int)fx[i].Op;
                el.FindPropertyRelative("Value").floatValue = fx[i].V;
            }
            var tags = so.FindProperty("<GrantTags>k__BackingField");
            bool wet = k == WeatherKind.Rain || k == WeatherKind.Storm;
            tags.arraySize = wet ? 2 : 1;
            tags.GetArrayElementAtIndex(0).stringValue = WeatherDeck.WorldTag(k);
            if (wet) tags.GetArrayElementAtIndex(1).stringValue = "World.Rain";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(m);
        }
    }
}
