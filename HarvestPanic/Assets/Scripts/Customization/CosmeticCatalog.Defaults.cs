using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Default VS content: 24-swatch palette, 12+2 skin tones, 8 name tags, 2 bodies, 10 hats, 4 face, 3 back items, 4 emotes.
    /// Sources: design/gdd/character-customization.md and design/art/character-style.md (names + flavour text).
    /// Attach spaces (metres): Hat = crown top centre; Face = face front at eye level; Back = back surface at chest height.
    /// In every space +Y is up and +Z is the farmer's forward.
    /// </summary>
    public partial class CosmeticCatalog
    {
        /// <summary>Hat id of the starter Straw Hat.</summary>
        public const ushort StrawHatId = 1;

        /// <summary>Rebuilds every list with the shipped VS defaults (editor builder and tests).</summary>
        public void ResetToDefaults()
        {
            unlockAllCosmetics = true;
            palette = DefaultPalette();
            skinTones = DefaultSkins();
            nameTags = DefaultNameTags();
            bodies = new List<BodyVariant>
            {
                new BodyVariant { name = "Sturdy", flavour = "Built like a hay bale. Falls like one too.", bodyScale = new Vector3(1.12f, 0.96f, 1.12f), headScale = 1.18f },
                new BodyVariant { name = "Lanky", flavour = "Can reach the top shelf. Cannot reach a decision.", bodyScale = new Vector3(0.9f, 1.08f, 0.9f), headScale = 1.1f },
            };
            hats = DefaultHats();
            faces = DefaultFaces();
            backs = DefaultBacks();
            emotes = DefaultEmotes();
        }

        static List<Swatch> DefaultPalette() => new List<Swatch>
        {
            Swatch.Hex("Denim Blue", "#3B6FD1"), Swatch.Hex("Pumpkin Orange", "#F08A24"), Swatch.Hex("Clover Green", "#3FAE5A"), Swatch.Hex("Lilac", "#A77BE0"),
            Swatch.Hex("Barn Brick", "#9C4A3A"), Swatch.Hex("Mustard", "#D9B13B"), Swatch.Hex("Pond Teal", "#2E9C9C"), Swatch.Hex("Sky", "#7EC8E8"),
            Swatch.Hex("Bubblegum", "#F28DB2"), Swatch.Hex("Landlord Plum", "#5B2A6E"), Swatch.Hex("Moss", "#5E7F3A"), Swatch.Hex("Rust", "#B5562B"),
            Swatch.Hex("Navy", "#24345E"), Swatch.Hex("Charcoal", "#3A3A40"), Swatch.Hex("Cream", "#F2E6C8"), Swatch.Hex("Snow", "#F7F7F2"),
            Swatch.Hex("Sand", "#CDB38A"), Swatch.Hex("Mud Brown", "#6B4A2E"), Swatch.Hex("Wheat", "#E3C27A"), Swatch.Hex("Mint", "#9FE0C0"),
            Swatch.Hex("Lemon", "#F5E663"), Swatch.Hex("Coral", "#F2785C"), Swatch.Hex("Slate", "#6E7F94"), Swatch.Hex("Hot Pink", "#E04FA0"),
        };

        static List<Swatch> DefaultSkins() => new List<Swatch>
        {
            Swatch.Hex("Buttermilk", "#F6D7C3"), Swatch.Hex("Rosy Apple", "#F0C1B0"), Swatch.Hex("Peach Cobbler", "#EBC0A0"), Swatch.Hex("Honeycomb", "#D9A27C"),
            Swatch.Hex("Almond", "#C9956B"), Swatch.Hex("Maple", "#C08560"), Swatch.Hex("Toffee", "#B07850"), Swatch.Hex("Pecan", "#9E6A47"),
            Swatch.Hex("Cinnamon", "#8C5A3C"), Swatch.Hex("Cocoa", "#7A4E33"), Swatch.Hex("Coffee Bean", "#5A3725"), Swatch.Hex("Black Walnut", "#3E261A"),
            Swatch.Hex("Pickled", "#9FD39A", true), Swatch.Hex("Moonstruck", "#8FB4F0", true),
        };

        static List<NameTagStyle> DefaultNameTags()
        {
            NameTagStyle T(string n, string hex, TagShape s) { ColorUtility.TryParseHtmlString(hex, out var c); return new NameTagStyle { name = n, color = c, shape = s }; }
            return new List<NameTagStyle>
            {
                T("Denim Blue", "#3B6FD1", TagShape.Circle), T("Pumpkin Orange", "#F08A24", TagShape.Square),
                T("Clover Green", "#3FAE5A", TagShape.Triangle), T("Lilac", "#A77BE0", TagShape.Diamond),
                T("Sunflower", "#F5D142", TagShape.Star), T("Teal", "#2EC4C4", TagShape.Hexagon),
                T("Pink", "#F27BB5", TagShape.Heart), T("Snow", "#F2F2F2", TagShape.Cross),
            };
        }

        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        static CosmeticPart P(PartShape s, Vector3 pos, Vector3 size, Color col, Vector3 euler = default, PartTint tint = PartTint.Fixed, float taper = 0f) =>
            new CosmeticPart { shape = s, position = pos, size = size, color = col, euler = euler, tint = tint, taper = taper };

        static CosmeticItem I(ushort id, string key, string name, string flavour, UnlockSource unlock, params CosmeticPart[] parts) =>
            new CosmeticItem { id = id, key = key, displayName = name, flavour = flavour, unlock = unlock, parts = new List<CosmeticPart>(parts) };

        static List<CosmeticItem> DefaultHats()
        {
            Color straw = C("#E8C872"), strawDark = C("#C9A24E"), white = C("#F7F7F2"), black = C("#26221F");
            var list = new List<CosmeticItem>
            {
                I(1, "hat.straw", "Straw Hat", "Smells like Grandpa. Smells like regret.", UnlockSource.Starter,
                    P(PartShape.Cylinder, new Vector3(0, -0.07f, 0), new Vector3(0.56f, 0.025f, 0.56f), straw),
                    P(PartShape.Cylinder, new Vector3(0, -0.01f, 0), new Vector3(0.25f, 0.12f, 0.25f), straw),
                    P(PartShape.Cylinder, new Vector3(0, -0.045f, 0), new Vector3(0.26f, 0.035f, 0.26f), C("#C0443A")),
                    P(PartShape.Sphere, new Vector3(0, 0.045f, 0), new Vector3(0.24f, 0.05f, 0.24f), strawDark)),
                I(2, "hat.trucker", "Trucker Cap", "Free with any purchase of 400 lb of feed.", UnlockSource.Starter,
                    P(PartShape.Sphere, new Vector3(0, -0.04f, 0), new Vector3(0.25f, 0.2f, 0.26f), white, default, PartTint.Primary),
                    P(PartShape.Cube, new Vector3(0, -0.075f, 0.16f), new Vector3(0.21f, 0.022f, 0.15f), default, new Vector3(-8, 0, 0), PartTint.Primary),
                    P(PartShape.Cube, new Vector3(0, -0.035f, 0.11f), new Vector3(0.16f, 0.08f, 0.03f), white, new Vector3(-20, 0, 0)),
                    P(PartShape.Sphere, new Vector3(0, 0.06f, 0), new Vector3(0.04f, 0.04f, 0.04f), white)),
                I(3, "hat.bucket", "Bucket Helmet", "Holds water. Doesn't hold dignity.", UnlockSource.Letter,
                    P(PartShape.Cone, new Vector3(0, -0.2f, 0), new Vector3(0.32f, 0.3f, 0.32f), C("#9AA3AD"), default, PartTint.Fixed, 0.78f),
                    P(PartShape.Cylinder, new Vector3(0, -0.19f, 0), new Vector3(0.335f, 0.025f, 0.335f), C("#7D858F")),
                    P(PartShape.Cube, new Vector3(0, 0.11f, 0), new Vector3(0.3f, 0.02f, 0.02f), C("#5A6068")),
                    P(PartShape.Cube, new Vector3(0, -0.12f, 0.17f), new Vector3(0.12f, 0.03f, 0.01f), black)),
                I(4, "hat.pumpkin", "Pumpkin Head", "The Scarecrow thinks you're cute.", UnlockSource.RegionClear,
                    P(PartShape.Sphere, new Vector3(0, -0.14f, 0), new Vector3(0.46f, 0.4f, 0.44f), C("#F08A24")),
                    P(PartShape.Sphere, new Vector3(0.1f, -0.14f, 0), new Vector3(0.3f, 0.38f, 0.42f), C("#E07A1E")),
                    P(PartShape.Sphere, new Vector3(-0.1f, -0.14f, 0), new Vector3(0.3f, 0.38f, 0.42f), C("#E07A1E")),
                    P(PartShape.Cylinder, new Vector3(0, 0.1f, 0), new Vector3(0.05f, 0.1f, 0.05f), C("#4F7A2A"), new Vector3(0, 0, 12)),
                    P(PartShape.Cone, new Vector3(-0.07f, -0.08f, 0.205f), new Vector3(0.07f, 0.06f, 0.02f), black, new Vector3(90, 0, 0)),
                    P(PartShape.Cone, new Vector3(0.07f, -0.08f, 0.205f), new Vector3(0.07f, 0.06f, 0.02f), black, new Vector3(90, 0, 0)),
                    P(PartShape.Cube, new Vector3(0, -0.2f, 0.2f), new Vector3(0.17f, 0.035f, 0.02f), black, new Vector3(0, 0, 0))),
                I(5, "hat.cowboy", "Cowboy Hat", "The bull has opinions.", UnlockSource.Starter,
                    P(PartShape.Sphere, new Vector3(0, -0.07f, 0), new Vector3(0.6f, 0.05f, 0.48f), C("#8A5A34")),
                    P(PartShape.Cylinder, new Vector3(0, 0.0f, 0), new Vector3(0.24f, 0.14f, 0.26f), C("#8A5A34")),
                    P(PartShape.Cube, new Vector3(0, 0.07f, 0), new Vector3(0.05f, 0.03f, 0.2f), C("#6E4527")),
                    P(PartShape.Cylinder, new Vector3(0, -0.045f, 0), new Vector3(0.25f, 0.03f, 0.27f), black)),
                I(6, "hat.chef", "Chef Toque", "Yes, chef. No, chef. Where's the pig, chef?", UnlockSource.Trunk,
                    P(PartShape.Cylinder, new Vector3(0, 0.02f, 0), new Vector3(0.24f, 0.16f, 0.24f), white),
                    P(PartShape.Sphere, new Vector3(0, 0.2f, 0), new Vector3(0.36f, 0.28f, 0.36f), white),
                    P(PartShape.Sphere, new Vector3(0.09f, 0.25f, 0.04f), new Vector3(0.2f, 0.2f, 0.2f), white),
                    P(PartShape.Sphere, new Vector3(-0.08f, 0.27f, -0.05f), new Vector3(0.2f, 0.2f, 0.2f), white)),
                FlowerCrown(),
                I(8, "hat.cone", "Traffic Cone", "Road works ahead. The road is you.", UnlockSource.Letter,
                    P(PartShape.Cone, new Vector3(0, -0.06f, 0), new Vector3(0.24f, 0.62f, 0.24f), C("#FF6A13"), default, PartTint.Fixed, 0.12f),
                    P(PartShape.Cone, new Vector3(0, 0.12f, 0), new Vector3(0.175f, 0.09f, 0.175f), white, default, PartTint.Fixed, 0.8f),
                    P(PartShape.Cone, new Vector3(0, 0.3f, 0), new Vector3(0.12f, 0.08f, 0.12f), white, default, PartTint.Fixed, 0.75f),
                    P(PartShape.Cube, new Vector3(0, -0.07f, 0), new Vector3(0.34f, 0.03f, 0.34f), C("#E85A0C"))),
                I(9, "hat.viking", "Viking Horns", "Raids barns. Only his own.", UnlockSource.Trunk,
                    P(PartShape.Sphere, new Vector3(0, -0.07f, 0), new Vector3(0.26f, 0.22f, 0.27f), C("#8E969E")),
                    P(PartShape.Cylinder, new Vector3(0, -0.1f, 0), new Vector3(0.27f, 0.04f, 0.28f), C("#B08A3A")),
                    P(PartShape.Cube, new Vector3(0, -0.05f, 0.12f), new Vector3(0.035f, 0.14f, 0.04f), C("#B08A3A")),
                    P(PartShape.Cone, new Vector3(0.12f, -0.03f, 0), new Vector3(0.08f, 0.26f, 0.08f), C("#F2E6C8"), new Vector3(0, 0, -62)),
                    P(PartShape.Cone, new Vector3(-0.12f, -0.03f, 0), new Vector3(0.08f, 0.26f, 0.08f), C("#F2E6C8"), new Vector3(0, 0, 62))),
                I(10, "hat.fedora", "Grandpa's Fedora", "He wore it to every bank meeting. It never helped.", UnlockSource.Letter,
                    P(PartShape.Cylinder, new Vector3(0, -0.06f, 0), new Vector3(0.42f, 0.02f, 0.38f), C("#4A4038")),
                    P(PartShape.Cylinder, new Vector3(0, 0.0f, 0), new Vector3(0.23f, 0.12f, 0.22f), C("#4A4038")),
                    P(PartShape.Cube, new Vector3(0, 0.06f, 0), new Vector3(0.06f, 0.025f, 0.16f), C("#3A322C")),
                    P(PartShape.Cylinder, new Vector3(0, -0.035f, 0), new Vector3(0.235f, 0.03f, 0.225f), black),
                    P(PartShape.Capsule, new Vector3(0.12f, 0.02f, -0.02f), new Vector3(0.025f, 0.14f, 0.02f), C("#F5D142"), new Vector3(0, 0, -35))),
            };
            list[3].blocksFirstPersonView = true; // Pumpkin Head
            list[2].blocksFirstPersonView = true; // Bucket Helmet
            return list;
        }

        static CosmeticItem FlowerCrown()
        {
            var item = I(7, "hat.flowercrown", "Flower Crown", "Picked from the cursed patch. Probably fine.", UnlockSource.Trunk);
            Color[] petals = { C("#F28DB2"), C("#F5E663"), C("#F7F7F2"), C("#A77BE0") };
            const int n = 9;
            item.parts.Add(P(PartShape.Cylinder, new Vector3(0, -0.07f, 0), new Vector3(0.25f, 0.02f, 0.26f), C("#4F8A3A")));
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var pos = new Vector3(Mathf.Sin(a) * 0.125f, -0.06f, Mathf.Cos(a) * 0.13f);
                item.parts.Add(P(PartShape.Sphere, pos, new Vector3(0.075f, 0.06f, 0.075f), petals[i % petals.Length]));
                item.parts.Add(P(PartShape.Sphere, pos + new Vector3(0, 0.025f, 0), new Vector3(0.03f, 0.03f, 0.03f), C("#F5C542")));
            }
            return item;
        }

        static List<CosmeticItem> DefaultFaces()
        {
            Color brown = C("#5A3725"), black = C("#1E1B19");
            return new List<CosmeticItem>
            {
                I(1, "face.moustache", "Grandpa Moustache", "Inherited. Like the debt.", UnlockSource.Trunk,
                    P(PartShape.Sphere, new Vector3(0.045f, -0.085f, 0.0f), new Vector3(0.1f, 0.04f, 0.05f), brown, new Vector3(0, 0, 18)),
                    P(PartShape.Sphere, new Vector3(-0.045f, -0.085f, 0.0f), new Vector3(0.1f, 0.04f, 0.05f), brown, new Vector3(0, 0, -18)),
                    P(PartShape.Sphere, new Vector3(0.1f, -0.07f, -0.01f), new Vector3(0.035f, 0.035f, 0.035f), brown),
                    P(PartShape.Sphere, new Vector3(-0.1f, -0.07f, -0.01f), new Vector3(0.035f, 0.035f, 0.035f), brown)),
                I(2, "face.goggles", "Safety Goggles", "Mandatory around llamas.", UnlockSource.Trunk,
                    P(PartShape.Cylinder, new Vector3(0.048f, 0f, 0.012f), new Vector3(0.08f, 0.03f, 0.08f), C("#F5C542"), new Vector3(90, 0, 0)),
                    P(PartShape.Cylinder, new Vector3(-0.048f, 0f, 0.012f), new Vector3(0.08f, 0.03f, 0.08f), C("#F5C542"), new Vector3(90, 0, 0)),
                    P(PartShape.Cylinder, new Vector3(0.048f, 0f, 0.028f), new Vector3(0.062f, 0.005f, 0.062f), C("#7EC8E8"), new Vector3(90, 0, 0)),
                    P(PartShape.Cylinder, new Vector3(-0.048f, 0f, 0.028f), new Vector3(0.062f, 0.005f, 0.062f), C("#7EC8E8"), new Vector3(90, 0, 0)),
                    P(PartShape.Cylinder, new Vector3(0f, 0f, -0.1f), new Vector3(0.235f, 0.025f, 0.235f), C("#3A3A40"))),
                I(3, "face.sunglasses", "Sunglasses", "Too cool for the coop.", UnlockSource.Trunk,
                    P(PartShape.Cube, new Vector3(0.045f, 0.0f, 0.012f), new Vector3(0.075f, 0.045f, 0.015f), black),
                    P(PartShape.Cube, new Vector3(-0.045f, 0.0f, 0.012f), new Vector3(0.075f, 0.045f, 0.015f), black),
                    P(PartShape.Cube, new Vector3(0f, 0.012f, 0.012f), new Vector3(0.03f, 0.01f, 0.012f), black),
                    P(PartShape.Cube, new Vector3(0.088f, 0.01f, -0.04f), new Vector3(0.008f, 0.01f, 0.1f), black),
                    P(PartShape.Cube, new Vector3(-0.088f, 0.01f, -0.04f), new Vector3(0.008f, 0.01f, 0.1f), black)),
                I(4, "face.clownnose", "Clown Nose", "Honk. The chickens respect you less now.", UnlockSource.Trunk,
                    P(PartShape.Sphere, new Vector3(0f, -0.04f, 0.03f), new Vector3(0.065f, 0.065f, 0.065f), C("#E5483B"))),
            };
        }

        static List<CosmeticItem> DefaultBacks()
        {
            Color wicker = C("#B88A4A"), wood = C("#8A6040"), metal = C("#9AA3AD");
            return new List<CosmeticItem>
            {
                I(1, "back.basket", "Harvest Basket Backpack", "Holds nothing. The physics team was very clear.", UnlockSource.Trunk,
                    P(PartShape.Cone, new Vector3(0, -0.2f, -0.15f), new Vector3(0.32f, 0.36f, 0.24f), wicker, default, PartTint.Fixed, 1.25f),
                    P(PartShape.Cylinder, new Vector3(0, 0.16f, -0.15f), new Vector3(0.4f, 0.03f, 0.3f), C("#9C7038")),
                    P(PartShape.Cone, new Vector3(0.06f, 0.15f, -0.15f), new Vector3(0.06f, 0.2f, 0.06f), C("#F08A24"), new Vector3(0, 0, -12)),
                    P(PartShape.Sphere, new Vector3(-0.07f, 0.2f, -0.13f), new Vector3(0.11f, 0.11f, 0.11f), C("#3FAE5A")),
                    P(PartShape.Sphere, new Vector3(0.0f, 0.19f, -0.2f), new Vector3(0.1f, 0.1f, 0.1f), C("#B3A3E6")),
                    P(PartShape.Cube, new Vector3(0.1f, 0.0f, 0.0f), new Vector3(0.035f, 0.42f, 0.02f), C("#6B4A2E")),
                    P(PartShape.Cube, new Vector3(-0.1f, 0.0f, 0.0f), new Vector3(0.035f, 0.42f, 0.02f), C("#6B4A2E"))),
                I(2, "back.cape", "Cape", "Every hero needs one. You are not a hero.", UnlockSource.Trunk,
                    P(PartShape.Cube, new Vector3(0, -0.28f, -0.07f), new Vector3(0.44f, 0.62f, 0.025f), default, new Vector3(-8, 0, 0), PartTint.Secondary),
                    P(PartShape.Cylinder, new Vector3(0, 0.03f, -0.02f), new Vector3(0.32f, 0.04f, 0.12f), C("#F5C542"))),
                I(3, "back.pitchfork", "Pitchfork Holster", "For decoration. Allegedly.", UnlockSource.Trunk,
                    P(PartShape.Cylinder, new Vector3(0, -0.05f, -0.09f), new Vector3(0.035f, 1.1f, 0.035f), wood, new Vector3(0, 0, 30)),
                    P(PartShape.Cube, new Vector3(-0.27f, 0.42f, -0.09f), new Vector3(0.2f, 0.025f, 0.025f), metal, new Vector3(0, 0, 30)),
                    P(PartShape.Cone, new Vector3(-0.27f, 0.55f, -0.09f), new Vector3(0.025f, 0.24f, 0.025f), metal, new Vector3(0, 0, 30)),
                    P(PartShape.Cone, new Vector3(-0.19f, 0.5f, -0.09f), new Vector3(0.025f, 0.24f, 0.025f), metal, new Vector3(0, 0, 30)),
                    P(PartShape.Cone, new Vector3(-0.35f, 0.6f, -0.09f), new Vector3(0.025f, 0.24f, 0.025f), metal, new Vector3(0, 0, 30)),
                    P(PartShape.Cube, new Vector3(0, 0.0f, -0.03f), new Vector3(0.36f, 0.035f, 0.02f), C("#6B4A2E"), new Vector3(0, 0, 30))),
            };
        }

        static List<EmoteDefinition> DefaultEmotes() => new List<EmoteDefinition>
        {
            new EmoteDefinition { id = 1, key = "wave", displayName = "Wave", animState = "Wave", duration = 1.5f, cooldown = 1.5f, noiseLoudness = 0.6f, unlock = UnlockSource.Starter },
            new EmoteDefinition { id = 2, key = "point", displayName = "Point", animState = "Point", duration = 1.2f, cooldown = 1.2f, placesPing = true, unlock = UnlockSource.Starter },
            new EmoteDefinition { id = 3, key = "dance", displayName = "Dance", animState = "Dance", duration = 0f, cooldown = 1f, loopsUntilMove = true, unlock = UnlockSource.Starter },
            new EmoteDefinition { id = 4, key = "faint", displayName = "Faint", animState = "Faint", kind = EmoteKind.Faint, duration = 2f, cooldown = 5f, unlock = UnlockSource.Starter },
        };
    }
}
