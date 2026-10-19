using System;
using System.Collections.Generic;
using System.Text;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Server-side loadout validation (GDD Edge Cases: "Unowned item in loadout ... the server replaces it with the
    /// slot default and logs it. There is no kick." and "Duplicate name tag colour: the later joiner gets the next free colour").
    /// Pure logic: no Unity objects besides the catalog, so it is unit-tested in EditMode.
    /// </summary>
    public static class LoadoutValidator
    {
        /// <summary>
        /// Returns a copy of <paramref name="input"/> where every out-of-range index, unknown id and unowned item is
        /// replaced by the slot default. <paramref name="report"/> lists what changed (empty when nothing did).
        /// </summary>
        /// <param name="ownedKeys">Unlocked item keys of this player, or null when the server only trusts starter items.</param>
        public static FarmerLoadout Sanitize(FarmerLoadout input, CosmeticCatalog catalog, ICollection<string> ownedKeys, out string report)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var d = FarmerLoadout.Default;
            var o = input;
            var sb = new StringBuilder();

            void Fix(string what, int from, int to) { sb.Append(what).Append(' ').Append(from).Append("->").Append(to).Append("; "); }

            if (o.Body >= catalog.bodies.Count) { Fix("body", o.Body, d.Body); o.Body = d.Body; }
            if (o.Primary >= catalog.palette.Count) { Fix("primary", o.Primary, d.Primary); o.Primary = d.Primary; }
            if (o.Secondary >= catalog.palette.Count) { Fix("secondary", o.Secondary, d.Secondary); o.Secondary = d.Secondary; }
            if (o.Accent >= catalog.palette.Count) { Fix("accent", o.Accent, d.Accent); o.Accent = d.Accent; }
            if (o.Skin >= catalog.skinTones.Count ||
                (catalog.skinTones[o.Skin].fantasy && !catalog.IsOwned(UnlockSource.Letter, "skin." + o.Skin, ownedKeys)))
            { Fix("skin", o.Skin, d.Skin); o.Skin = d.Skin; }
            if (o.NameTag >= catalog.nameTags.Count) { Fix("tag", o.NameTag, d.NameTag); o.NameTag = d.NameTag; }

            foreach (CosmeticSlot slot in new[] { CosmeticSlot.Hat, CosmeticSlot.Face, CosmeticSlot.Back })
            {
                ushort id = o.GetItem(slot);
                if (id == 0) continue;
                var item = catalog.FindItem(slot, id);
                if (item == null || !catalog.IsOwned(item.unlock, item.key, ownedKeys))
                {
                    ushort def = d.GetItem(slot);
                    Fix(slot.ToString().ToLowerInvariant(), id, def);
                    o.SetItem(slot, def);
                }
            }

            for (int k = 0; k < 4; k++)
            {
                ushort id = o.GetEmote(k);
                var e = catalog.FindEmote(id);
                if (id != 0 && (e == null || !catalog.IsOwned(e.unlock, "emote." + e.key, ownedKeys)))
                {
                    ushort def = d.GetEmote(k);
                    Fix("emote" + k, id, def);
                    o.SetEmote(k, def);
                }
            }

            report = sb.ToString();
            return o;
        }

        /// <summary>
        /// Picks the name-tag colour for a player: <paramref name="requested"/> when free, otherwise the next free index
        /// (cyclic). <paramref name="takenByEarlierJoiners"/> holds tags of players who joined before this one.
        /// When all are taken (more players than colours) the request is kept.
        /// </summary>
        public static byte ResolveNameTag(byte requested, int tagCount, ICollection<byte> takenByEarlierJoiners)
        {
            if (tagCount <= 0 || takenByEarlierJoiners == null || !takenByEarlierJoiners.Contains(requested)) return requested;
            for (int i = 1; i < tagCount; i++)
            {
                byte candidate = (byte)((requested + i) % tagCount);
                if (!takenByEarlierJoiners.Contains(candidate)) return candidate;
            }
            return requested;
        }
    }
}
