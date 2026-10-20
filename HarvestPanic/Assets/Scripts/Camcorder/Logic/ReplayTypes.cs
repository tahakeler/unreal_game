using System;
using UnityEngine;

namespace HarvestPanic.Camcorder
{
    /// <summary>What a recorded actor is (drives ghost fallback colour and camera targets).</summary>
    public enum TrackKind : byte { Other = 0, Player = 1, Critter = 2, Carryable = 3, Plank = 4 }

    /// <summary>Per-sample state bits.</summary>
    [Flags]
    public enum SampleFlags : byte { None = 0, Visible = 1, Held = 2 }

    /// <summary>One actor transform at one tick (36 bytes, no references: the ring stays GC-free).</summary>
    [Serializable]
    public struct ReplaySample
    {
        public int Id;
        public TrackKind Kind;
        public SampleFlags Flags;
        public Vector3 Position;
        public Quaternion Rotation;

        public ReplaySample(int id, TrackKind kind, SampleFlags flags, Vector3 pos, Quaternion rot)
        { Id = id; Kind = kind; Flags = flags; Position = pos; Rotation = rot; }

        /// <summary>Approximate in-memory size used for budget maths.</summary>
        public const int SizeBytes = 36;
    }

    /// <summary>Discrete things that happened during a recording.</summary>
    public enum ReplayEventType : byte { Disaster = 0, Noise = 1, Manual = 2 }

    /// <summary>A disaster, noise cue or manual mark in recording time.</summary>
    [Serializable]
    public struct ReplayEvent
    {
        public float Time;
        public ReplayEventType Type;
        public Vector3 Position;
        /// <summary>Severity (0..100) for disasters, loudness (0..1) for noise.</summary>
        public float Value;
        /// <summary>BlameVerb as int for disasters, NoiseTag bits for noise.</summary>
        public int Code;
        public ulong PlayerId;
    }

    /// <summary>Static facts about a recorded actor, written once per tape.</summary>
    [Serializable]
    public struct TrackInfo
    {
        public int Id;
        public TrackKind Kind;
        /// <summary>NGO owner client id for players (PlayerIds.None otherwise).</summary>
        public ulong OwnerClientId;
        public string Name;
    }
}
