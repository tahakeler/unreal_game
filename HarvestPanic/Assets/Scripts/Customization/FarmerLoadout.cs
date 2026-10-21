using System;
using Unity.Collections;
using Unity.Netcode;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Equipment slots for catalogue items (ids are per-slot). Add-only: values are part of the save and the editor tooling.
    /// Hat/Face/Back: 0 = None. Head/Hair/Outfit: 0 = the base farmer look (round head, short crop, plain overalls).
    /// </summary>
    public enum CosmeticSlot : byte { Hat = 0, Face = 1, Back = 2, Head = 3, Hair = 4, Outfit = 5 }

    /// <summary>
    /// One player's Farmer Loadout (design/gdd/character-customization.md, "Replication &amp; persistence").
    /// Compact and unmanaged. Wire format v2 = 25 bytes (GDD budget 32, 7 spare):
    /// <code>
    /// offset size field
    ///  0     1   format (=2)
    ///  1     1   body           6  1  name tag       11 2 hat
    ///  2     1   primary        7  1  head           13 2 face
    ///  3     1   secondary      8  1  hair           15 2 back
    ///  4     1   accent         9  1  hair colour    17 8 emotes 4 x u16 (keys 5-8)
    ///  5     1   skin          10  1  outfit
    /// </code>
    /// v1 (20 bytes, no format byte, no head/hair/outfit) is still readable through <see cref="FromBytes"/>.
    /// Values are indices into <see cref="CosmeticCatalog"/>; the server sanitises every received loadout with
    /// <see cref="LoadoutValidator"/>.
    /// </summary>
    [Serializable]
    public struct FarmerLoadout : INetworkSerializable, IEquatable<FarmerLoadout>
    {
        /// <summary>Current wire/save format.</summary>
        public const byte Format = 2;
        /// <summary>Wire size in bytes (kept in sync with <see cref="NetworkSerialize{T}"/>; asserted by tests).</summary>
        public const int WireSize = 25;
        /// <summary>Size of the legacy v1 wire format.</summary>
        public const int WireSizeV1 = 20;
        /// <summary>GDD AC7 budget.</summary>
        public const int WireBudget = 32;

        /// <summary>Body variant index (0 Sturdy, 1 Lanky, ...).</summary>
        public byte Body;
        /// <summary>Overalls colour: index into the 24-swatch palette.</summary>
        public byte Primary;
        /// <summary>Shirt colour: palette index.</summary>
        public byte Secondary;
        /// <summary>Boots/gloves colour: palette index.</summary>
        public byte Accent;
        /// <summary>Skin tone index (0..11 base, 12..13 fantasy).</summary>
        public byte Skin;
        /// <summary>Name-tag colour index (0..7); also the player's HUD/Ledger colour.</summary>
        public byte NameTag;
        /// <summary>Head shape / face id (0 = base round head).</summary>
        public byte Head;
        /// <summary>Hairstyle id (0 = base short crop).</summary>
        public byte Hair;
        /// <summary>Hair colour: palette index.</summary>
        public byte HairColor;
        /// <summary>Outfit id (0 = plain overalls).</summary>
        public byte Outfit;
        /// <summary>Hat id (0 = none).</summary>
        public ushort Hat;
        /// <summary>Face item id (0 = none).</summary>
        public ushort Face;
        /// <summary>Back item id (0 = none).</summary>
        public ushort Back;
        /// <summary>Emote bound to key 5.</summary>
        public ushort Emote0;
        /// <summary>Emote bound to key 6.</summary>
        public ushort Emote1;
        /// <summary>Emote bound to key 7.</summary>
        public ushort Emote2;
        /// <summary>Emote bound to key 8.</summary>
        public ushort Emote3;

        /// <summary>Palette index of the default hair colour (Mud Brown).</summary>
        public const byte DefaultHairColor = 17;

        /// <summary>Starter loadout: Sturdy, Denim overalls, Straw Hat, Wave/Point/Dance/Faint on 5-8.</summary>
        public static FarmerLoadout Default => new FarmerLoadout
        {
            Body = 0, Primary = 0, Secondary = 14, Accent = 17, Skin = 2, NameTag = 0,
            Head = 0, Hair = 0, HairColor = DefaultHairColor, Outfit = 0,
            Hat = 1, Face = 0, Back = 0, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4
        };

        /// <summary>Item id for <paramref name="slot"/>.</summary>
        public ushort GetItem(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return Hat;
                case CosmeticSlot.Face: return Face;
                case CosmeticSlot.Back: return Back;
                case CosmeticSlot.Head: return Head;
                case CosmeticSlot.Hair: return Hair;
                case CosmeticSlot.Outfit: return Outfit;
                default: return 0;
            }
        }

        /// <summary>Sets the item id for <paramref name="slot"/> (byte slots clamp to 255).</summary>
        public void SetItem(CosmeticSlot slot, ushort id)
        {
            byte b = (byte)Math.Min(id, (ushort)255);
            switch (slot)
            {
                case CosmeticSlot.Hat: Hat = id; break;
                case CosmeticSlot.Face: Face = id; break;
                case CosmeticSlot.Back: Back = id; break;
                case CosmeticSlot.Head: Head = b; break;
                case CosmeticSlot.Hair: Hair = b; break;
                case CosmeticSlot.Outfit: Outfit = b; break;
            }
        }

        /// <summary>Largest id a slot can carry on the wire.</summary>
        public static ushort MaxId(CosmeticSlot slot) => slot == CosmeticSlot.Hat || slot == CosmeticSlot.Face || slot == CosmeticSlot.Back ? ushort.MaxValue : (ushort)255;

        /// <summary>Emote id on key slot 0..3 (keys 5..8).</summary>
        public ushort GetEmote(int keySlot)
        {
            switch (keySlot) { case 0: return Emote0; case 1: return Emote1; case 2: return Emote2; case 3: return Emote3; default: return 0; }
        }

        /// <summary>Binds <paramref name="emoteId"/> to key slot 0..3.</summary>
        public void SetEmote(int keySlot, ushort emoteId)
        {
            switch (keySlot) { case 0: Emote0 = emoteId; break; case 1: Emote1 = emoteId; break; case 2: Emote2 = emoteId; break; case 3: Emote3 = emoteId; break; }
        }

        /// <inheritdoc/>
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            byte format = Format;
            s.SerializeValue(ref format); // both peers run the same build (connection approval checks it); kept for saves/tests
            s.SerializeValue(ref Body);
            s.SerializeValue(ref Primary);
            s.SerializeValue(ref Secondary);
            s.SerializeValue(ref Accent);
            s.SerializeValue(ref Skin);
            s.SerializeValue(ref NameTag);
            s.SerializeValue(ref Head);
            s.SerializeValue(ref Hair);
            s.SerializeValue(ref HairColor);
            s.SerializeValue(ref Outfit);
            s.SerializeValue(ref Hat);
            s.SerializeValue(ref Face);
            s.SerializeValue(ref Back);
            s.SerializeValue(ref Emote0);
            s.SerializeValue(ref Emote1);
            s.SerializeValue(ref Emote2);
            s.SerializeValue(ref Emote3);
        }

        /// <summary>Serialises with NGO's writer and returns the bytes (tests, save checksum).</summary>
        public byte[] ToBytes()
        {
            using (var w = new FastBufferWriter(64, Allocator.Temp))
            {
                var copy = this;
                w.WriteNetworkSerializable(copy);
                return w.ToArray();
            }
        }

        /// <summary>Reads bytes written by <see cref="ToBytes"/> (v2) or by the v1 build (20 bytes; new slots get their base look).</summary>
        public static FarmerLoadout FromBytes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length == WireSizeV1) return FromV1Bytes(bytes);
            using (var r = new FastBufferReader(bytes, Allocator.Temp))
            {
                r.ReadNetworkSerializable(out FarmerLoadout l);
                return l;
            }
        }

        /// <summary>v1 layout: body, primary, secondary, accent, skin, tag (u8 each), hat, face, back, 4 emotes (u16 LE).</summary>
        static FarmerLoadout FromV1Bytes(byte[] b)
        {
            ushort U(int o) => (ushort)(b[o] | b[o + 1] << 8);
            var l = Default;
            l.Body = b[0]; l.Primary = b[1]; l.Secondary = b[2]; l.Accent = b[3]; l.Skin = b[4]; l.NameTag = b[5];
            l.Hat = U(6); l.Face = U(8); l.Back = U(10);
            l.Emote0 = U(12); l.Emote1 = U(14); l.Emote2 = U(16); l.Emote3 = U(18);
            l.Head = 0; l.Hair = 0; l.HairColor = DefaultHairColor; l.Outfit = 0;
            return l;
        }

        /// <inheritdoc/>
        public bool Equals(FarmerLoadout o) =>
            Body == o.Body && Primary == o.Primary && Secondary == o.Secondary && Accent == o.Accent && Skin == o.Skin &&
            NameTag == o.NameTag && Head == o.Head && Hair == o.Hair && HairColor == o.HairColor && Outfit == o.Outfit &&
            Hat == o.Hat && Face == o.Face && Back == o.Back &&
            Emote0 == o.Emote0 && Emote1 == o.Emote1 && Emote2 == o.Emote2 && Emote3 == o.Emote3;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is FarmerLoadout o && Equals(o);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int h = Body | Primary << 8 | Secondary << 16 | Accent << 24;
                h = h * 397 ^ (Skin | NameTag << 8 | Hat << 16);
                h = h * 397 ^ (Head | Hair << 8 | HairColor << 16 | Outfit << 24);
                h = h * 397 ^ (Face | Back << 16);
                h = h * 397 ^ (Emote0 | Emote1 << 16);
                return h * 397 ^ (Emote2 | Emote3 << 16);
            }
        }

        /// <inheritdoc/>
        public override string ToString() =>
            $"Body {Body} Col {Primary}/{Secondary}/{Accent} Skin {Skin} Tag {NameTag} Head {Head} Hair {Hair}/{HairColor} Outfit {Outfit} Hat {Hat} Face {Face} Back {Back} Emotes {Emote0},{Emote1},{Emote2},{Emote3}";
    }
}
