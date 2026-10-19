using System;
using Unity.Collections;
using Unity.Netcode;

namespace HarvestPanic.Customization
{
    /// <summary>Equipment slots for accessory items (ids are per-slot, 0 = None).</summary>
    public enum CosmeticSlot : byte { Hat = 0, Face = 1, Back = 2 }

    /// <summary>
    /// One player's Farmer Loadout (design/gdd/character-customization.md, "Replication &amp; persistence").
    /// Compact, unmanaged, 20 bytes on the wire (GDD budget: 32): body u8, 3 palette indices u8, skin u8,
    /// name-tag u8, hat/face/back u16, 4 equipped emotes u16. Values are indices into <see cref="CosmeticCatalog"/>;
    /// the server sanitises every received loadout with <see cref="LoadoutValidator"/>.
    /// </summary>
    [Serializable]
    public struct FarmerLoadout : INetworkSerializable, IEquatable<FarmerLoadout>
    {
        /// <summary>Wire size in bytes (kept in sync with <see cref="NetworkSerialize{T}"/>; asserted by tests).</summary>
        public const int WireSize = 20;

        /// <summary>Body variant index (0 Sturdy, 1 Lanky).</summary>
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

        /// <summary>Starter loadout: Sturdy, Denim overalls, Straw Hat, Wave/Point/Dance/Faint on 5-8.</summary>
        public static FarmerLoadout Default => new FarmerLoadout
        {
            Body = 0, Primary = 0, Secondary = 14, Accent = 17, Skin = 2, NameTag = 0,
            Hat = 1, Face = 0, Back = 0, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4
        };

        /// <summary>Accessory id for <paramref name="slot"/>.</summary>
        public ushort GetItem(CosmeticSlot slot) => slot == CosmeticSlot.Hat ? Hat : slot == CosmeticSlot.Face ? Face : Back;

        /// <summary>Sets the accessory id for <paramref name="slot"/>.</summary>
        public void SetItem(CosmeticSlot slot, ushort id)
        {
            if (slot == CosmeticSlot.Hat) Hat = id;
            else if (slot == CosmeticSlot.Face) Face = id;
            else Back = id;
        }

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
            s.SerializeValue(ref Body);
            s.SerializeValue(ref Primary);
            s.SerializeValue(ref Secondary);
            s.SerializeValue(ref Accent);
            s.SerializeValue(ref Skin);
            s.SerializeValue(ref NameTag);
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

        /// <summary>Reads a loadout written by <see cref="ToBytes"/>.</summary>
        public static FarmerLoadout FromBytes(byte[] bytes)
        {
            using (var r = new FastBufferReader(bytes, Allocator.Temp))
            {
                r.ReadNetworkSerializable(out FarmerLoadout l);
                return l;
            }
        }

        /// <inheritdoc/>
        public bool Equals(FarmerLoadout o) =>
            Body == o.Body && Primary == o.Primary && Secondary == o.Secondary && Accent == o.Accent && Skin == o.Skin &&
            NameTag == o.NameTag && Hat == o.Hat && Face == o.Face && Back == o.Back &&
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
                h = h * 397 ^ (Face | Back << 16);
                h = h * 397 ^ (Emote0 | Emote1 << 16);
                return h * 397 ^ (Emote2 | Emote3 << 16);
            }
        }

        /// <inheritdoc/>
        public override string ToString() =>
            $"Body {Body} Col {Primary}/{Secondary}/{Accent} Skin {Skin} Tag {NameTag} Hat {Hat} Face {Face} Back {Back} Emotes {Emote0},{Emote1},{Emote2},{Emote3}";
    }
}
