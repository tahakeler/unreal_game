using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>Where a loaded run resumes.</summary>
    public enum RunResumePoint : byte { Shop = 0, WeekStart = 1 }

    /// <summary>
    /// Host Run Save payload (save-persistence.md §3, trimmed to what exists in the slice): version, calendar,
    /// wallet + IOU, quota, owned upgrades (+price paid), landlord rules, run stats. Versioned JSON.
    /// </summary>
    [Serializable]
    public sealed class RunSaveData
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public string SavedAtUtc;
        public RunResumePoint Resume;
        public int Week = 1, DayOfWeek = 1, DayIndex;
        public int Balance, Iou, WeekIncome, Quota, Players = 1;
        public List<string> Upgrades = new List<string>();
        public List<int> UpgradePaid = new List<int>();
        public List<string> LandlordRules = new List<string>();
        public List<string> Offers = new List<string>();
        public int TotalEarned, TotalSpentUpgrades, DaysPlayed, Seed;
    }

    /// <summary>
    /// Atomic, checksummed save file I/O. File = first line "HPSAVE &lt;crc32hex&gt;", then JSON. Writes temp + rename and
    /// keeps one <c>.bak</c>; load falls back to the backup on a bad checksum; future versions are refused.
    /// </summary>
    public static class RunSaveStore
    {
        public const string FileName = "run_slot0.json";
        const string Magic = "HPSAVE ";

        /// <summary>Override for tests; default <c>Application.persistentDataPath</c>.</summary>
        public static string DirectoryOverride;
        public static string Dir => string.IsNullOrEmpty(DirectoryOverride) ? Application.persistentDataPath : DirectoryOverride;
        public static string PathFor(string dir = null) => Path.Combine(dir ?? Dir, FileName);

        public static string Serialize(RunSaveData d)
        {
            string json = JsonUtility.ToJson(d, true);
            return Magic + Crc32(json).ToString("x8") + "\n" + json;
        }

        /// <summary>Parses text; returns null for bad header/checksum or unsupported version.</summary>
        public static RunSaveData Deserialize(string text, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(text) || !text.StartsWith(Magic, StringComparison.Ordinal)) { error = "not a save"; return null; }
            int nl = text.IndexOf('\n');
            if (nl < 0) { error = "truncated"; return null; }
            string crc = text.Substring(Magic.Length, nl - Magic.Length).Trim();
            string json = text.Substring(nl + 1);
            if (!string.Equals(crc, Crc32(json).ToString("x8"), StringComparison.OrdinalIgnoreCase)) { error = "checksum"; return null; }
            RunSaveData d;
            try { d = JsonUtility.FromJson<RunSaveData>(json); } catch (Exception e) { error = e.Message; return null; }
            if (d == null) { error = "empty"; return null; }
            if (d.Version > RunSaveData.CurrentVersion) { error = "This farm was saved by a newer build."; return null; }
            // v_n -> v_n+1 migrations go here.
            d.Version = RunSaveData.CurrentVersion;
            return d;
        }

        public static bool Exists(string dir = null) => File.Exists(PathFor(dir)) || File.Exists(PathFor(dir) + ".bak");

        public static bool Write(RunSaveData d, string dir = null)
        {
            try
            {
                string path = PathFor(dir);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                d.SavedAtUtc = DateTime.UtcNow.ToString("o");
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, Serialize(d), Encoding.UTF8);
                if (File.Exists(path)) File.Copy(path, path + ".bak", true);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[Economy] Run save failed (will retry next boundary): " + e.Message); return false; }
        }

        public static RunSaveData Load(out string error, string dir = null)
        {
            error = null;
            string path = PathFor(dir);
            foreach (var p in new[] { path, path + ".bak" })
            {
                if (!File.Exists(p)) continue;
                var d = Deserialize(File.ReadAllText(p, Encoding.UTF8), out error);
                if (d != null) return d;
            }
            error = error ?? "no save";
            return null;
        }

        public static void Delete(string dir = null)
        {
            string path = PathFor(dir);
            foreach (var p in new[] { path, path + ".bak", path + ".tmp" }) try { if (File.Exists(p)) File.Delete(p); } catch { }
        }

        static uint[] _table;
        public static uint Crc32(string s)
        {
            if (_table == null)
            {
                _table = new uint[256];
                for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; _table[i] = c; }
            }
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in Encoding.UTF8.GetBytes(s)) crc = _table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return ~crc;
        }
    }
}
