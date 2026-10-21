using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Dev-only helper for Scenes/Dev/WardrobeTest.unity: applies a set of silly demo loadouts to standalone farmers
    /// (no networking) so the lineup, tests and QA screenshots have something to look at.
    /// </summary>
    public sealed class WardrobeTestLineup : MonoBehaviour
    {
        [SerializeField] List<FarmerVisual> farmers = new List<FarmerVisual>();

        /// <summary>Lineup farmers, left to right.</summary>
        public IReadOnlyList<FarmerVisual> Farmers => farmers;

        /// <summary>Demo set A: one of each team colour, very different silhouettes (index 2 keeps the base head/hair/outfit).</summary>
        public static FarmerLoadout[] SetA => new[]
        {
            new FarmerLoadout { Body = 0, Primary = 0, Secondary = 5, Accent = 17, Skin = 2, NameTag = 0, Head = 2, Hair = 0, HairColor = 17, Outfit = 5, Hat = 1, Face = 1, Back = 1, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 1, Primary = 1, Secondary = 15, Accent = 13, Skin = 11, NameTag = 1, Head = 1, Hair = 2, HairColor = 13, Outfit = 3, Hat = 8, Face = 3, Back = 2, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 0, Primary = 2, Secondary = 20, Accent = 23, Skin = 12, NameTag = 2, HairColor = 17, Hat = 9, Face = 2, Back = 3, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 1, Primary = 3, Secondary = 8, Accent = 12, Skin = 9, NameTag = 3, Head = 4, Hair = 3, HairColor = 18, Outfit = 1, Hat = 6, Face = 4, Back = 0, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
        };

        /// <summary>Demo set B: the remaining hats plus the new bodies, hairstyles and outfits.</summary>
        public static FarmerLoadout[] SetB => new[]
        {
            new FarmerLoadout { Body = 2, Primary = 9, Secondary = 18, Accent = 17, Skin = 5, NameTag = 4, Head = 5, Hair = 9, HairColor = 15, Outfit = 2, Hat = 4, Face = 0, Back = 1, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 0, Primary = 4, Secondary = 14, Accent = 17, Skin = 7, NameTag = 5, Head = 6, Hair = 7, HairColor = 11, Outfit = 6, Hat = 5, Face = 0, Back = 3, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 3, Primary = 7, Secondary = 23, Accent = 19, Skin = 13, NameTag = 6, Head = 3, Hair = 6, HairColor = 20, Outfit = 7, Hat = 7, Face = 3, Back = 2, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 1, Primary = 6, Secondary = 16, Accent = 4, Skin = 0, NameTag = 7, Head = 7, Hair = 4, HairColor = 8, Outfit = 4, Hat = 10, Face = 2, Back = 0, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
        };

        /// <summary>Demo set C: no hats, so heads and hairstyles read (QA captures).</summary>
        public static FarmerLoadout[] SetC => new[]
        {
            new FarmerLoadout { Body = 0, Primary = 0, Secondary = 14, Accent = 17, Skin = 3, NameTag = 0, Head = 3, Hair = 1, HairColor = 11, Outfit = 9, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 3, Primary = 1, Secondary = 15, Accent = 13, Skin = 9, NameTag = 1, Head = 5, Hair = 6, HairColor = 13, Outfit = 8, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 2, Primary = 2, Secondary = 18, Accent = 17, Skin = 6, NameTag = 2, Head = 2, Hair = 7, HairColor = 17, Outfit = 5, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 1, Primary = 3, Secondary = 20, Accent = 12, Skin = 1, NameTag = 3, Head = 7, Hair = 4, HairColor = 23, Outfit = 2, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
        };

        /// <summary>Names shown on the demo tags.</summary>
        public static readonly string[] Names = { "Grandpa Jr.", "Cone Zone", "Big Sven", "Chef Oops" };

        System.Collections.IEnumerator Start()
        {
            yield return null; // after FarmerLoadoutSync.Start so the demo names stick
            ApplySet(SetA, Names);
        }

        /// <summary>Applies <paramref name="set"/> to the lineup (wraps if shorter).</summary>
        public void ApplySet(FarmerLoadout[] set, string[] names = null)
        {
            for (int i = 0; i < farmers.Count; i++)
            {
                if (farmers[i] == null) continue;
                farmers[i].Apply(set[i % set.Length]);
                if (names != null && farmers[i].NameTag != null) farmers[i].NameTag.SetName(names[i % names.Length]);
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor builder hook.</summary>
        public void EditorSetup(List<FarmerVisual> list) => farmers = list;
#endif
    }
}
