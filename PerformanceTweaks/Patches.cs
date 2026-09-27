using System;
using HarmonyLib;

namespace SignalSimMods.PerformanceTweaks
{
    /// <summary>
    /// Re-applies our caps right after the game applies one of its own graphics settings, so the
    /// in-game menu keeps working and our limits sit on top of whatever the player picks.
    /// Patched by name one at a time so a game update that renames one handler doesn't break the rest.
    /// </summary>
    internal static class Patches
    {
        private static readonly string[] SettingsHandlers =
        {
            "LoadSettings",
            "OnShadowsChange",
            "OnVolDropdown",
            "OnTerrainDropdownChange",
            "OnFXAAToggle",
            "OnAoToggle",
            "OnSSRToggle",
            "OnCBChange",
            "OnFogToggle",
        };

        // Handlers to re-run after a live "tweaks off" so game values match the menu again.
        private static readonly string[] ReapplyHandlers =
        {
            "OnShadowsChange",
            "OnVolDropdown",
            "OnTerrainDropdownChange",
            "OnFXAAToggle",
            "OnAoToggle",
            "OnSSRToggle",
            "OnCBChange",
        };

        public static void Apply(Harmony harmony)
        {
            var postfix = new HarmonyMethod(AccessTools.Method(typeof(Patches), nameof(SettingsPostfix)));
            foreach (var name in SettingsHandlers)
            {
                var method = AccessTools.Method(typeof(global::SettingsManager), name);
                if (method == null)
                {
                    PerformanceTweaksPlugin.Log.LogWarning($"SettingsManager.{name} not found; skipping hook.");
                    continue;
                }
                try { harmony.Patch(method, postfix: postfix); }
                catch (Exception e) { PerformanceTweaksPlugin.Log.LogWarning($"Could not hook SettingsManager.{name}: {e.Message}"); }
            }
        }

        private static void SettingsPostfix(global::SettingsManager __instance)
        {
            PerformanceTweaksPlugin.SettingsManager = __instance;
            PerformanceTweaksPlugin.RequestApply();
        }

        public static void ReapplyGameSettings()
        {
            var sm = PerformanceTweaksPlugin.SettingsManager;
            if (sm == null) return;
            foreach (var name in ReapplyHandlers)
            {
                try { AccessTools.Method(typeof(global::SettingsManager), name)?.Invoke(sm, null); }
                catch (Exception e)
                {
                    // Expected in menus where e.g. the in-game camera doesn't exist yet.
                    PerformanceTweaksPlugin.Log.LogDebug($"Re-running {name} failed: {e.InnerException?.Message ?? e.Message}");
                }
            }
        }
    }
}
