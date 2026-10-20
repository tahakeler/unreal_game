using System;

namespace HarvestPanic.Audio
{
    /// <summary>Mix group of a sound (sound-bible §4.1). Drives the user bus and which ducks apply.</summary>
    public enum MixGroup : byte { Music, Critters, Impacts, Player, Ambience, UIStingers, Voice }

    /// <summary>3D attenuation presets (sound-bible §5.4). Logarithmic rolloff, min -> max distance.</summary>
    public enum AttenuationPreset : byte { TwoD, Player, CritterSmall, CritterLarge, Telegraph, Impact, HazardLoud, AmbiencePoint }

    /// <summary>Concurrency group with a voice cap and steal rule (sound-bible §4.4).</summary>
    public enum VoiceLimit : byte { None, Footsteps, Impacts, Stingers }

    /// <summary>Static description of one runtime event (folder name under Resources/Audio).</summary>
    public readonly struct AudioEventDef
    {
        public readonly string Name;
        public readonly MixGroup Group;
        public readonly AttenuationPreset Attenuation;
        public readonly VoiceLimit Limit;
        public readonly float BaseDb;
        public readonly float PitchJitterSemis;

        public AudioEventDef(string name, MixGroup group, AttenuationPreset att, VoiceLimit limit, float baseDb, float pitchJitter)
        { Name = name; Group = group; Attenuation = att; Limit = limit; BaseDb = baseDb; PitchJitterSemis = pitchJitter; }
    }

    /// <summary>
    /// The event table (stand-in for the sound-bible's DT_AudioEvents). Index in <see cref="All"/> is the wire id
    /// used by <c>AudioNetRelay</c>, so append only.
    /// </summary>
    public static class AudioEvents
    {
        public const string FootstepDirt = "SFX_Player_Footstep_Dirt";
        public const string PlayerShout = "SFX_Player_Shout";
        public const string PigSqueal = "SFX_Critter_Pig_Squeal";
        public const string ChickenPanic = "SFX_Critter_Chicken_Panic";
        public const string BullTelegraph = "SFX_Critter_Bull_Telegraph";
        public const string PlankCreak = "SFX_Bridge_Plank_Creak";
        public const string PlankBreak = "SFX_Bridge_Plank_Break";
        public const string SplashBig = "SFX_Water_Splash_Big";
        public const string SplashSmall = "SFX_Water_Splash_Small";
        public const string ImpactMetal = "SFX_Impact_Metal";
        public const string ToolMetal = "SFX_Tool_Metal";
        public const string ImpactCrate = "SFX_Impact_Crate";
        public const string KaChing = "SFX_Wallet_KaChing";
        public const string WalletWah = "SFX_Wallet_Wah";
        public const string CoinTick = "SFX_Bruise_CoinTick";
        public const string StgRiver = "STG_Disaster_River";
        public const string StgPlank = "STG_Disaster_Plank";
        public const string StgWagon = "STG_Disaster_Wagon";
        public const string StgGeneric = "STG_Disaster_Generic";
        public const string StgRecordScratch = "STG_Disaster_RecordScratch";
        public const string StgSweetener = "STG_Disaster_Sweetener";
        public const string StgThroatClear = "STG_Music_ThroatClear";
        public const string PocketWatchTick = "UI_Timer_PocketWatch_Tick";
        public const string SundownBell = "SFX_Market_Bell_Sundown";
        public const string AmbFarmDay = "AMB_Farm_Day";

        public const string MusMorning = "MUS_Morning_Porch";
        public const string MusRoundup = "MUS_Workday_Roundup";
        public const string MusCrunch = "MUS_Workday_Crunch";
        public const string MusDusk = "MUS_Night_Dusk";
        public const string MusPayout = "MUS_Payout_Market";
        public const string MusSunday = "MUS_Sunday_Landlord";

        static AudioEventDef D(string n, MixGroup g, AttenuationPreset a, VoiceLimit l = VoiceLimit.None, float db = 0f, float jitter = 1.5f) =>
            new AudioEventDef(n, g, a, l, db, jitter);

        /// <summary>Every event. APPEND ONLY (index = network id).</summary>
        public static readonly AudioEventDef[] All =
        {
            D(FootstepDirt, MixGroup.Player, AttenuationPreset.Player, VoiceLimit.Footsteps, -12f),
            D("SFX_Player_Footstep_Grass", MixGroup.Player, AttenuationPreset.Player, VoiceLimit.Footsteps, -12f),
            D("SFX_Player_Footstep_Wood", MixGroup.Player, AttenuationPreset.Player, VoiceLimit.Footsteps, -12f),
            D(PlayerShout, MixGroup.Player, AttenuationPreset.Player, VoiceLimit.None, -4f),
            D(PigSqueal, MixGroup.Critters, AttenuationPreset.CritterLarge, VoiceLimit.None, -8f, 3f),
            D(ChickenPanic, MixGroup.Critters, AttenuationPreset.CritterSmall, VoiceLimit.None, -8f, 3f),
            D(BullTelegraph, MixGroup.Critters, AttenuationPreset.Telegraph, VoiceLimit.None, -2f, 0.5f),
            D(PlankCreak, MixGroup.Impacts, AttenuationPreset.Impact, VoiceLimit.Impacts, -8f),
            D(PlankBreak, MixGroup.Impacts, AttenuationPreset.HazardLoud, VoiceLimit.Impacts, -3f),
            D(SplashBig, MixGroup.Impacts, AttenuationPreset.HazardLoud, VoiceLimit.Impacts, -3f),
            D(SplashSmall, MixGroup.Impacts, AttenuationPreset.Impact, VoiceLimit.Impacts, -6f),
            D(ImpactMetal, MixGroup.Impacts, AttenuationPreset.Impact, VoiceLimit.Impacts, -6f),
            D(ToolMetal, MixGroup.Impacts, AttenuationPreset.Impact, VoiceLimit.Impacts, -8f),
            D(ImpactCrate, MixGroup.Impacts, AttenuationPreset.Impact, VoiceLimit.Impacts, -6f),
            D("SFX_Impact_Wood", MixGroup.Impacts, AttenuationPreset.Impact, VoiceLimit.Impacts, -6f),
            D("SFX_Impact_Dirt", MixGroup.Impacts, AttenuationPreset.Impact, VoiceLimit.Impacts, -6f),
            D("SFX_Impact_Soft", MixGroup.Impacts, AttenuationPreset.Impact, VoiceLimit.Impacts, -6f),
            D("SFX_Item_Break", MixGroup.Impacts, AttenuationPreset.Impact, VoiceLimit.Impacts, -4f),
            D("SFX_Bruise_Thud_Small", MixGroup.Impacts, AttenuationPreset.Impact, VoiceLimit.Impacts, -6f),
            D("SFX_Bruise_Crunch", MixGroup.Impacts, AttenuationPreset.Impact, VoiceLimit.Impacts, -6f),
            D("SFX_Bruise_RegisterClunk", MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -6f),
            D(CoinTick, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -8f, 0.5f),
            D(KaChing, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -4f, 0.5f),
            D(WalletWah, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -4f, 0f),
            D(SundownBell, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -4f, 0f),
            D(PocketWatchTick, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -10f, 0f),
            D(StgRiver, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.Stingers, -4f, 0f),
            D(StgPlank, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.Stingers, -4f, 0f),
            D(StgWagon, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.Stingers, -4f, 0f),
            D(StgGeneric, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.Stingers, -4f, 0f),
            D(StgRecordScratch, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -4f, 0f),
            D(StgSweetener, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -6f, 0f),
            D(StgThroatClear, MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -6f, 0f),
            D("UI_Menu_Hover", MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -10f, 0f),
            D("UI_Menu_Confirm", MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -6f, 0f),
            D("UI_Menu_Back", MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -6f, 0f),
            D("UI_Menu_Error", MixGroup.UIStingers, AttenuationPreset.TwoD, VoiceLimit.None, -6f, 0f),
            D(AmbFarmDay, MixGroup.Ambience, AttenuationPreset.TwoD, VoiceLimit.None, -18f, 0f),
            D(MusMorning, MixGroup.Music, AttenuationPreset.TwoD, VoiceLimit.None, -14f, 0f),
            D(MusRoundup, MixGroup.Music, AttenuationPreset.TwoD, VoiceLimit.None, -14f, 0f),
            D(MusCrunch, MixGroup.Music, AttenuationPreset.TwoD, VoiceLimit.None, -14f, 0f),
            D(MusDusk, MixGroup.Music, AttenuationPreset.TwoD, VoiceLimit.None, -14f, 0f),
            D(MusPayout, MixGroup.Music, AttenuationPreset.TwoD, VoiceLimit.None, -14f, 0f),
            D(MusSunday, MixGroup.Music, AttenuationPreset.TwoD, VoiceLimit.None, -14f, 0f),
        };

        static System.Collections.Generic.Dictionary<string, int> _index;

        /// <summary>Wire id of <paramref name="name"/> or -1.</summary>
        public static int IndexOf(string name)
        {
            if (_index == null)
            {
                _index = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < All.Length; i++) _index[All[i].Name] = i;
            }
            return name != null && _index.TryGetValue(name, out var idx) ? idx : -1;
        }

        public static bool TryGet(string name, out AudioEventDef def)
        {
            int i = IndexOf(name);
            def = i >= 0 ? All[i] : default;
            return i >= 0;
        }

        /// <summary>The user-facing settings bus a mix group feeds.</summary>
        public static HarvestPanic.Core.AudioBus BusOf(MixGroup g) => g switch
        {
            MixGroup.Music => HarvestPanic.Core.AudioBus.Music,
            MixGroup.UIStingers => HarvestPanic.Core.AudioBus.UI,
            MixGroup.Voice => HarvestPanic.Core.AudioBus.Voice,
            _ => HarvestPanic.Core.AudioBus.Sfx,
        };

        /// <summary>Min/max distance (m) of an attenuation preset (sound-bible §5.4).</summary>
        public static void Range(AttenuationPreset p, out float min, out float max)
        {
            switch (p)
            {
                case AttenuationPreset.Player: min = 2f; max = 25f; break;
                case AttenuationPreset.CritterSmall: min = 2f; max = 30f; break;
                case AttenuationPreset.CritterLarge: min = 4f; max = 60f; break;
                case AttenuationPreset.Telegraph: min = 6f; max = 80f; break;
                case AttenuationPreset.Impact: min = 3f; max = 50f; break;
                case AttenuationPreset.HazardLoud: min = 5f; max = 90f; break;
                case AttenuationPreset.AmbiencePoint: min = 5f; max = 40f; break;
                default: min = 1f; max = 1f; break;
            }
        }

        /// <summary>Voice cap of a concurrency group (0 = unlimited).</summary>
        public static int CapOf(VoiceLimit l) => l switch
        {
            VoiceLimit.Footsteps => 8,
            VoiceLimit.Impacts => 12,
            VoiceLimit.Stingers => 1,
            _ => 0,
        };
    }
}
