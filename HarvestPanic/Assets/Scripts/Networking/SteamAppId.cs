using System;
using System.IO;
using UnityEngine;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Resolves the Steam App ID. Priority: "-steamAppId N" launch arg &gt; steam_appid.txt (next to the executable or in
    /// the working directory) &gt; NetworkingConfig.steamAppId (480 = Valve's Spacewar test app until the owner has a
    /// real App ID; see NetworkingConfig). Invalid values fall through to the next source.
    /// </summary>
    public static class SteamAppId
    {
        /// <summary>Valve's public test app ("Spacewar"). Works for lobbies/relay with any Steam account.</summary>
        public const uint SpacewarTestAppId = 480;
        public const string FileName = "steam_appid.txt";

        /// <summary>Pure resolution (EditMode tested). Null/blank/invalid inputs are ignored.</summary>
        public static uint Resolve(uint configId, string fileText, string argText)
        {
            if (TryParse(argText, out uint a)) return a;
            if (TryParse(fileText, out uint f)) return f;
            return configId != 0 ? configId : SpacewarTestAppId;
        }

        /// <summary>Parses the first token of <paramref name="text"/> as a non-zero App ID.</summary>
        public static bool TryParse(string text, out uint id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            int ws = t.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            if (ws >= 0) t = t.Substring(0, ws);
            return uint.TryParse(t, out id) && id != 0;
        }

        /// <summary>Resolves using the real process environment. Never throws.</summary>
        public static uint ResolveForProcess(uint configId)
        {
            string file = null;
            try
            {
                foreach (var dir in CandidateDirs())
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    string p = Path.Combine(dir, FileName);
                    if (File.Exists(p)) { file = File.ReadAllText(p); break; }
                }
            }
            catch (Exception e) { NetLog.Warn("appid-file", "Couldn't read " + FileName + ": " + e.Message); }
            uint id = Resolve(configId, file, SessionService.Arg("-steamAppId"));
            if (id == SpacewarTestAppId) NetLog.Info("Steam App ID 480 (Spacewar test app). TODO(owner): set the real App ID in NetworkingConfig.");
            return id;
        }

        static string[] CandidateDirs()
        {
            string exeDir = null;
            try
            {
                // Windows: <Game>_Data -> exe dir. macOS: .app/Contents -> folder containing the .app.
                var data = new DirectoryInfo(Application.dataPath);
                exeDir = Application.platform == RuntimePlatform.OSXPlayer ? data.Parent?.Parent?.FullName : data.Parent?.FullName;
            }
            catch (Exception) { }
            return new[] { Environment.CurrentDirectory, exeDir };
        }
    }
}
