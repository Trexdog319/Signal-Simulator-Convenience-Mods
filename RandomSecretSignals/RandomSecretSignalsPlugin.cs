using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SignalSimMods.Shared;
using UnityEngine;

namespace SignalSimMods.RandomSecretSignals
{
    /// <summary>
    /// Stock SignalMain.GenerateSignal only produces the secret/event signals on specific real-world
    /// days and hours (e.g. the 15th at midnight, the 28th at 22:00). With RandomEvents on, each new
    /// signal instead has a SecretEventChance% chance of being one of the not-yet-seen event signals.
    /// Each event still only completes once per save (the game's own save flags are respected), and the
    /// six-part storyline still plays in order.
    /// </summary>
    [BepInPlugin(Guid, "Random Secret Signals", Version)]
    public class RandomSecretSignalsPlugin : BaseUnityPlugin
    {
        public const string Guid = "signalsim.randomsecretsignals";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> RandomEvents;
        internal static ConfigEntry<float> SecretEventChance;
        internal static ConfigEntry<float> StoryChance;

        private void Awake()
        {
            Log = Logger;
            RandomEvents = Config.Bind("Events", "RandomEvents", true,
                "true: secret event signals appear at random (SecretEventChance per signal) instead of on set dates/times.\n" +
                "false: original date/time schedule. Also toggleable in Options > Gameplay.");
            SecretEventChance = Config.Bind("Events", "SecretEventChance", 3f,
                new ConfigDescription("Percent chance that a newly generated signal is a secret event signal (when RandomEvents is on).",
                    new AcceptableValueRange<float>(0f, 100f)));
            StoryChance = Config.Bind("Story", "StorySignalChance", 10f,
                new ConfigDescription("Percent chance that a normal (non-event) signal is a story signal: Grey aliens, Space Explorers, " +
                    "Mars Base, Missing Person. Stock game is 10. Applies in both event modes.",
                    new AcceptableValueRange<float>(0f, 100f)));

            new Harmony(Guid).PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo("Random Secret Signals loaded. Mode: " + (RandomEvents.Value ? "random (" + SecretEventChance.Value + "%)" : "original schedule"));
        }

        private struct SecretEvent
        {
            public string Name;
            public Func<bool> Available;
            public Action Generate;
        }

        private static readonly SecretEvent[] Events =
        {
            // Storyline (Event0..Event5). GenerateEventSignal picks the next part from eventsDetected.
            new SecretEvent { Name = "Storyline", Available = () => Global.saveStruct.eventsDetected <= 5, Generate = SignalGenerator.GenerateEventSignal },
            new SecretEvent { Name = "Halloween", Available = () => Global.saveStruct.halloween != DateTime.Now.Year, Generate = SignalGenerator.GenerateHalloweenEventSignal },
            new SecretEvent { Name = "Tripod", Available = () => !Global.saveStruct.tripodEvent, Generate = SignalGenerator.GenerateTripodEventSignal },
            new SecretEvent { Name = "Borg ship", Available = () => !Global.saveStruct.borgEvent, Generate = SignalGenerator.GenerateBorgSignal },
            new SecretEvent { Name = "Borg invasion", Available = () => Global.saveStruct.borgEvent && !Global.saveStruct.borgInvasion, Generate = SignalGenerator.GenerateBorgInvasion },
            new SecretEvent { Name = "SpaceX booster", Available = () => !Global.saveStruct.SpaceX, Generate = SignalGenerator.GenerateSpaceXEvent },
        };

        [HarmonyPatch]
        private static class GenerateSignalPatch
        {
            // SignalMain is internal to Assembly-CSharp, so it is reached by name.
            private static readonly Type SignalMainType = AccessTools.TypeByName("SignalMain");
            private static readonly MethodInfo SignalInformationGenerator = AccessTools.Method(SignalMainType, "SignalInformationGenerator");
            private static readonly FieldInfo InstanceField = AccessTools.Field(SignalMainType, "instance");
            private static readonly FieldInfo CoordCoroutineField = AccessTools.Field(SignalMainType, "signalCoordinateDetectionInformation");

            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(SignalMainType, "GenerateSignal");
            }

            // Mirrors SignalMain.GenerateSignal, with the date/time checks replaced by a dice roll.
            [HarmonyPrefix]
            private static bool Prefix()
            {
                if (!RandomEvents.Value) return true;

                CoordinateDetector.CanDetectCoordinates = false;
                FrequencyDetector.CoordinatesDetected = false;

                float chance = SecretEventChance.Value;
                float roll = UnityEngine.Random.value * 100f;
                string picked = null;
                if (roll < chance)
                {
                    var available = new List<SecretEvent>();
                    foreach (var ev in Events)
                        if (ev.Available()) available.Add(ev);
                    if (available.Count > 0)
                    {
                        var ev = available[UnityEngine.Random.Range(0, available.Count)];
                        ev.Generate();
                        picked = ev.Name;
                    }
                }
                if (picked == null) SignalGenerator.GenerateSignal();

                Log.LogInfo(picked != null
                    ? string.Format("Secret event signal: {0} (rolled {1:0.00} < {2}%)", picked, roll, chance)
                    : string.Format("Normal signal, type '{0}' (story chance {1}%; secret rolled {2:0.00}, chance {3}%)",
                        SignalGenerator.signalGeneratedType, StoryChance.Value, roll, chance));

                SignalInformationGenerator.Invoke(null, null);
                var instance = (MonoBehaviour)InstanceField.GetValue(null);
                CoordCoroutineField.SetValue(null, instance.StartCoroutine(CoordinateDetector.SignalCoordinateDetectionInformation()));
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
                SettingsMenu.AddToggle(container, toggleRow, "randomEvents", "Random Event Signals", RandomEvents.Value,
                    v => RandomEvents.Value = v);
                SettingsMenu.AddSlider(container, sliderRow, "eventChance", "Radio Event Chance", 0f, 100f, SecretEventChance.Value, 0.5f,
                    v => v.ToString(v < 10f ? "0.0" : "0") + "%", v => SecretEventChance.Value = v);
                SettingsMenu.AddSlider(container, sliderRow, "storyChance", "Radio Story Chance", 0f, 100f, StoryChance.Value, 0.5f,
                    v => v.ToString(v < 10f ? "0.0" : "0") + "%", v => StoryChance.Value = v);
            }
        }
    }
}
