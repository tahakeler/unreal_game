using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Head, hair and outfit placeholders (procedural until Tripo batch 02 lands: SM_Head_*, SM_Hair_*, SK_Outfit_*).
    /// Id 0 of these slots is the base farmer look, so v1 saves (which had no such fields) keep their look.
    /// </summary>
    public partial class CosmeticCatalog
    {
        /// <summary>Card name of id 0 in a slot (the base look or None).</summary>
        public static string BaseLookName(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return "Bald & Breezy";
                case CosmeticSlot.Face: return "Bare Face";
                case CosmeticSlot.Back: return "Nothing";
                case CosmeticSlot.Head: return "Classic Noggin";
                case CosmeticSlot.Hair: return "Short Crop";
                default: return "Plain Overalls";
            }
        }

        /// <summary>Flavour of id 0 in a slot.</summary>
        public static string BaseLookFlavour(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return "The breeze is free. So is the sunburn.";
                case CosmeticSlot.Face: return "Choose your expression for the inevitable.";
                case CosmeticSlot.Back: return "Travelling light. Suspiciously light.";
                case CosmeticSlot.Head: return "Round, reliable, mostly empty.";
                case CosmeticSlot.Hair: return "Grandpa's barber. Grandpa's rules.";
                default: return "Factory default. Smells like the store.";
            }
        }

        static CosmeticItem Look(ushort id, string key, string name, string flavour, UnlockSource unlock, string hint, float weight, params CosmeticPart[] parts)
        {
            var it = I(id, key, name, flavour, unlock, parts);
            it.unlockHint = hint;
            it.randomWeight = weight;
            return it;
        }

        static List<CosmeticItem> DefaultHeads()
        {
            Color freckle = C("#8C5A3C"), blush = C("#F2857F");
            var longFace = Look(1, "head.long", "Long Face", "Tall, long, worried. Mostly worried.", UnlockSource.Starter, "", 1f);
            longFace.headShape = new Vector3(0.93f, 1.13f, 0.97f);
            var square = Look(6, "head.square", "Square Jaw", "Chiselled by hard work. And a hay bale.", UnlockSource.Starter, "", 1f);
            square.headShape = new Vector3(1.1f, 0.97f, 1.04f);
            var list = new List<CosmeticItem>
            {
                longFace,
                Look(2, "head.potatonose", "Potato Nose", "Smells the harvest before it's ripe.", UnlockSource.Starter, "", 1.2f,
                    P(PartShape.Sphere, new Vector3(0f, -0.04f, 0.012f), new Vector3(0.075f, 0.065f, 0.07f), default, default, PartTint.SkinShade)),
                Look(3, "head.freckles", "Freckles", "Sun-kissed. Sun-bullied, really.", UnlockSource.Starter, "", 1f,
                    P(PartShape.Sphere, new Vector3(0.05f, -0.045f, -0.012f), Vector3.one * 0.014f, freckle),
                    P(PartShape.Sphere, new Vector3(-0.05f, -0.045f, -0.012f), Vector3.one * 0.014f, freckle),
                    P(PartShape.Sphere, new Vector3(0.068f, -0.034f, -0.02f), Vector3.one * 0.012f, freckle),
                    P(PartShape.Sphere, new Vector3(-0.068f, -0.034f, -0.02f), Vector3.one * 0.012f, freckle),
                    P(PartShape.Sphere, new Vector3(0.062f, -0.058f, -0.018f), Vector3.one * 0.012f, freckle),
                    P(PartShape.Sphere, new Vector3(-0.062f, -0.058f, -0.018f), Vector3.one * 0.012f, freckle),
                    P(PartShape.Sphere, new Vector3(0.012f, -0.03f, 0.008f), Vector3.one * 0.01f, freckle),
                    P(PartShape.Sphere, new Vector3(-0.012f, -0.03f, 0.008f), Vector3.one * 0.01f, freckle)),
                Look(4, "head.rosy", "Rosy Cheeks", "Healthy farm glow. Or a fever. Hard to say.", UnlockSource.Starter, "", 1f,
                    P(PartShape.Sphere, new Vector3(0.064f, -0.05f, -0.022f), new Vector3(0.048f, 0.032f, 0.02f), blush),
                    P(PartShape.Sphere, new Vector3(-0.064f, -0.05f, -0.022f), new Vector3(0.048f, 0.032f, 0.02f), blush)),
                Look(5, "head.bigears", "Big Ears", "Hears the landlord coming. Can't do anything about it.", UnlockSource.Starter, "", 1.2f,
                    P(PartShape.Sphere, new Vector3(0.118f, -0.025f, -0.11f), new Vector3(0.035f, 0.085f, 0.06f), default, new Vector3(0, -15, 0), PartTint.Skin),
                    P(PartShape.Sphere, new Vector3(-0.118f, -0.025f, -0.11f), new Vector3(0.035f, 0.085f, 0.06f), default, new Vector3(0, 15, 0), PartTint.Skin)),
                square,
                Look(7, "head.unibrow", "Unibrow", "One brow. Twice the judgement.", UnlockSource.Trunk, TrunkCommon, 1.2f,
                    P(PartShape.Cube, new Vector3(0f, 0.035f, 0.0f), new Vector3(0.14f, 0.026f, 0.03f), default, new Vector3(-8, 0, 0), PartTint.Hair)),
            };
            return list;
        }

        static List<CosmeticItem> DefaultHairs()
        {
            Color tie = C("#C0443A");
            const PartTint H = PartTint.Hair;
            CosmeticPart Cap(float y = -0.035f) => P(PartShape.Sphere, new Vector3(0f, y, -0.02f), new Vector3(0.25f, 0.135f, 0.285f), default, default, H, 0f, true);
            var mop = Look(1, "hair.mop", "Mop Top", "Cut with a bowl. The bowl was also used for slop.", UnlockSource.Starter, "", 1f,
                P(PartShape.Sphere, new Vector3(0f, -0.03f, -0.015f), new Vector3(0.265f, 0.15f, 0.295f), default, default, H, 0f, true),
                P(PartShape.Sphere, new Vector3(-0.08f, -0.075f, 0.105f), new Vector3(0.075f, 0.07f, 0.06f), default, default, H),
                P(PartShape.Sphere, new Vector3(-0.04f, -0.08f, 0.115f), new Vector3(0.075f, 0.07f, 0.06f), default, default, H),
                P(PartShape.Sphere, new Vector3(0f, -0.082f, 0.118f), new Vector3(0.075f, 0.07f, 0.06f), default, default, H),
                P(PartShape.Sphere, new Vector3(0.04f, -0.08f, 0.115f), new Vector3(0.075f, 0.07f, 0.06f), default, default, H),
                P(PartShape.Sphere, new Vector3(0.08f, -0.075f, 0.105f), new Vector3(0.075f, 0.07f, 0.06f), default, default, H),
                P(PartShape.Sphere, new Vector3(0.115f, -0.095f, 0f), new Vector3(0.06f, 0.12f, 0.13f), default, default, H),
                P(PartShape.Sphere, new Vector3(-0.115f, -0.095f, 0f), new Vector3(0.06f, 0.12f, 0.13f), default, default, H),
                P(PartShape.Sphere, new Vector3(0f, -0.1f, -0.12f), new Vector3(0.23f, 0.15f, 0.08f), default, default, H));
            var pony = Look(2, "hair.ponytail", "Ponytail", "Aerodynamic. Pigs grab it anyway.", UnlockSource.Starter, "", 1f,
                Cap(),
                P(PartShape.Sphere, new Vector3(0f, -0.09f, -0.155f), Vector3.one * 0.05f, tie),
                P(PartShape.Capsule, new Vector3(0f, -0.19f, -0.2f), new Vector3(0.075f, 0.2f, 0.075f), default, new Vector3(25, 0, 0), H),
                P(PartShape.Sphere, new Vector3(0f, -0.28f, -0.235f), new Vector3(0.085f, 0.1f, 0.085f), default, default, H));
            var afro = Look(3, "hair.afro", "Big Afro", "Hat-resistant. Wind-resistant. Bee-attractive.", UnlockSource.Starter, "", 1.3f,
                P(PartShape.Sphere, new Vector3(0f, -0.02f, -0.025f), new Vector3(0.37f, 0.29f, 0.37f), default, default, H, 0f, true),
                P(PartShape.Sphere, new Vector3(0.125f, -0.1f, -0.03f), new Vector3(0.13f, 0.16f, 0.17f), default, default, H),
                P(PartShape.Sphere, new Vector3(-0.125f, -0.1f, -0.03f), new Vector3(0.13f, 0.16f, 0.17f), default, default, H));
            var mohawk = Look(4, "hair.mohawk", "Mohawk", "Punk rock. Pig rock.", UnlockSource.Trunk, TrunkCommon, 1.3f);
            for (int i = 0; i < 5; i++)
                mohawk.parts.Add(P(PartShape.Cone, new Vector3(0f, -0.015f + (i == 1 || i == 2 ? 0.006f : 0f), 0.08f - i * 0.05f),
                    new Vector3(0.05f, 0.12f + (i == 1 || i == 2 ? 0.03f : 0f), 0.085f), default, new Vector3(-12f - i * 6f, 0, 0), H, 0f, true));
            var bun = Look(5, "hair.bun", "Top Bun", "Holds a pencil, two seeds and a secret.", UnlockSource.Starter, "", 1f,
                Cap(),
                P(PartShape.Sphere, new Vector3(0f, 0.025f, -0.075f), new Vector3(0.125f, 0.115f, 0.125f), default, default, H, 0f, true),
                P(PartShape.Cylinder, new Vector3(0f, -0.02f, -0.075f), new Vector3(0.085f, 0.03f, 0.085f), tie, default, PartTint.Fixed, 0f, true));
            var pig = Look(6, "hair.pigtails", "Pigtails", "Double the tails, double the trouble.", UnlockSource.Starter, "", 1.1f,
                Cap(),
                P(PartShape.Sphere, new Vector3(0.13f, -0.1f, -0.06f), Vector3.one * 0.05f, tie),
                P(PartShape.Sphere, new Vector3(-0.13f, -0.1f, -0.06f), Vector3.one * 0.05f, tie),
                P(PartShape.Capsule, new Vector3(0.165f, -0.18f, -0.06f), new Vector3(0.07f, 0.19f, 0.07f), default, new Vector3(0, 0, 22), H),
                P(PartShape.Capsule, new Vector3(-0.165f, -0.18f, -0.06f), new Vector3(0.07f, 0.19f, 0.07f), default, new Vector3(0, 0, -22), H));
            var beard = Look(7, "hair.beard", "Bushy Beard", "Itchy. Majestic. Home to at least one sparrow.", UnlockSource.Starter, "", 1.2f,
                P(PartShape.Sphere, new Vector3(0f, -0.125f, -0.03f), new Vector3(0.2f, 0.16f, 0.13f), default, default, H, 0f, false, AttachPoint.Face),
                P(PartShape.Sphere, new Vector3(0.078f, -0.075f, -0.065f), new Vector3(0.08f, 0.13f, 0.11f), default, default, H, 0f, false, AttachPoint.Face),
                P(PartShape.Sphere, new Vector3(-0.078f, -0.075f, -0.065f), new Vector3(0.08f, 0.13f, 0.11f), default, default, H, 0f, false, AttachPoint.Face),
                P(PartShape.Sphere, new Vector3(0f, -0.068f, 0.004f), new Vector3(0.12f, 0.035f, 0.04f), default, default, H, 0f, false, AttachPoint.Face));
            var bald = Look(8, "hair.bald", "Bald & Proud", "Aerodynamic. Shiny in the sun.", UnlockSource.Starter, "", 0.8f);
            bald.hideBaseHair = true;
            var tufts = Look(9, "hair.tufts", "Grandpa Tufts", "Business on the sides. Sunburn on top.", UnlockSource.Letter, "Grandpa's Letter: survive your first Landlord Sunday", 1.2f,
                P(PartShape.Sphere, new Vector3(0.118f, -0.085f, -0.02f), new Vector3(0.07f, 0.09f, 0.13f), default, default, H),
                P(PartShape.Sphere, new Vector3(-0.118f, -0.085f, -0.02f), new Vector3(0.07f, 0.09f, 0.13f), default, default, H),
                P(PartShape.Sphere, new Vector3(0f, -0.11f, -0.12f), new Vector3(0.2f, 0.07f, 0.06f), default, default, H));
            tufts.hideBaseHair = true;
            return new List<CosmeticItem> { mop, pony, afro, mohawk, bun, pig, beard, bald, tufts };
        }

        static CosmeticItem Outfit(ushort id, string key, string name, string flavour, UnlockSource unlock, string hint, float weight,
            OutfitPattern overalls, OutfitPattern shirt, PartTint shirtTint = PartTint.Secondary, PartTint sleeves = PartTint.Secondary, params CosmeticPart[] parts)
        {
            var it = Look(id, key, name, flavour, unlock, hint, weight, parts);
            it.outfit = new OutfitStyle { overallsPattern = overalls, shirtPattern = shirt, shirtTint = shirtTint, sleeveTint = sleeves };
            return it;
        }

        static List<CosmeticItem> DefaultOutfits()
        {
            Color cream = C("#F2E6C8");
            return new List<CosmeticItem>
            {
                Outfit(1, "outfit.gingham", "Gingham Gamble", "Picnic-ready. Not pig-ready.", UnlockSource.Trunk, TrunkCommon, 1f, OutfitPattern.Gingham, OutfitPattern.Plain),
                Outfit(2, "outfit.patched", "Patched Up", "Every patch is a story. Most are about the goat.", UnlockSource.Trunk, TrunkCommon, 1.1f, OutfitPattern.Patches, OutfitPattern.Plain),
                Outfit(3, "outfit.mud", "Mud Season", "Pre-muddied for your convenience.", UnlockSource.Trunk, TrunkCommon, 1.1f, OutfitPattern.Mud, OutfitPattern.Plain),
                Outfit(4, "outfit.pinstripe", "Debt Collector Pinstripe", "Dress for the job you owe.", UnlockSource.Trunk, TrunkRare, 1f, OutfitPattern.Pinstripe, OutfitPattern.Plain),
                Outfit(5, "outfit.flannel", "Flannel & Jeans", "Lumberjack chic. No lumber was jacked.", UnlockSource.Starter, "", 1.2f, OutfitPattern.Denim, OutfitPattern.Flannel),
                Outfit(6, "outfit.raincoat", "Rain Slicker", "Puddle-proof. Pig-proof? No.", UnlockSource.Trunk, TrunkRare, 1.1f, OutfitPattern.Plain, OutfitPattern.Plain, PartTint.Primary, PartTint.Primary,
                    P(PartShape.Sphere, new Vector3(0f, 0.2f, -0.035f), new Vector3(0.28f, 0.13f, 0.17f), default, new Vector3(-10, 0, 0), PartTint.Primary, 0f, false, AttachPoint.Back),
                    P(PartShape.Cone, new Vector3(0f, -0.34f, 0f), new Vector3(0.46f, 0.36f, 0.36f), default, default, PartTint.Primary, 0.8f, false, AttachPoint.Hips),
                    P(PartShape.Cube, new Vector3(0f, 0.06f, 0.125f), new Vector3(0.025f, 0.05f, 0.02f), cream, default, PartTint.Fixed, 0f, false, AttachPoint.Chest),
                    P(PartShape.Cube, new Vector3(0f, -0.04f, 0.13f), new Vector3(0.025f, 0.05f, 0.02f), cream, default, PartTint.Fixed, 0f, false, AttachPoint.Chest)),
                Outfit(7, "outfit.polka", "Polka Party", "Spotted from space. Spotted by crows.", UnlockSource.Trunk, TrunkCommon, 1.1f, OutfitPattern.Polka, OutfitPattern.Plain),
                Outfit(8, "outfit.sailor", "Sailor Stripes", "Seaworthy. Barn-worthy? Debatable.", UnlockSource.Starter, "", 1f, OutfitPattern.Plain, OutfitPattern.Stripes),
                Outfit(9, "outfit.tank", "Muscle Tank", "Sleeves are for people who've never lifted a pig.", UnlockSource.Starter, "", 1.1f, OutfitPattern.Plain, OutfitPattern.Plain, PartTint.Skin, PartTint.Skin),
            };
        }
    }
}
