using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SignalSimMods.SaveSlots
{
    /// <summary>
    /// Several independent save files. A SAVE FILES button on the main menu lists them: pick one, then
    /// CONTINUE / NEW GAME work on that save only. Each save keeps its own difficulty.
    /// </summary>
    [BepInPlugin(Guid, "Save Slots", Version)]
    public class SaveSlotsPlugin : BaseUnityPlugin
    {
        public const string Guid = "signalsim.saveslots";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            SlotStore.ApplySelection();
            new Harmony(Guid).PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo("Save Slots loaded. " + SlotStore.Slots.Count + " save file(s); using \"" + SlotStore.Current.displayName + "\" (" + Global.SaveName + ".dat).");
        }

        /// <summary>Before the menu checks whether there is a game to CONTINUE, make sure it looks at the chosen slot.</summary>
        [HarmonyPatch(typeof(MenuControl), "Start")]
        private static class MenuStartPatch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                SlotStore.ApplySelection();
            }

            [HarmonyPostfix]
            private static void Postfix(MenuControl __instance)
            {
                if (__instance.InGame) return;
                if (__instance.GetComponent<SaveSlotsMenu>() == null) __instance.gameObject.AddComponent<SaveSlotsMenu>();
            }
        }

        /// <summary>The save is read when the game scene starts (Global.Awake -> LoadGame).</summary>
        [HarmonyPatch(typeof(Global), nameof(Global.LoadGame))]
        private static class LoadPatch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                SlotStore.ApplySelection();
                SaveSlotsPlugin.Log.LogInfo("Loading save file \"" + SlotStore.Current.displayName + "\" (" + Global.SaveName + ".dat).");
            }
        }

        // Starting a game (Continue, New Game, or confirming New Game over an existing save): use the slot's difficulty.
        [HarmonyPatch(typeof(MenuControl), nameof(MenuControl.OnContinueButtonClick))]
        private static class ContinuePatch { [HarmonyPrefix] private static void Prefix() { SlotStore.ApplySelection(); SlotStore.ApplyDifficulty(); } }

        [HarmonyPatch(typeof(MenuControl), nameof(MenuControl.OnStartButtonClicked))]
        private static class StartPatch { [HarmonyPrefix] private static void Prefix() { SlotStore.ApplySelection(); SlotStore.ApplyDifficulty(); } }

        [HarmonyPatch(typeof(MenuControl), nameof(MenuControl.OnYesButtonClicked))]
        private static class NewGamePatch { [HarmonyPrefix] private static void Prefix() { SlotStore.ApplySelection(); SlotStore.ApplyDifficulty(); } }

        // Difficulty picked in the menu: remember it for the selected save.
        [HarmonyPatch(typeof(MenuControl), "SelectEasyMode")]
        private static class EasyPatch { [HarmonyPostfix] private static void Postfix() { SlotStore.RecordDifficulty(1); } }

        [HarmonyPatch(typeof(MenuControl), "SelectNormalMode")]
        private static class NormalPatch { [HarmonyPostfix] private static void Postfix() { SlotStore.RecordDifficulty(0); } }

        [HarmonyPatch(typeof(MenuControl), "SelectHardMode")]
        private static class HardPatch { [HarmonyPostfix] private static void Postfix() { SlotStore.RecordDifficulty(2); } }
    }
}
