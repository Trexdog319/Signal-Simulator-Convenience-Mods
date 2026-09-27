using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SignalSimMods.SignalPlayback
{
    /// <summary>
    /// The laptop's signal database can only PLAY/STOP a recorded signal from the start. This adds a seek
    /// bar with elapsed / total time, pause, skip back / forward and loop to that screen, so you can see
    /// how long a signal is and replay any part of it.
    /// </summary>
    [BepInPlugin(Guid, "Signal Playback", Version)]
    public class SignalPlaybackPlugin : BaseUnityPlugin
    {
        public const string Guid = "signalsim.signalplayback";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> SkipSeconds;
        internal static ConfigEntry<bool> Loop;
        internal static ConfigEntry<KeyboardShortcut> SkipBackKey;
        internal static ConfigEntry<KeyboardShortcut> SkipForwardKey;
        internal static ConfigEntry<KeyboardShortcut> PauseKey;

        private void Awake()
        {
            Log = Logger;
            SkipSeconds = Config.Bind("Playback", "SkipSeconds", 5f,
                new ConfigDescription("How far the skip back / forward buttons and keys jump, in seconds.",
                    new AcceptableValueRange<float>(1f, 60f)));
            Loop = Config.Bind("Playback", "Loop", false,
                "Replay the signal from the start when it ends. Also toggled with the LOOP button (remembered).");
            SkipBackKey = Config.Bind("Keys", "SkipBackKey", new KeyboardShortcut(KeyCode.LeftArrow),
                "Skip back while the signal database is open.");
            SkipForwardKey = Config.Bind("Keys", "SkipForwardKey", new KeyboardShortcut(KeyCode.RightArrow),
                "Skip forward while the signal database is open.");
            PauseKey = Config.Bind("Keys", "PauseKey", new KeyboardShortcut(KeyCode.P),
                "Pause / resume while the signal database is open.");

            new Harmony(Guid).PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo("Signal Playback loaded.");
        }

        /// <summary>DBMain finds all of its UI in Awake/Start, so the controls are added right after.</summary>
        [HarmonyPatch(typeof(DBMain), "Start")]
        private static class AddControlsPatch
        {
            [HarmonyPostfix]
            private static void Postfix(DBMain __instance)
            {
                try { PlaybackControls.Attach(__instance); }
                catch (System.Exception e) { Log.LogError("Could not add playback controls: " + e); }
            }
        }

        [HarmonyPatch(typeof(DBMain), "OnDBbuttonClicked")]
        private static class SignalSelectedPatch
        {
            [HarmonyPostfix]
            private static void Postfix(DBMain __instance)
            {
                var controls = __instance.GetComponent<PlaybackControls>();
                if (controls != null) controls.OnSignalSelected();
            }
        }

        /// <summary>
        /// DBaudioPlay toggles on AudioSource.isPlaying, which is false while paused, so the game would
        /// restart the signal from 0:00. While paused, its STOP button now simply stops.
        /// </summary>
        [HarmonyPatch(typeof(DBMain), nameof(DBMain.DBaudioPlay))]
        private static class PlayStopPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(DBMain __instance, ref IEnumerator __result)
            {
                var controls = __instance.GetComponent<PlaybackControls>();
                if (controls == null || !controls.Paused) return true;
                controls.StopFromPause();
                __result = Nothing();
                return false;
            }

            private static IEnumerator Nothing()
            {
                yield break;
            }
        }
    }
}
