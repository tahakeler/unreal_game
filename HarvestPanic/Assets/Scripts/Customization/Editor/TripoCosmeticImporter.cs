using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarvestPanic.Customization.Editor
{
    /// <summary>
    /// Turns the owner's Tripo FBXs (design/art/asset-specs/tripo-batch-02-character.md) into catalogue items.
    /// <list type="number">
    /// <item>Drop a model into <c>Assets/ThirdParty/Tripo/&lt;Name&gt;/</c> (FBX + textures; a Tripo <c>.fbm</c> folder works too),
    /// or use <b>HarvestPanic/Cosmetics/Copy + Import Tripo Models From Desktop</b> to pull them from
    /// <c>~/Desktop/HarvestPanic Tripo Models/{Hats,Face,Back,Hair,Heads}</c>.</item>
    /// <item>Select the FBX (or its folder) and run <b>HarvestPanic/Cosmetics/Import Tripo Cosmetic</b> (or Import All).</item>
    /// </list>
    /// The importer works out slot + item from the spec name (<c>SM_Hat_StrawHat</c>) or, for raw Tripo names
    /// (<c>cowboy+hat+3d+model.fbx</c>), from the category folder and keywords. It sets static import settings, builds a
    /// material (HarvestPanic/Cosmetics/TintMask when a <c>*_Mask</c> texture exists, else URP Lit), fits the model to the
    /// slot size and anchor (hat brim on the crown, glasses in front of the eyes, backpack on the back), saves a prefab
    /// under <c>Assets/Art/Cosmetics/Items/Prefabs</c> and writes a <see cref="CosmeticEntry"/> into the catalogue. Spec items
    /// reuse the placeholder's id/key/name/flavour/unlock, so the Tripo Straw Hat simply replaces the primitive one
    /// (saves and the network loadout are unchanged). Re-importing updates the prefab and keeps your entry edits.
    /// Rigged bodies (SK_Farmer_*) and outfits (SK_Outfit_*) are set to Humanoid but not added: they need retargeting onto
    /// the farmer rig first (docs/art/adding-cosmetics.md).
    /// </summary>
    public static class TripoCosmeticImporter
    {
        public const string TripoRoot = "Assets/ThirdParty/Tripo";
        public const string ItemsRoot = "Assets/Art/Cosmetics/Items";
        public const string ShaderName = "HarvestPanic/Cosmetics/TintMask";

        /// <summary>Default source folder of the owner's Tripo downloads.</summary>
        public static string DesktopSource => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", "HarvestPanic Tripo Models");

        /// <summary>Where the model is anchored in its attach space.</summary>
        public enum Anchor { Bottom, Top, Centre, Front }

        /// <summary>What a file turned out to be.</summary>
        public enum Kind { Cosmetic, Rigged, NotCosmetic }

        /// <summary>Result of <see cref="Identify"/>.</summary>
        public struct Identity
        {
            public Kind kind;
            public CosmeticSlot slot;
            public string key, assetName, displayName;
            public float size;
            public Anchor anchor;
            public Vector3 at;
            public AttachPoint attach;
            public string note;
        }

        struct Known
        {
            public CosmeticSlot slot; public string key, spec; public string[] words; public float size; public Anchor anchor; public Vector3 at; public AttachPoint attach;
            public Known(CosmeticSlot s, string k, string spec, string[] w, float size, Anchor a, Vector3 at, AttachPoint attach = AttachPoint.Auto)
            { slot = s; key = k; this.spec = spec; words = w; this.size = size; anchor = a; this.at = at; this.attach = attach; }
        }

        // Sizes (largest bounds dimension, metres in attach space: the farmer's head is ~22 cm wide before the body's
        // chunky-head scale) and anchors match the procedural placeholders they replace.
        static readonly Known[] KnownItems =
        {
            new Known(CosmeticSlot.Hat, "hat.straw", "SM_Hat_StrawHat", new[] { "straw" }, 0.56f, Anchor.Bottom, new Vector3(0f, -0.08f, 0f)),
            new Known(CosmeticSlot.Hat, "hat.trucker", "SM_Hat_TruckerCap", new[] { "trucker", "cap" }, 0.32f, Anchor.Bottom, new Vector3(0f, -0.13f, 0.03f)),
            new Known(CosmeticSlot.Hat, "hat.cowboy", "SM_Hat_CowboyHat", new[] { "cowboy" }, 0.6f, Anchor.Bottom, new Vector3(0f, -0.09f, 0f)),
            new Known(CosmeticSlot.Hat, "hat.cone", "SM_Hat_TrafficCone", new[] { "traffic", "cone" }, 0.62f, Anchor.Bottom, new Vector3(0f, -0.08f, 0f)),
            new Known(CosmeticSlot.Hat, "hat.pumpkin", "SM_Hat_PumpkinHead", new[] { "pumpkin" }, 0.46f, Anchor.Bottom, new Vector3(0f, -0.36f, 0f)),
            new Known(CosmeticSlot.Hat, "hat.bucket", "SM_Hat_BucketHelmet", new[] { "bucket" }, 0.36f, Anchor.Bottom, new Vector3(0f, -0.35f, 0f)),
            new Known(CosmeticSlot.Hat, "hat.chef", "SM_Hat_ChefToque", new[] { "chef", "toque" }, 0.44f, Anchor.Bottom, new Vector3(0f, -0.06f, 0f)),
            new Known(CosmeticSlot.Hat, "hat.flowercrown", "SM_Hat_FlowerCrown", new[] { "flower", "crown", "daisy" }, 0.32f, Anchor.Bottom, new Vector3(0f, -0.09f, 0f)),
            new Known(CosmeticSlot.Hat, "hat.viking", "SM_Hat_VikingHorns", new[] { "viking", "horn" }, 0.52f, Anchor.Bottom, new Vector3(0f, -0.14f, 0f)),
            new Known(CosmeticSlot.Hat, "hat.fedora", "SM_Hat_GrandpasFedora", new[] { "fedora", "grandpa" }, 0.42f, Anchor.Bottom, new Vector3(0f, -0.07f, 0f)),
            new Known(CosmeticSlot.Face, "face.moustache", "SM_Face_Moustache", new[] { "moustache", "mustache" }, 0.2f, Anchor.Centre, new Vector3(0f, -0.085f, 0.01f)),
            new Known(CosmeticSlot.Face, "face.sunglasses", "SM_Face_Sunglasses", new[] { "sunglass", "shades" }, 0.21f, Anchor.Front, new Vector3(0f, 0.005f, 0.035f)),
            new Known(CosmeticSlot.Face, "face.goggles", "SM_Face_Goggles", new[] { "goggle", "welding", "aviator" }, 0.26f, Anchor.Front, new Vector3(0f, 0.0f, 0.04f)),
            new Known(CosmeticSlot.Back, "back.basket", "SM_Back_BackpackBasket", new[] { "basket", "backpack", "wicker" }, 0.45f, Anchor.Front, new Vector3(0f, -0.12f, 0.0f)),
            new Known(CosmeticSlot.Back, "back.cape", "SK_Back_Cape", new[] { "cape", "picnic", "blanket" }, 0.62f, Anchor.Front, new Vector3(0f, -0.28f, 0.0f)),
            new Known(CosmeticSlot.Hair, "hair.mop", "SM_Hair_Mop", new[] { "mop", "bowl" }, 0.3f, Anchor.Top, new Vector3(0f, 0.035f, -0.01f)),
            new Known(CosmeticSlot.Hair, "hair.ponytail", "SM_Hair_Ponytail", new[] { "ponytail", "pony" }, 0.42f, Anchor.Top, new Vector3(0f, 0.03f, -0.03f)),
            new Known(CosmeticSlot.Hair, "hair.beard", "SM_Hair_Beard", new[] { "beard" }, 0.24f, Anchor.Centre, new Vector3(0f, -0.12f, -0.03f), AttachPoint.Face),
            new Known(CosmeticSlot.Head, "head.long", "SM_Head_Long", new[] { "long", "egg" }, 0.36f, Anchor.Centre, new Vector3(0f, -0.03f, -0.115f)),
            new Known(CosmeticSlot.Head, "head.freckles", "SM_Head_Freckles", new[] { "freckle" }, 0.34f, Anchor.Centre, new Vector3(0f, -0.03f, -0.115f)),
            new Known(CosmeticSlot.Head, "head.potatonose", "SM_Head_BigNose", new[] { "bignose", "nose", "potato" }, 0.34f, Anchor.Centre, new Vector3(0f, -0.03f, -0.115f)),
            new Known(CosmeticSlot.Head, "head.round", "SM_Head_Round", new[] { "round", "ball" }, 0.34f, Anchor.Centre, new Vector3(0f, -0.03f, -0.115f)),
        };

        static readonly Dictionary<string, CosmeticSlot> SlotWords = new Dictionary<string, CosmeticSlot>(StringComparer.OrdinalIgnoreCase)
        {
            { "Hat", CosmeticSlot.Hat }, { "Hats", CosmeticSlot.Hat }, { "Face", CosmeticSlot.Face }, { "Back", CosmeticSlot.Back },
            { "Hair", CosmeticSlot.Hair }, { "Head", CosmeticSlot.Head }, { "Heads", CosmeticSlot.Head }, { "Outfit", CosmeticSlot.Outfit }, { "Outfits", CosmeticSlot.Outfit },
        };

        static readonly HashSet<string> Filler = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "3d", "model", "cartoon", "only", "farmer", "the", "a", "an", "of", "worn", "as", "with", "and", "soft", "toy", "stylized", "chunky", "big", "hd", "lowpoly" };

        // ---- Identification ------------------------------------------------------------------------------------------

        /// <summary>
        /// Works out what <paramref name="name"/> (a file stem or folder name) is. <paramref name="category"/> is the source
        /// category folder ("Hats", "Face"...) when known.
        /// </summary>
        public static Identity Identify(string name, string category = null)
        {
            var id = new Identity { kind = Kind.NotCosmetic };
            if (string.IsNullOrEmpty(name)) return id;
            name = Path.GetFileNameWithoutExtension(name);
            var m = Regex.Match(name, @"^(SM|SK)_(Hat|Face|Back|Hair|Head|Outfit|Farmer)_(.+)$", RegexOptions.IgnoreCase);
            string rest;
            CosmeticSlot slot;
            if (m.Success)
            {
                string slotWord = m.Groups[2].Value;
                rest = m.Groups[3].Value;
                if (slotWord.Equals("Farmer", StringComparison.OrdinalIgnoreCase) || slotWord.Equals("Outfit", StringComparison.OrdinalIgnoreCase))
                    return new Identity { kind = Kind.Rigged, assetName = name, note = "rigged body/outfit: needs retargeting onto the farmer rig before it can be an item" };
                slot = SlotWords[slotWord];
            }
            else
            {
                if (category != null && (category.Equals("Bodies", StringComparison.OrdinalIgnoreCase) || category.Equals("Outfits", StringComparison.OrdinalIgnoreCase)))
                    return new Identity { kind = Kind.Rigged, assetName = name, note = "rigged body/outfit: needs retargeting onto the farmer rig before it can be an item" };
                if (category == null || !SlotWords.TryGetValue(category, out slot)) return id;
                rest = name;
            }

            string lower = rest.ToLowerInvariant();
            var known = KnownItems.FirstOrDefault(k => k.spec.Equals(name, StringComparison.OrdinalIgnoreCase));
            // Keywords only for raw Tripo names: a spec name that is not in the table is a new item ("SM_Hat_LanternCap" is not a Trucker Cap).
            if (known.key == null && !m.Success) known = KnownItems.FirstOrDefault(k => k.slot == slot && k.words.Any(w => lower.Contains(w)));
            if (known.key != null)
            {
                return new Identity
                {
                    kind = Kind.Cosmetic, slot = slot, key = known.key, assetName = known.spec,
                    displayName = Spaced(Pascal(Regex.Replace(known.spec, @"^(SM|SK)_[A-Za-z]+_", ""))),
                    size = known.size, anchor = known.anchor, at = known.at, attach = known.attach,
                };
            }
            string pascal = Pascal(rest);
            if (pascal.Length == 0) return id;
            var d = Defaults(slot);
            return new Identity
            {
                kind = Kind.Cosmetic, slot = slot, key = slot.ToString().ToLowerInvariant() + "." + pascal.ToLowerInvariant(),
                assetName = (slot == CosmeticSlot.Back && lower.Contains("cape") ? "SK_" : "SM_") + slot + "_" + pascal,
                displayName = Spaced(pascal), size = d.size, anchor = d.anchor, at = d.at, attach = AttachPoint.Auto,
            };
        }

        static (float size, Anchor anchor, Vector3 at) Defaults(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return (0.42f, Anchor.Bottom, new Vector3(0f, -0.08f, 0f));
                case CosmeticSlot.Face: return (0.2f, Anchor.Front, new Vector3(0f, 0f, 0.035f));
                case CosmeticSlot.Back: return (0.45f, Anchor.Front, new Vector3(0f, -0.12f, 0f));
                case CosmeticSlot.Hair: return (0.32f, Anchor.Top, new Vector3(0f, 0.035f, -0.015f));
                default: return (0.34f, Anchor.Centre, new Vector3(0f, -0.03f, -0.115f));
            }
        }

        static string Pascal(string raw)
        {
            var words = Regex.Split(raw, @"[^A-Za-z0-9]+|(?<=[a-z])(?=[A-Z])").Where(w => w.Length > 0 && !Filler.Contains(w)).Take(3);
            var sb = new StringBuilder();
            foreach (var w in words) sb.Append(char.ToUpperInvariant(w[0])).Append(w.Substring(1).ToLowerInvariant());
            return sb.ToString();
        }

        static string Spaced(string pascal) => Regex.Replace(pascal, @"(?<=[a-z0-9])(?=[A-Z])", " ");

        // ---- Menus ---------------------------------------------------------------------------------------------------

        [MenuItem("HarvestPanic/Cosmetics/Import Tripo Cosmetic")]
        public static void ImportSelected()
        {
            var cat = Catalog();
            if (cat == null) return;
            var paths = new List<string>();
            foreach (var guid in Selection.assetGUIDs)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(p)) paths.AddRange(Directory.GetFiles(p, "*.fbx", SearchOption.TopDirectoryOnly).Select(f => f.Replace('\\', '/')));
                else if (p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) paths.Add(p);
            }
            if (paths.Count == 0) { EditorUtility.DisplayDialog("Import Tripo Cosmetic", "Select an FBX (or its folder) under " + TripoRoot + ".", "OK"); return; }
            var log = new StringBuilder();
            foreach (var p in paths) { Import(p, cat, ItemsRoot, out string report); log.AppendLine(report); }
            Debug.Log("[TripoCosmeticImporter]\n" + log);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Import Tripo Cosmetic", log.ToString(), "OK");
        }

        [MenuItem("HarvestPanic/Cosmetics/Import Tripo Cosmetic", true)]
        static bool ImportSelectedValidate() => Selection.assetGUIDs.Length > 0;

        [MenuItem("HarvestPanic/Cosmetics/Import All Tripo Cosmetics")]
        public static void ImportAll()
        {
            var cat = Catalog();
            if (cat == null) return;
            var log = new StringBuilder();
            int n = 0;
            foreach (var dir in Directory.GetDirectories(TripoRoot))
            {
                string folder = Path.GetFileName(dir);
                if (Identify(folder).kind == Kind.NotCosmetic) continue; // critters and props live here too
                foreach (var fbx in Directory.GetFiles(dir, "*.fbx", SearchOption.TopDirectoryOnly))
                {
                    Import(fbx.Replace('\\', '/'), cat, ItemsRoot, out string report);
                    log.AppendLine(report);
                    n++;
                }
            }
            Debug.Log($"[TripoCosmeticImporter] Import All: {n} model(s)\n{log}");
        }

        /// <summary>Batch entry point: <c>-executeMethod HarvestPanic.Customization.Editor.TripoCosmeticImporter.ImportAllBatch</c>.</summary>
        public static void ImportAllBatch()
        {
            try { ImportAll(); }
            catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        [MenuItem("HarvestPanic/Cosmetics/Copy + Import Tripo Models From Desktop")]
        public static void CopyFromDesktop()
        {
            var copied = CopyFrom(DesktopSource);
            Debug.Log($"[TripoCosmeticImporter] Copied {copied.Count} model(s) from {DesktopSource}\n" + string.Join("\n", copied));
            ImportAll();
        }

        /// <summary>
        /// Copies cosmetic FBXs (and their textures: a sibling <c>.fbm</c> folder or images named after the model) from
        /// <paramref name="srcRoot"/>/{Hats,Face,Back,Hair,Heads} into <c>Assets/ThirdParty/Tripo/&lt;SpecName&gt;/</c>.
        /// Returns the destination FBX paths. Bodies/Outfits are copied too (rigged; set up as Humanoid, not catalogued).
        /// </summary>
        public static List<string> CopyFrom(string srcRoot)
        {
            var copied = new List<string>();
            if (!Directory.Exists(srcRoot)) { Debug.LogWarning("[TripoCosmeticImporter] No folder " + srcRoot); return copied; }
            foreach (var catDir in Directory.GetDirectories(srcRoot))
            {
                string category = Path.GetFileName(catDir);
                bool known = SlotWords.ContainsKey(category) || category.Equals("Bodies", StringComparison.OrdinalIgnoreCase);
                if (!known) continue;
                foreach (var fbx in Directory.GetFiles(catDir, "*.fbx", SearchOption.AllDirectories))
                {
                    string stem = Path.GetFileNameWithoutExtension(fbx);
                    var id = Identify(stem, category);
                    if (id.kind == Kind.NotCosmetic) continue;
                    string name = id.assetName ?? stem;
                    string destDir = TripoRoot + "/" + name;
                    Directory.CreateDirectory(destDir);
                    string dest = destDir + "/" + name + ".fbx";
                    if (!File.Exists(dest) || File.GetLastWriteTimeUtc(fbx) > File.GetLastWriteTimeUtc(dest)) File.Copy(fbx, dest, true);
                    foreach (var img in SourceImages(fbx))
                    {
                        string lowerName = Path.GetFileName(img).ToLowerInvariant();
                        string suffix = lowerName.Contains("mask") ? "_Mask" : lowerName.Contains("normal") ? "_Normal" : "_BaseColor";
                        if (suffix == "_Normal") continue; // flat-shaded art style: albedo only
                        File.Copy(img, destDir + "/" + name + suffix + Path.GetExtension(img).ToLowerInvariant(), true);
                    }
                    copied.Add(dest);
                }
            }
            AssetDatabase.Refresh();
            return copied;
        }

        static IEnumerable<string> SourceImages(string fbx)
        {
            string dir = Path.GetDirectoryName(fbx), stem = Path.GetFileNameWithoutExtension(fbx);
            string fbm = Path.Combine(dir, stem + ".fbm");
            var list = new List<string>();
            if (Directory.Exists(fbm)) list.AddRange(Directory.GetFiles(fbm).Where(IsImage));
            list.AddRange(Directory.GetFiles(dir).Where(f => IsImage(f) && Path.GetFileName(f).StartsWith(stem, StringComparison.OrdinalIgnoreCase)
                                                             && !Path.GetFileNameWithoutExtension(f).Equals(stem, StringComparison.OrdinalIgnoreCase))); // skip the preview PNG
            return list;
        }

        static bool IsImage(string f)
        {
            string e = Path.GetExtension(f).ToLowerInvariant();
            return e == ".png" || e == ".jpg" || e == ".jpeg" || e == ".tga";
        }

        static CosmeticCatalog Catalog()
        {
            var cat = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(FarmerAssetBuilder.CatalogPath);
            if (cat == null) Debug.LogError("[TripoCosmeticImporter] Missing " + FarmerAssetBuilder.CatalogPath + " (run HarvestPanic/Customization/Build All Farmer Assets)");
            return cat;
        }

        // ---- Import --------------------------------------------------------------------------------------------------

        static readonly Dictionary<CosmeticSlot, int> TriBudget = new Dictionary<CosmeticSlot, int>
        { { CosmeticSlot.Hat, 1500 }, { CosmeticSlot.Back, 1500 }, { CosmeticSlot.Face, 600 }, { CosmeticSlot.Hair, 1000 }, { CosmeticSlot.Head, 2500 }, { CosmeticSlot.Outfit, 3000 } };

        /// <summary>
        /// Imports one FBX into <paramref name="catalog"/>; assets go under <paramref name="itemsRoot"/> (Prefabs, Materials,
        /// &lt;Slot&gt;). Returns the entry (null when the file is not a cosmetic) and a one-line report.
        /// </summary>
        public static CosmeticEntry Import(string fbxPath, CosmeticCatalog catalog, string itemsRoot, out string report)
        {
            string folder = Path.GetFileName(Path.GetDirectoryName(fbxPath));
            var id = Identify(Path.GetFileNameWithoutExtension(fbxPath));
            if (id.kind == Kind.NotCosmetic) id = Identify(folder);
            if (id.kind == Kind.Rigged)
            {
                SetupRigged(fbxPath);
                report = $"{fbxPath}: {id.note}; imported as Humanoid, not added to the catalogue.";
                return null;
            }
            if (id.kind != Kind.Cosmetic) { report = $"{fbxPath}: not a cosmetic name (use SM_Hat_*, SM_Face_*, SM_Back_*, SM_Hair_*, SM_Head_*)."; return null; }

            string stem = Path.GetFileNameWithoutExtension(fbxPath);
            string assetName = id.assetName ?? stem;
            string dir = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            ConfigureModel(fbxPath);
            var (bc, mask) = FindTextures(dir, stem);
            ConfigureTexture(bc, true, id.slot == CosmeticSlot.Head ? 1024 : 512);
            ConfigureTexture(mask, false, id.slot == CosmeticSlot.Head ? 1024 : 512);
            var mat = BuildMaterial(itemsRoot, assetName, bc, mask);
            RemapMaterials(fbxPath, mat);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) { report = fbxPath + ": failed to load the model"; return null; }
            var (prefab, tris, size) = BuildPrefab(itemsRoot, assetName, model, mat, id);

            var builtIn = catalog.BuiltIn(id.slot).Find(i => i.key == id.key);
            var entry = WriteEntry(catalog, itemsRoot, id, builtIn, prefab, mask != null);
            string budget = TriBudget.TryGetValue(id.slot, out int b) && tris > b ? $" OVER BUDGET ({tris} > {b} tris: run Smart LowPoly / tools/tripo)" : $" {tris} tris";
            report = $"{fbxPath} -> {id.slot} '{entry.item.displayName}' ({entry.item.key}, id {entry.item.id}){(builtIn != null ? " replaces the placeholder" : " new item")}; " +
                     $"{(mask != null ? "tint mask" : bc != null ? "albedo only" : "no texture found")};{budget}; fitted {size.x:0.00}x{size.y:0.00}x{size.z:0.00} m";
            return entry;
        }

        static void ConfigureModel(string fbxPath)
        {
            if (!(AssetImporter.GetAtPath(fbxPath) is ModelImporter imp)) return;
            bool dirty = false;
            void Set<T>(T current, T want, Action<T> apply) { if (!EqualityComparer<T>.Default.Equals(current, want)) { apply(want); dirty = true; } }
            Set(imp.animationType, ModelImporterAnimationType.None, v => imp.animationType = v);
            Set(imp.importAnimation, false, v => imp.importAnimation = v);
            Set(imp.importBlendShapes, false, v => imp.importBlendShapes = v);
            Set(imp.importCameras, false, v => imp.importCameras = v);
            Set(imp.importLights, false, v => imp.importLights = v);
            Set(imp.addCollider, false, v => imp.addCollider = v);
            Set(imp.globalScale, 1f, v => imp.globalScale = v);
            Set(imp.materialImportMode, ModelImporterMaterialImportMode.ImportViaMaterialDescription, v => imp.materialImportMode = v);
            if (dirty) imp.SaveAndReimport();
        }

        static void SetupRigged(string fbxPath)
        {
            if (!(AssetImporter.GetAtPath(fbxPath) is ModelImporter imp)) return;
            if (imp.animationType == ModelImporterAnimationType.Human) return;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importCameras = false; imp.importLights = false;
            imp.SaveAndReimport();
        }

        /// <summary>Albedo and mask textures for a model: <c>&lt;Name&gt;_BaseColor</c>/<c>T_*_BC</c>/any image, and <c>*mask*</c>; also searches a Tripo .fbm folder.</summary>
        public static (Texture2D bc, Texture2D mask) FindTextures(string dir, string assetName)
        {
            var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Where(IsImage).Select(f => f.Replace('\\', '/')).ToList();
            string preview = dir + "/" + assetName + ".png"; // Tripo preview render, not a texture
            files.Remove(preview);
            string maskPath = files.FirstOrDefault(f => Path.GetFileName(f).ToLowerInvariant().Contains("mask"));
            string bcPath = files.FirstOrDefault(f => { var n = Path.GetFileName(f).ToLowerInvariant(); return n.Contains("basecolor") || n.Contains("base_color") || n.Contains("_bc") || n.Contains("albedo") || n.Contains("diffuse"); })
                            ?? files.FirstOrDefault(f => f != maskPath && !Path.GetFileName(f).ToLowerInvariant().Contains("normal"));
            return (bcPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(bcPath) : null, maskPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath) : null);
        }

        static void ConfigureTexture(Texture2D tex, bool srgb, int maxSize)
        {
            if (tex == null || !(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) is TextureImporter ti)) return;
            bool d = false;
            if (ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; d = true; }
            if (ti.maxTextureSize != maxSize) { ti.maxTextureSize = maxSize; d = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; d = true; }
            if (!srgb && ti.alphaSource != TextureImporterAlphaSource.FromInput) { ti.alphaSource = TextureImporterAlphaSource.FromInput; d = true; }
            if (!srgb && ti.alphaIsTransparency) { ti.alphaIsTransparency = false; d = true; }
            if (d) ti.SaveAndReimport();
        }

        static Material BuildMaterial(string itemsRoot, string assetName, Texture2D bc, Texture2D mask)
        {
            EnsureFolder(itemsRoot + "/Materials");
            string path = itemsRoot + "/Materials/MI_" + assetName + ".mat";
            var shader = mask != null ? Shader.Find(ShaderName) : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            if (bc != null) mat.SetTexture("_BaseMap", bc);
            if (mask != null && mat.HasProperty("_MaskMap")) mat.SetTexture("_MaskMap", mask);
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_SpecularHighlights")) { mat.SetFloat("_SpecularHighlights", 0f); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void RemapMaterials(string fbxPath, Material mat)
        {
            if (!(AssetImporter.GetAtPath(fbxPath) is ModelImporter imp)) return;
            bool dirty = false;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (!(o is Material m) || m == mat) continue;
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), mat);
                dirty = true;
            }
            if (dirty) imp.SaveAndReimport();
        }

        static (GameObject prefab, int tris, Vector3 size) BuildPrefab(string itemsRoot, string assetName, GameObject model, Material mat, Identity id)
        {
            EnsureFolder(itemsRoot + "/Prefabs");
            var root = new GameObject(assetName);
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Model";
                inst.transform.SetParent(root.transform, false);
                int tris = 0;
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh != null) tris += (int)(mesh.triangles.LongLength / 3);
                }
                foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                var b = Bounds(root);
                float max = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (max > 1e-5f) inst.transform.localScale *= id.size / max;
                b = Bounds(root);
                Vector3 anchor;
                switch (id.anchor)
                {
                    case Anchor.Bottom: anchor = new Vector3(b.center.x, b.min.y, b.center.z); break;
                    case Anchor.Top: anchor = new Vector3(b.center.x, b.max.y, b.center.z); break;
                    case Anchor.Front: anchor = new Vector3(b.center.x, b.center.y, b.max.z); break;
                    default: anchor = b.center; break;
                }
                inst.transform.localPosition += id.at - anchor;
                var size = Bounds(root).size;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, itemsRoot + "/Prefabs/" + assetName + ".prefab");
                return (prefab, tris, size);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Bounds Bounds(GameObject root)
        {
            var rs = root.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static CosmeticEntry WriteEntry(CosmeticCatalog catalog, string itemsRoot, Identity id, CosmeticItem builtIn, GameObject prefab, bool hasMask)
        {
            string dir = itemsRoot + "/" + id.slot;
            EnsureFolder(dir);
            string path = dir + "/" + id.key + ".asset";
            var entry = AssetDatabase.LoadAssetAtPath<CosmeticEntry>(path);
            bool fresh = entry == null;
            if (fresh)
            {
                entry = ScriptableObject.CreateInstance<CosmeticEntry>();
                entry.slot = id.slot;
                var it = new CosmeticItem { key = id.key };
                if (builtIn != null)
                {
                    it.id = builtIn.id; it.displayName = builtIn.displayName; it.flavour = builtIn.flavour; it.unlock = builtIn.unlock;
                    it.unlockHint = builtIn.unlockHint; it.blocksFirstPersonView = builtIn.blocksFirstPersonView; it.hidesHair = builtIn.hidesHair;
                    it.randomWeight = builtIn.randomWeight; it.hideBaseHair = builtIn.hideBaseHair; it.outfit = builtIn.outfit;
                }
                else
                {
                    it.id = NextFreeId(catalog, id.slot);
                    it.displayName = id.displayName ?? id.key;
                    it.flavour = "Fresh from Grandpa's attic.";
                    it.unlock = UnlockSource.Starter;
                }
                entry.item = it;
                AssetDatabase.CreateAsset(entry, path);
            }
            var item = entry.item;
            item.prefab = prefab;
            item.parts = new List<CosmeticPart>();
            item.attach = id.attach;
            item.headShape = Vector3.one;
            if (hasMask) { item.tintMode = TintMode.MaskTexture; item.maskRed = id.slot == CosmeticSlot.Hair ? PartTint.Hair : PartTint.Primary; }
            else if (id.slot == CosmeticSlot.Hair) { item.tintMode = TintMode.PerMaterial; item.materialTints = new List<PartTint> { PartTint.Hair }; } // flat grey hair, tinted whole
            else item.tintMode = TintMode.None; // hats and props are authored in real colours at VS
            entry.name = id.key;
            EditorUtility.SetDirty(entry);
            if (!catalog.entries.Contains(entry)) catalog.entries.Add(entry);
            catalog.entries.RemoveAll(e => e == null);
            catalog.MarkItemsDirty();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return entry;
        }

        /// <summary>Next unused id in <paramref name="slot"/> (built-ins and entries).</summary>
        public static ushort NextFreeId(CosmeticCatalog catalog, CosmeticSlot slot)
        {
            int max = 0;
            foreach (var it in catalog.BuiltIn(slot)) max = Mathf.Max(max, it.id);
            foreach (var e in catalog.entries) if (e != null && e.slot == slot && e.item != null) max = Mathf.Max(max, e.item.id);
            int next = max + 1;
            if (next > FarmerLoadout.MaxId(slot)) throw new InvalidOperationException(slot + ": no ids left on the wire");
            return (ushort)next;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
