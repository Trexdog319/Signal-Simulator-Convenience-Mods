using System.Collections;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using S = SignalSimMods.PerformanceTweaks.Settings;

namespace SignalSimMods.PerformanceTweaks
{
    [BepInPlugin(Guid, Name, Version)]
    public class PerformanceTweaksPlugin : BaseUnityPlugin
    {
        public const string Guid = "signalsim.performancetweaks";
        public const string Name = "Performance Tweaks";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static PerformanceTweaksPlugin Instance;
        internal static global::SettingsManager SettingsManager;

        private const float GlobalsInterval = 5f;

        private bool applyRequested;
        private float nextGlobalsApply;
        private bool active;
        private bool wasIndoors;

        // Background throttle
        private bool throttled;
        private int savedTargetFps;
        private int savedVSync;

        // Overlay
        private bool showOverlay;
        private float smoothedDelta = 1f / 60f;
        private float worstDelta;
        private float windowWorst;
        private float worstResetAt;
        private GUIStyle overlayStyle;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            S.Bind(Config);

            var harmony = new Harmony(Guid);
            Patches.Apply(harmony);

            SceneManager.sceneLoaded += OnSceneLoaded;
            active = S.Enabled.Value;
            Log.LogInfo($"{Name} loaded. Tweaks {(active ? "ON" : "OFF")}. {S.ToggleKey.Value} = toggle, {S.OverlayKey.Value} = FPS overlay.");
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        internal static void RequestApply()
        {
            if (Instance != null) Instance.applyRequested = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Tweaks.ForgetProbes();
            if (mode == LoadSceneMode.Single) SettingsManager = null;
            StartCoroutine(ApplyAfterSceneStart());
        }

        private IEnumerator ApplyAfterSceneStart()
        {
            // Game scripts apply their own saved settings in Start/LoadSettings and some via Invoke a
            // couple of seconds later, so apply once shortly after load and again once things settle.
            yield return new WaitForSecondsRealtime(1f);
            applyRequested = true;
            yield return new WaitForSecondsRealtime(4f);
            applyRequested = true;
        }

        private void Update()
        {
            if (S.ToggleKey.Value.IsDown()) SetActive(!active);
            if (S.OverlayKey.Value.IsDown()) showOverlay = !showOverlay;

            if (!active) return;

            if (SettingsManager == null) SettingsManager = FindObjectOfType<global::SettingsManager>();

            if (applyRequested)
            {
                applyRequested = false;
                SafeApply(full: true);
                nextGlobalsApply = Time.unscaledTime + GlobalsInterval;
            }
            else if (Time.unscaledTime >= nextGlobalsApply)
            {
                SafeApply(full: false);
                nextGlobalsApply = Time.unscaledTime + GlobalsInterval;
            }

            // Switch detail level the moment the player crosses the base door, not on the next 5 s tick.
            bool indoors = Tweaks.Indoors;
            if (indoors != wasIndoors)
            {
                wasIndoors = indoors;
                Tweaks.ApplyDetailLevel();
            }

            Tweaks.TickReflectionProbes();
        }

        private static void SafeApply(bool full)
        {
            try
            {
                if (full) Tweaks.ApplyAll();
                else Tweaks.ApplyGlobals();
            }
            catch (System.Exception e)
            {
                Log.LogWarning("Applying tweaks failed (will retry): " + e);
            }
        }

        private void SetActive(bool on)
        {
            active = on;
            if (on)
            {
                applyRequested = true;
                Log.LogInfo("Tweaks ON");
            }
            else
            {
                Originals.RestoreAll();
                Tweaks.ForgetProbes();
                Patches.ReapplyGameSettings();
                Log.LogInfo("Tweaks OFF (game values restored)");
            }
        }

        // ---------------------------------------------------------------- Background throttle

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && !throttled && S.BackgroundFps.Value > 0 && active)
            {
                savedTargetFps = Application.targetFrameRate;
                savedVSync = QualitySettings.vSyncCount;
                QualitySettings.vSyncCount = 0; // vsync overrides targetFrameRate
                Application.targetFrameRate = S.BackgroundFps.Value;
                throttled = true;
            }
            else if (focused && throttled)
            {
                QualitySettings.vSyncCount = savedVSync;
                Application.targetFrameRate = savedTargetFps;
                throttled = false;
            }
        }

        // ---------------------------------------------------------------- Overlay

        private void LateUpdate()
        {
            if (!showOverlay) return;
            float dt = Time.unscaledDeltaTime;
            smoothedDelta = Mathf.Lerp(smoothedDelta, dt, 0.05f);
            windowWorst = Mathf.Max(windowWorst, dt);
            if (Time.unscaledTime >= worstResetAt)
            {
                // Publish the slowest frame of the last 2 s window, then start a new window.
                worstDelta = windowWorst;
                windowWorst = 0f;
                worstResetAt = Time.unscaledTime + 2f;
            }
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            if (overlayStyle == null)
            {
                overlayStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft };
                overlayStyle.normal.textColor = Color.white;
            }
            string text = $"{1f / smoothedDelta:0} FPS  {smoothedDelta * 1000f:0.0} ms\n" +
                          $"worst (2s): {worstDelta * 1000f:0.0} ms\n" +
                          $"Perf tweaks: {(active ? "ON" : "OFF")}  [{S.ToggleKey.Value}]";
            GUI.Box(new Rect(10, 10, 230, 62), text, overlayStyle);
        }
    }
}
