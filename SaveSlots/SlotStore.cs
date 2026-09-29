using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace SignalSimMods.SaveSlots
{
    internal class SlotInfo
    {
        public string saveName;      // file name without ".dat"; the game reads/writes <persistentDataPath>/<saveName>.dat
        public string displayName;
        public int difficulty = -1;  // game's difficultyMode: 0 normal, 1 easy, 2 hard; -1 = not recorded yet
    }

    internal class SlotList
    {
        public List<SlotInfo> slots = new List<SlotInfo>();
        public string selected;
    }

    /// <summary>
    /// The list of save files. Every save read/write in the game goes through Global.SaveName, so selecting a
    /// slot is just pointing that at the slot's file. Slot 1 keeps the game's original "save" name, so the
    /// existing save stays where it was and still works if the mod is removed.
    /// </summary>
    internal static class SlotStore
    {
        public const string OriginalSaveName = "save";
        private const string ExtraPrefix = "save_slot";

        private static SlotList data;
        private static readonly Dictionary<string, KeyValuePair<DateTime, string>> summaryCache = new Dictionary<string, KeyValuePair<DateTime, string>>();

        private static string DataDir { get { return Application.persistentDataPath; } }
        private static string MetaDir { get { return Path.Combine(DataDir, "SignalSimMods"); } }
        private static string MetaPath { get { return Path.Combine(MetaDir, "saveslots.txt"); } }
        // Written by 1.0.0 with Unity's JsonUtility, which silently dropped the slot list (only "selected" survived).
        private static string LegacyJsonPath { get { return Path.Combine(MetaDir, "saveslots.json"); } }

        public static List<SlotInfo> Slots { get { Ensure(); return data.slots; } }

        public static SlotInfo Current
        {
            get
            {
                Ensure();
                foreach (var s in data.slots) if (s.saveName == data.selected) return s;
                return data.slots[0];
            }
        }

        public static string SaveFile(SlotInfo s) { return Path.Combine(DataDir, s.saveName + ".dat"); }
        public static bool HasGame(SlotInfo s) { return File.Exists(SaveFile(s)); }

        // ------------------------------------------------------------------ load / save

        private static void Ensure()
        {
            if (data != null) return;
            data = new SlotList();
            try
            {
                if (File.Exists(MetaPath)) Read(File.ReadAllLines(MetaPath));
                else if (File.Exists(LegacyJsonPath))
                {
                    // Only the selection is recoverable from the old file.
                    var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(LegacyJsonPath), "\"selected\"\\s*:\\s*\"([^\"]*)\"");
                    if (m.Success) data.selected = m.Groups[1].Value;
                }
            }
            catch (Exception e)
            {
                SaveSlotsPlugin.Log.LogError("Couldn't read the save list (" + e.Message + "); rebuilding it from the save files.");
                data = new SlotList();
            }

            // Adopt save files that aren't listed: the original save on first run, or slots whose entry was lost.
            if (Find(OriginalSaveName) == null && (data.slots.Count == 0 || File.Exists(Path.Combine(DataDir, OriginalSaveName + ".dat"))))
                data.slots.Insert(0, new SlotInfo { saveName = OriginalSaveName, displayName = "Save 1" });
            foreach (var f in Directory.GetFiles(DataDir, ExtraPrefix + "*.dat"))
            {
                string name = Path.GetFileNameWithoutExtension(f);
                if (Find(name) == null) data.slots.Add(new SlotInfo { saveName = name, displayName = "Save " + name.Substring(ExtraPrefix.Length) });
            }
            if (data.selected == null || Find(data.selected) == null) data.selected = data.slots[0].saveName;
            Write();
            try { if (File.Exists(LegacyJsonPath)) File.Delete(LegacyJsonPath); } catch { }
        }

        /// <summary>
        /// Plain text, one entry per line, tab-separated:
        ///   selected  saveName
        ///   slot      saveName  difficulty  display name
        /// </summary>
        private static void Read(string[] lines)
        {
            foreach (var line in lines)
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var f = line.Split('\t');
                if (f[0] == "selected" && f.Length >= 2) data.selected = f[1];
                else if (f[0] == "slot" && f.Length >= 4 && IsValidSaveName(f[1]) && Find(f[1]) == null)
                {
                    int diff;
                    if (!int.TryParse(f[2], out diff)) diff = -1;
                    data.slots.Add(new SlotInfo { saveName = f[1], difficulty = diff, displayName = f[3].Length > 0 ? f[3] : f[1] });
                }
            }
        }

        // Only names this mod creates, so a hand-edited list can't point the game at an arbitrary path.
        private static bool IsValidSaveName(string n)
        {
            if (n == OriginalSaveName) return true;
            if (!n.StartsWith(ExtraPrefix) || n.Length == ExtraPrefix.Length) return false;
            foreach (char c in n.Substring(ExtraPrefix.Length)) if (c < '0' || c > '9') return false;
            return true;
        }

        private static SlotInfo Find(string saveName)
        {
            foreach (var s in data.slots) if (s.saveName == saveName) return s;
            return null;
        }

        private static void Write()
        {
            try
            {
                Directory.CreateDirectory(MetaDir);
                var sb = new System.Text.StringBuilder();
                sb.Append("# Signal Simulator save files (Save Slots mod). One line per save: slot, file name, difficulty, name.\n");
                sb.Append("selected\t").Append(data.selected).Append('\n');
                foreach (var s in data.slots)
                    sb.Append("slot\t").Append(s.saveName).Append('\t').Append(s.difficulty).Append('\t').Append(Clean(s.displayName)).Append('\n');
                string tmp = MetaPath + ".tmp";
                File.WriteAllText(tmp, sb.ToString());
                if (File.Exists(MetaPath)) File.Delete(MetaPath);
                File.Move(tmp, MetaPath);
            }
            catch (Exception e)
            {
                SaveSlotsPlugin.Log.LogError("Couldn't save the save list: " + e.Message);
            }
        }

        private static string Clean(string name)
        {
            return (name ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        /// <summary>Forget the in-memory list and read it back from disk (used by tests).</summary>
        internal static void ReloadFromDisk()
        {
            data = null;
            Ensure();
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Points the game at the selected slot's file. Call before anything reads the save.</summary>
        public static void ApplySelection()
        {
            Global.SaveName = Current.saveName;
        }

        public static void Select(SlotInfo s)
        {
            Ensure();
            data.selected = s.saveName;
            Write();
            ApplySelection();
            ApplyDifficulty();
        }

        /// <summary>Difficulty is a global game setting; switch it to the selected slot's (if one was recorded).</summary>
        public static void ApplyDifficulty()
        {
            var s = Current;
            if (SettingsManager.gameSettings == null) return;
            if (s.difficulty < 0)
            {
                // Older slot: adopt whatever is currently set.
                s.difficulty = SettingsManager.gameSettings.difficultyMode;
                Write();
            }
            else SettingsManager.gameSettings.difficultyMode = s.difficulty;
        }

        public static void RecordDifficulty(int mode)
        {
            var s = Current;
            if (s.difficulty == mode) return;
            s.difficulty = mode;
            Write();
        }

        public static SlotInfo Create()
        {
            Ensure();
            int n = 2;
            while (Find(ExtraPrefix + n) != null || File.Exists(Path.Combine(DataDir, ExtraPrefix + n + ".dat"))) n++;
            int label = data.slots.Count + 1;
            var s = new SlotInfo { saveName = ExtraPrefix + n, displayName = "Save " + label };
            data.slots.Add(s);
            Write();
            return s;
        }

        public static void Rename(SlotInfo s, string name)
        {
            name = Clean(name).Trim();
            if (name.Length == 0) return;
            if (name.Length > 32) name = name.Substring(0, 32);
            s.displayName = name;
            Write();
        }

        /// <summary>Deletes the slot's game for good, including other mods' data stored per save.</summary>
        public static void Delete(SlotInfo s)
        {
            Ensure();
            TryDelete(SaveFile(s));
            // Other mods keep per-save data in SignalSimMods as "<something>_<saveName>.bin"; it goes with the save.
            if (Directory.Exists(MetaDir))
                foreach (var f in Directory.GetFiles(MetaDir, "*_" + s.saveName + ".bin*")) TryDelete(f);
            data.slots.Remove(s);
            summaryCache.Remove(s.saveName);
            if (data.slots.Count == 0) data.slots.Add(new SlotInfo { saveName = OriginalSaveName, displayName = "Save 1" });
            if (Find(data.selected) == null) data.selected = data.slots[0].saveName;
            Write();
            ApplySelection();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { SaveSlotsPlugin.Log.LogError("Couldn't delete " + path + ": " + e.Message); }
        }

        // ------------------------------------------------------------------ display

        public static string DifficultyName(int mode)
        {
            return mode == 1 ? "Easy" : mode == 2 ? "Hard" : "Normal";
        }

        /// <summary>One line describing the slot's progress, read from the save itself (cached until the file changes).</summary>
        public static string Summary(SlotInfo s)
        {
            string path = SaveFile(s);
            if (!File.Exists(path)) return "Empty - press NEW GAME to start";
            DateTime written = File.GetLastWriteTime(path);
            KeyValuePair<DateTime, string> cached;
            if (summaryCache.TryGetValue(s.saveName, out cached) && cached.Key == written) return cached.Value;

            string text;
            try
            {
                SaveStruct save;
                using (var f = File.OpenRead(path)) save = (SaveStruct)new BinaryFormatter().Deserialize(f);
                int signals = save.SignalDB != null ? save.SignalDB.Count : 0;
                text = string.Format("{0} credits  |  {1} signals  |  {2}  |  played {3:d MMM yyyy, HH:mm}",
                    Mathf.FloorToInt(save.currentCreditsPoints).ToString("N0"), signals, DifficultyName(s.difficulty < 0 ? 0 : s.difficulty), written);
            }
            catch (Exception e)
            {
                SaveSlotsPlugin.Log.LogWarning("Couldn't read " + path + " for its summary: " + e.Message);
                text = string.Format("Saved game  |  played {0:d MMM yyyy, HH:mm}", written);
            }
            summaryCache[s.saveName] = new KeyValuePair<DateTime, string>(written, text);
            return text;
        }
    }
}
