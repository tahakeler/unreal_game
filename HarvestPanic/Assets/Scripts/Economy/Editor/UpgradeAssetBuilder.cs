using System.IO;
using HarvestPanic.Core;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.Economy.Editor
{
    /// <summary>
    /// Idempotent generator for the farm-upgrade catalog (farm-upgrades.md §3, Alpha subset implementable through
    /// Day Modifiers). Each upgrade = UpgradeDefinition (Resources/Data/Upgrades) + Run-duration Upgrade modifier
    /// (Resources/Data/DayModifiers/Mod_Upgrade_*). Batchmode:
    /// <c>-executeMethod HarvestPanic.Economy.Editor.UpgradeAssetBuilder.BuildUpgrades</c>
    /// </summary>
    public static class UpgradeAssetBuilder
    {
        const string UpData = "Assets/Resources/Data/Upgrades";
        const string ModData = "Assets/Resources/Data/DayModifiers";

        struct U
        {
            public string Key, Name, Desc, Flavor, Requires; public int Price, Tier; public bool Staple; public DayParamEffect[] Fx;
        }

        static DayParamEffect Fx(string p, DayParamOp op, float v) => new DayParamEffect { ParamName = p, Op = op, Value = v };

        static readonly U[] Catalog =
        {
            new U { Key = "BridgeReinforcement1", Name = "Bridge Reinforcement I", Price = 300, Tier = 1, Staple = true,
                Desc = "Planks break 30% less and hold 20% more weight.", Flavor = "Grandpa nails one more nail in.",
                Fx = new[] { Fx(DayParams.BridgePlankBreakMult, DayParamOp.Mul, 0.7f), Fx(DayParams.BridgeSafeLoadMult, DayParamOp.Mul, 1.2f) } },
            new U { Key = "BridgeReinforcement2", Name = "Bridge Reinforcement II", Price = 600, Tier = 2, Requires = "Upgrade.BridgeReinforcement1",
                Desc = "Planks break 55% less in total and hold even more.", Flavor = "Now with a second nail.",
                Fx = new[] { Fx(DayParams.BridgePlankBreakMult, DayParamOp.Mul, 0.643f), Fx(DayParams.BridgeSafeLoadMult, DayParamOp.Mul, 1.15f) } },
            new U { Key = "PaddedCrates", Name = "Padded Crates", Price = 300, Tier = 1,
                Desc = "Goods take 20% less bruising.", Flavor = "Pillows. For turnips.",
                Fx = new[] { Fx("Goods.BruiseMult", DayParamOp.Mul, 0.8f) } },
            new U { Key = "MarketBell", Name = "Market Bell", Price = 200, Tier = 1,
                Desc = "The buyer accepts goods in the pen twice as fast.", Flavor = "Ding. Sold. Ding. Sold.",
                Fx = new[] { Fx("Market.AcceptSeconds", DayParamOp.Mul, 0.5f) } },
            new U { Key = "StallSign", Name = "Market Stall Sign", Price = 350, Tier = 2,
                Desc = "Market demand +0.05 on everything.", Flavor = "\"FRESH-ISH\" in big letters.",
                Fx = new[] { Fx("Market.DemandMult", DayParamOp.Add, 0.05f) } },
            new U { Key = "RubberBoots", Name = "Rubber Boots", Price = 150, Tier = 1,
                Desc = "Mud slows you about half as much.", Flavor = "Squelch with dignity.",
                Fx = new[] { Fx(DayParams.MudSpeedMult, DayParamOp.Add, 0.2f) } },
            new U { Key = "GrandpasBoots", Name = "Grandpa's Boots", Price = 250, Tier = 1,
                Desc = "+30% stamina and faster recovery for the whole crew.", Flavor = "They still smell like 1974.",
                Fx = new[] { Fx(DayParams.PlayerStaminaMult, DayParamOp.Mul, 1.3f) } },
            new U { Key = "BetterNets", Name = "Better Nets", Price = 200, Tier = 1,
                Desc = "Net reach +30%.", Flavor = "Bigger holes? No, smaller holes.",
                Fx = new[] { Fx(DayParams.ToolsNetReachMult, DayParamOp.Mul, 1.3f) } },
            new U { Key = "LanternPosts", Name = "Lantern Posts", Price = 250, Tier = 2,
                Desc = "Lit path: the dusk market stays open 60 s longer.", Flavor = "The moths are thrilled.",
                Fx = new[] { Fx("Day.DuskSeconds", DayParamOp.Add, 60f) } },
            new U { Key = "WagonTarp", Name = "Wagon Tarp", Price = 150, Tier = 1,
                Desc = "Produce stays fresh 25% longer.", Flavor = "It was a tablecloth. Was.",
                Fx = new[] { Fx("Goods.FreshnessDecayMult", DayParamOp.Mul, 0.75f) } },
            new U { Key = "Insurance", Name = "Insurance Policy", Price = 400, Tier = 2,
                Desc = "Respawn fees cost 25% less.", Flavor = "Fine print: pigs not covered.",
                Fx = new[] { Fx(DayParams.EconomyRespawnFeeMult, DayParamOp.Mul, 0.75f) } },
            new U { Key = "Megaphone", Name = "Megaphone", Price = 150, Tier = 1,
                Desc = "Shouts carry twice as far. Also scares the livestock.", Flavor = "HELLO. CAN. YOU. HEAR. ME.",
                Fx = new[] { Fx(DayParams.NoiseVoiceRadiusMult, DayParamOp.Mul, 2f) } },
            new U { Key = "MorningCoffee", Name = "Morning Coffee", Price = 200, Tier = 2,
                Desc = "The workday lasts 30 s longer.", Flavor = "Grandpa's brew. Viscous.",
                Fx = new[] { Fx("Day.WorkdaySeconds", DayParamOp.Add, 30f) } },
        };

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static SerializedProperty P(SerializedObject so, string auto) => so.FindProperty("<" + auto + ">k__BackingField") ?? so.FindProperty(auto);

        [MenuItem("HarvestPanic/Economy/Build Upgrades")]
        public static void BuildUpgrades()
        {
            Directory.CreateDirectory(UpData);
            Directory.CreateDirectory(ModData);
            foreach (var u in Catalog)
            {
                var mod = LoadOrCreate<DayModifier>($"{ModData}/Mod_Upgrade_{u.Key}.asset");
                var so = new SerializedObject(mod);
                P(so, "ModifierId").stringValue = "Mod.Upgrade." + u.Key;
                P(so, "Category").enumValueIndex = (int)DayModifierCategory.Upgrade;
                P(so, "Weight").enumValueIndex = (int)DayModifierWeight.Minor;
                P(so, "StackRule").enumValueIndex = (int)DayModifierStackRule.Unique;
                P(so, "Duration").enumValueIndex = (int)DayModifierDuration.Run;
                P(so, "CardText").stringValue = "";
                P(so, "Announced").boolValue = false; // owned upgrades show in the shop ledger, not the daily twist card
                var fx = P(so, "Effects"); fx.arraySize = u.Fx.Length;
                for (int i = 0; i < u.Fx.Length; i++)
                {
                    var el = fx.GetArrayElementAtIndex(i);
                    el.FindPropertyRelative("ParamName").stringValue = u.Fx[i].ParamName;
                    el.FindPropertyRelative("Op").enumValueIndex = (int)u.Fx[i].Op;
                    el.FindPropertyRelative("Value").floatValue = u.Fx[i].Value;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mod);

                var def = LoadOrCreate<UpgradeDefinition>($"{UpData}/Upgrade_{u.Key}.asset");
                def.UpgradeId = "Upgrade." + u.Key;
                def.DisplayName = u.Name; def.Description = u.Desc; def.FlavorLine = u.Flavor;
                def.Price = u.Price; def.Tier = u.Tier; def.Staple = u.Staple; def.RequiresId = u.Requires ?? "";
                def.Modifier = mod;
                EditorUtility.SetDirty(def);
            }
            AssetDatabase.SaveAssets();
            Content.DayModifiers.Invalidate();
            UpgradeCatalog.All.Invalidate();
            Debug.Log("[UpgradeAssetBuilder] built " + Catalog.Length + " upgrades");
        }
    }
}
