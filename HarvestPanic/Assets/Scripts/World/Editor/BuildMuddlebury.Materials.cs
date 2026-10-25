using UnityEditor;
using UnityEngine;

namespace HarvestPanic.World.Editor
{
    public static partial class BuildMuddlebury
    {
        /// <summary>
        /// Region 1 architecture palette (art bible 4.1/4.2): muted warm mid-values, reds and golds kept under ~30%
        /// saturation (barn brick is the one landmark red), textures carry value only so the colour stays in the material.
        /// </summary>
        static class Mats
        {
            public static Material BarnWall, BarnRoof, Trim, WoodDark, WoodMid, WoodLight, Floor, Stone, StoneDark, Cobble,
                Hay, HayEnd, GlassDay, GlassNight, LampDay, LampNight, Iron, Rope, Bronze, ClockFace, Bunting, SignBoard,
                SignBoardDark, SignPlum, Straw, Water, SignText, Canvas, Laundry, Leaves, Rug, DoorWood, Gold;
            public static Material[] Walls, Roofs, Shutters, Awnings, LaundryCols;
            public static Font SignFont;

            static Material M(string name, string hex, Texture2D tex = null, float smooth = 0.1f) => WorldAssets.Mat(name, WorldAssets.Hex(hex), smooth, tex);

            public static void Init()
            {
                var boards = WorldTextures.Boards("BoardsV", 5, true, 11);
                var planks = WorldTextures.Boards("PlanksH", 6, false, 12);
                var shingles = WorldTextures.Shingles("Shingles", 8, 6, 13);
                var plaster = WorldTextures.Plaster("Plaster", 14);
                var stone = WorldTextures.StoneBlocks("StoneBlocks", 6, 3, 15);
                var hay = WorldTextures.Hay("Hay", WorldAssets.Hex("E2C474"), WorldAssets.Hex("C7A452"), WorldAssets.Hex("F3DE9A"), 16);
                var hayEnd = WorldTextures.HayRings("HayRings", WorldAssets.Hex("E6CB7E"), WorldAssets.Hex("BF9C4E"));
                var glassDay = WorldTextures.Window("WindowDay", WorldAssets.Hex("52748C"), WorldAssets.Hex("F1E6CC"), WorldAssets.Hex("B9D2DE"));
                var glassNight = WorldTextures.Window("WindowNight", WorldAssets.Hex("FFC777"), WorldAssets.Hex("5A3E2A"), WorldAssets.Hex("FFE6B0"));
                var clock = WorldTextures.ClockFace("ClockFace", WorldAssets.Hex("F4EAD2"), WorldAssets.Hex("2B2118"), WorldAssets.Hex("6A7E9A"));
                var bunt = WorldTextures.Palette("Bunting", new[] { WorldAssets.Hex("4FA39A"), WorldAssets.Hex("F4E9CF"), WorldAssets.Hex("9DBB7A"), WorldAssets.Hex("A88CC8"), WorldAssets.Hex("6E9BD0"), WorldAssets.Hex("E9C6A8") });
                var cobble = WorldAssets.CobbleTexture("CobbleWall", WorldAssets.Hex("D8CCB4"), WorldAssets.Hex("9A8E7A"));

                BarnWall = M("BarnBoards", "9C4A3A", boards);
                BarnRoof = M("BarnRoofShingle", "5E4E48", shingles);
                Trim = M("Trim", "F3E6CC", planks);
                WoodDark = M("WoodDark", "6B4A30", planks);
                WoodMid = M("WoodMid", "8A6040", planks);
                WoodLight = M("WoodLight", "B48C62", planks);
                Floor = M("FloorBoards", "A47C54", planks);
                DoorWood = M("DoorWood", "7A5236", boards);
                Stone = M("StoneBlocks", "C2B8A4", stone);
                StoneDark = M("StoneDark", "958B7C", stone);
                Cobble = M("CobbleWall", "D0C4AC", cobble);
                Hay = M("HayStraw", "FFFFFF", hay);
                HayEnd = M("HayEnd", "FFFFFF", hayEnd);
                Straw = M("Straw", "EBD9A0", hay);
                GlassDay = M("WindowDay", "FFFFFF", glassDay, 0.45f);
                GlassNight = WorldAssets.Mat("WindowNight", Color.white, 0.2f, glassNight, new Color(1.05f, 0.82f, 0.52f));
                LampDay = M("LampGlass", "F6E7BC", null, 0.4f);
                LampNight = WorldAssets.Mat("LampGlassLit", WorldAssets.Hex("FFE2A6"), 0.2f, null, new Color(2.2f, 1.6f, 0.8f));
                Iron = M("Iron", "3E3A38", null, 0.3f);
                Rope = M("Rope", "C8A870");
                Bronze = M("Bronze", "A8844E", null, 0.45f);
                Gold = M("ClockHands", "2B2118");
                ClockFace = M("ClockFace", "FFFFFF", clock);
                Bunting = M("Bunting", "FFFFFF", bunt);
                SignBoard = M("SignBoard", "E8D4A8", planks);
                SignBoardDark = M("SignBoardDark", "5E4632", planks);
                SignPlum = M("SignPlum", "5B2A6E", planks); // royal plum = Landlord / debt (art bible 4.1, semantic)
                Canvas = M("Canvas", "EFE6D2");
                Leaves = M("HedgeLeaves", "6F9446");
                Rug = M("Rug", "7E6A8E");
                Laundry = M("LaundryWhite", "F4EEE2");
                LaundryCols = new[] { M("LaundryBlue", "9DB8D6"), M("LaundrySage", "B8CC9C"), M("LaundryPeach", "EBC8AE"), M("LaundryLilac", "C9B8DC"), Laundry };

                Walls = new[] { M("WallCream", "EFE2C8", plaster), M("WallSage", "CAD3B0", plaster), M("WallPeach", "E9CCAE", plaster), M("WallSky", "C3D4DA", plaster), M("WallButter", "EDE0B4", plaster), M("WallLilac", "D9D0E0", plaster), M("WallWhite", "F2ECE0", plaster) };
                Roofs = new[] { M("RoofTeal", "5E8F86", shingles), M("RoofPlum", "7A6A8A", shingles), M("RoofSlate", "66788F", shingles), M("RoofClay", "A08070", shingles), M("RoofMoss", "6E8460", shingles), M("RoofCharcoal", "5A5550", shingles) };
                Shutters = new[] { M("ShutterTeal", "4F8A82", boards), M("ShutterSage", "7F9A6E", boards), M("ShutterPlum", "7A6088", boards), M("ShutterBlue", "5F7FA0", boards), M("ShutterForest", "52705A", boards) };
                Awnings = new[]
                {
                    M("AwningTeal", "FFFFFF", WorldTextures.Stripes("AwningTeal", WorldAssets.Hex("4F9A90"), WorldAssets.Hex("F4E9CF"), 4)),
                    M("AwningPlum", "FFFFFF", WorldTextures.Stripes("AwningPlum", WorldAssets.Hex("8A6E9C"), WorldAssets.Hex("F4E9CF"), 4)),
                    M("AwningSage", "FFFFFF", WorldTextures.Stripes("AwningSage", WorldAssets.Hex("8AAA6A"), WorldAssets.Hex("F4E9CF"), 4)),
                    M("AwningBlue", "FFFFFF", WorldTextures.Stripes("AwningBlue", WorldAssets.Hex("6688B0"), WorldAssets.Hex("F4E9CF"), 4)),
                };

                string wm = WorldAssets.MatDir + "/M_World_Water.mat";
                Water = AssetDatabase.LoadAssetAtPath<Material>(wm);
                if (Water == null) { Water = new Material(Shader.Find("HarvestPanic/StylizedWater")); AssetDatabase.CreateAsset(Water, wm); }
                Water.SetColor("_ShallowColor", WorldAssets.Hex("6FC3B8"));
                Water.SetColor("_DeepColor", WorldAssets.Hex("2E7C8C"));
                Water.SetColor("_FoamColor", WorldAssets.Hex("FBF4E2"));
                Water.SetFloat("_FlowSpeed", 2.5f);
                Water.SetFloat("_StripeScale", 0.22f);
                Water.SetFloat("_BankFoam", 0.035f);
                Water.SetFloat("_Alpha", 0.92f);
                EditorUtility.SetDirty(Water);

                SignFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/LilitaOne/LilitaOne-Regular.ttf");
                string st = WorldAssets.MatDir + "/M_World_SignText.mat";
                SignText = AssetDatabase.LoadAssetAtPath<Material>(st);
                if (SignText == null) { SignText = new Material(Shader.Find("HarvestPanic/SignText")); AssetDatabase.CreateAsset(SignText, st); }
                SignText.SetColor("_Color", Color.white);
                EditorUtility.SetDirty(SignText);
            }

            public static Material Wall(int i) => Walls[((i % Walls.Length) + Walls.Length) % Walls.Length];
            public static Material Roof(int i) => Roofs[((i % Roofs.Length) + Roofs.Length) % Roofs.Length];
            public static Material Shutter(int i) => Shutters[((i % Shutters.Length) + Shutters.Length) % Shutters.Length];
            public static Material Awning(int i) => Awnings[((i % Awnings.Length) + Awnings.Length) % Awnings.Length];
        }
    }
}
