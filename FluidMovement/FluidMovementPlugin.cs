using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SignalSimMods.Shared;
using UnityEngine;

namespace SignalSimMods.FluidMovement
{
    [BepInPlugin(Guid, "Fluid Movement", Version)]
    public class FluidMovementPlugin : BaseUnityPlugin
    {
        public const string Guid = "signalsim.fluidmovement";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        private static UnityEngine.UI.Toggle swayToggle;

        // Camera
        internal static ConfigEntry<bool> CameraSway;
        internal static ConfigEntry<float> SwayStrength;
        internal static ConfigEntry<float> HeadBobStrength;
        internal static ConfigEntry<KeyboardShortcut> ToggleSwayKey;

        // Jumping
        internal static ConfigEntry<bool> EnableJump;
        internal static ConfigEntry<KeyCode> JumpKey;
        internal static ConfigEntry<float> JumpHeight;
        internal static ConfigEntry<float> Gravity;
        internal static ConfigEntry<bool> HoldToBunnyHop;
        internal static ConfigEntry<float> AirStrafeControl;
        internal static ConfigEntry<float> AirSteering;

        // Golf kart
        internal static ConfigEntry<bool> ImprovedKart;
        internal static ConfigEntry<float> KartHighSpeedSteer;
        internal static ConfigEntry<float> MaxBunnyHopSpeed;

        // Crouching
        internal static ConfigEntry<bool> EnableCrouch;
        internal static ConfigEntry<KeyCode> CrouchKey;
        internal static ConfigEntry<KeyCode> CrouchAltKey;
        internal static ConfigEntry<bool> CrouchIsToggle;
        internal static ConfigEntry<float> CrouchHeight;
        internal static ConfigEntry<float> CrouchSpeed;

        private void Awake()
        {
            Log = Logger;

            CameraSway = Config.Bind("Camera", "CameraSway", true,
                "Head bob, strafe lean, look sway and landing dip. Also toggleable in Options > Gameplay.");
            SwayStrength = Config.Bind("Camera", "SwayStrength", 1f,
                new ConfigDescription("Multiplier for lean/look sway and landing dip.", new AcceptableValueRange<float>(0f, 2f)));
            HeadBobStrength = Config.Bind("Camera", "HeadBobStrength", 1f,
                new ConfigDescription("Multiplier for walking head bob.", new AcceptableValueRange<float>(0f, 2f)));
            ToggleSwayKey = Config.Bind("Camera", "ToggleSwayKey", KeyboardShortcut.Empty,
                "Optional hotkey to toggle camera sway in-game.");

            EnableJump = Config.Bind("Jumping", "EnableJump", true, "Allow jumping.");
            JumpKey = Config.Bind("Jumping", "JumpKey", KeyCode.Space, "Jump key (controller: the 'Jump' button).");
            JumpHeight = Config.Bind("Jumping", "JumpHeight", 1.0f,
                new ConfigDescription("Jump apex height in metres.", new AcceptableValueRange<float>(0.3f, 2f)));
            Gravity = Config.Bind("Jumping", "Gravity", 22f,
                new ConfigDescription("Gravity in m/s^2 (higher = snappier jumps).", new AcceptableValueRange<float>(9.81f, 40f)));
            HoldToBunnyHop = Config.Bind("Jumping", "HoldToBunnyHop", true,
                "Holding jump re-jumps on the frame you land (no ground friction = momentum kept). If false, you must time each press.");
            AirSteering = Config.Bind("Jumping", "AirSteering", 1f,
                new ConfigDescription("How strongly movement keys push you mid-air (adds to your jump momentum). 0 = none.", new AcceptableValueRange<float>(0f, 3f)));
            AirStrafeControl = Config.Bind("Jumping", "AirStrafeControl", 1f,
                new ConfigDescription("Air acceleration multiplier. Strafe + turn mid-air to gain speed.", new AcceptableValueRange<float>(0f, 3f)));
            MaxBunnyHopSpeed = Config.Bind("Jumping", "MaxBunnyHopSpeed", 15f,
                new ConfigDescription("Horizontal speed cap in m/s (sprint is 9).", new AcceptableValueRange<float>(9f, 40f)));

            EnableCrouch = Config.Bind("Crouching", "EnableCrouch", true, "Allow crouching.");
            CrouchKey = Config.Bind("Crouching", "CrouchKey", KeyCode.LeftControl, "Crouch key (controller: right-stick click).");
            CrouchAltKey = Config.Bind("Crouching", "CrouchAltKey", KeyCode.C, "Secondary crouch key.");
            CrouchIsToggle = Config.Bind("Crouching", "CrouchIsToggle", false, "Press to toggle crouch instead of holding.");
            CrouchHeight = Config.Bind("Crouching", "CrouchHeight", 0.55f,
                new ConfigDescription("Crouched height as a fraction of standing height.", new AcceptableValueRange<float>(0.4f, 0.9f)));
            CrouchSpeed = Config.Bind("Crouching", "CrouchSpeed", 2.2f,
                new ConfigDescription("Crouch walk speed in m/s (walk is 4).", new AcceptableValueRange<float>(1f, 4f)));

            ImprovedKart = Config.Bind("GolfKart", "ImprovedHandling", true,
                "Smoother acceleration, speed-sensitive framerate-independent steering, and no speed loss from brushing " +
                "bumps/roads. false = stock kart. Also toggleable in Options > Gameplay.");
            KartHighSpeedSteer = Config.Bind("GolfKart", "HighSpeedSteerAngle", 14f,
                new ConfigDescription("Max front-wheel angle at high speed (stock allows 45 at any speed, which scrubs speed off).",
                    new AcceptableValueRange<float>(8f, 45f)));

            CameraSway.SettingChanged += (s, e) =>
            {
                if (swayToggle != null) swayToggle.SetIsOnWithoutNotify(CameraSway.Value);
            };

            new Harmony(Guid).PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            Log.LogInfo("Fluid Movement loaded.");
        }

        private void Update()
        {
            if (ToggleSwayKey.Value.IsDown())
            {
                CameraSway.Value = !CameraSway.Value;
                Log.LogInfo("Camera sway " + (CameraSway.Value ? "on" : "off"));
            }
        }

        [HarmonyPatch(typeof(FPSControl), "Start")]
        private static class AttachControllerPatch
        {
            [HarmonyPostfix]
            private static void Postfix(FPSControl __instance)
            {
                if (__instance.GetComponent<FluidMovementController>() == null)
                    __instance.gameObject.AddComponent<FluidMovementController>();
            }
        }

        // Replaces the stock movement (constant downward push, no vertical velocity).
        [HarmonyPatch(typeof(FPSControl), "PlayerMove")]
        private static class ReplaceMovePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(FPSControl __instance)
            {
                var ctrl = __instance.GetComponent<FluidMovementController>();
                if (ctrl == null) return true;
                ctrl.Move();
                return false;
            }
        }

        [HarmonyPatch(typeof(SettingsManager), "Awake")]
        private static class SettingsPatch
        {
            [HarmonyPostfix]
            private static void Postfix(SettingsManager __instance)
            {
                Transform container;
                GameObject toggleRow, sliderRow;
                if (!SettingsMenu.TryGetTemplates(__instance, out container, out toggleRow, out sliderRow))
                {
                    Log.LogWarning("Options panel not found; use the config file instead.");
                    return;
                }
                swayToggle = SettingsMenu.AddToggle(container, toggleRow, "cameraSway", "Camera Sway", CameraSway.Value,
                    v => CameraSway.Value = v);
                SettingsMenu.AddSlider(container, sliderRow, "swayStrength", "Sway Strength", 0f, 2f, SwayStrength.Value, 0.05f,
                    v => Mathf.RoundToInt(v * 100f) + "%", v => SwayStrength.Value = v);
                SettingsMenu.AddToggle(container, toggleRow, "kartHandling", "Kart Handling", ImprovedKart.Value,
                    v => ImprovedKart.Value = v);
            }
        }
    }
}
