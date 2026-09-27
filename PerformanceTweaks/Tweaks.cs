using System;
using System.Collections.Generic;
using DigitalRuby.WeatherMaker;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using Object = UnityEngine.Object;
using S = SignalSimMods.PerformanceTweaks.Settings;

namespace SignalSimMods.PerformanceTweaks
{
    internal static class Tweaks
    {
        private static readonly List<ReflectionProbe> throttledProbes = new List<ReflectionProbe>();
        private static float nextProbeRender;
        private static int probeCursor;

        /// <summary>Cheap settings that the game (or NGSS every frame) may overwrite; safe to re-run often.</summary>
        public static void ApplyGlobals()
        {
            ApplyQuality();
            ApplySunShadows();
            ApplyWeatherProfiles();
            ApplyMonitorCameras();
        }

        /// <summary>Everything, including scene scans. Run on scene load and after the player changes a setting.</summary>
        public static void ApplyAll()
        {
            ApplyGlobals();
            ApplyPostProcessing();
            ApplyColorblind();
            ApplyLights();
            ApplyReflectionProbes();
            ApplyTerrain();
        }

        // ---------------------------------------------------------------- Quality

        public static bool Indoors => PlayerCollisionDetection.inDoor;

        /// <summary>LOD bias and pixel lights: capped outdoors, game values (or the Indoor overrides) inside the base.</summary>
        public static void ApplyDetailLevel()
        {
            bool indoorDetail = S.FullDetailIndoors.Value && Indoors;

            float lodOriginal = Originals.GetOriginal(null, "lodBias", QualitySettings.lodBias);
            float lodWanted = indoorDetail
                ? (S.IndoorLodBias.Value > 0f ? S.IndoorLodBias.Value : lodOriginal)
                : (S.MaxLodBias.Value > 0f ? Mathf.Min(lodOriginal, S.MaxLodBias.Value) : lodOriginal);
            if (Mathf.Approximately(lodWanted, lodOriginal)) Originals.Restore(null, "lodBias");
            else Originals.Set(null, "lodBias", () => QualitySettings.lodBias, v => QualitySettings.lodBias = v, lodWanted);

            int lightsOriginal = Originals.GetOriginal(null, "pixelLights", QualitySettings.pixelLightCount);
            int lightsWanted = indoorDetail
                ? (S.IndoorPixelLights.Value > 0 ? S.IndoorPixelLights.Value : lightsOriginal)
                : (S.MaxPixelLights.Value > 0 ? Mathf.Min(lightsOriginal, S.MaxPixelLights.Value) : lightsOriginal);
            if (lightsWanted == lightsOriginal) Originals.Restore(null, "pixelLights");
            else Originals.Set(null, "pixelLights", () => QualitySettings.pixelLightCount, v => QualitySettings.pixelLightCount = v, lightsWanted);
        }

        private static void ApplyQuality()
        {
            ApplyDetailLevel();

            if (S.DisableSoftParticles.Value)
                Originals.Set(null, "softParticles", () => QualitySettings.softParticles, v => QualitySettings.softParticles = v, false);

            if (S.LimitAnisotropic.Value && QualitySettings.anisotropicFiltering == AnisotropicFiltering.ForceEnable)
                Originals.Set(null, "aniso", () => QualitySettings.anisotropicFiltering, v => QualitySettings.anisotropicFiltering = v, AnisotropicFiltering.Enable);

            if (S.ParticleRaycastBudget.Value > 0 && QualitySettings.particleRaycastBudget > S.ParticleRaycastBudget.Value)
                Originals.Set(null, "particleRaycast", () => QualitySettings.particleRaycastBudget, v => QualitySettings.particleRaycastBudget = v, S.ParticleRaycastBudget.Value);
        }

        // ---------------------------------------------------------------- Shadows

        private static void ApplySunShadows()
        {
            bool shadowsOff = S.FixShadowsOff.Value && GameShadowsSetToOff();
            if (shadowsOff)
                Originals.Set(null, "shadows", () => QualitySettings.shadows, v => QualitySettings.shadows = v, ShadowQuality.Disable);
            else if (Originals.IsTracked(null, "shadows"))
                Originals.Restore(null, "shadows");

            float maxDist = S.MaxShadowDistance.Value;
            if (maxDist > 0f && QualitySettings.shadowDistance > maxDist)
                Originals.Set(null, "shadowDistance", () => QualitySettings.shadowDistance, v => QualitySettings.shadowDistance = v, maxDist);

            int cascades = S.ShadowCascades.Value;
            if (cascades > 0 && QualitySettings.shadowCascades > cascades)
                Originals.Set(null, "cascades", () => QualitySettings.shadowCascades, v => QualitySettings.shadowCascades = v, cascades);

            // NGSS re-applies its own fields to QualitySettings every frame, so cap the fields too.
            foreach (var ngss in Object.FindObjectsOfType<NGSS_Directional>())
            {
                var n = ngss;
                if (maxDist > 0f && n.GLOBAL_SHADOWS_DISTANCE > maxDist)
                    Originals.Set(n, "dist", () => n.GLOBAL_SHADOWS_DISTANCE, v => n.GLOBAL_SHADOWS_DISTANCE = v, maxDist);
                if (cascades > 0 && n.GLOBAL_CASCADES_COUNT > cascades)
                    Originals.Set(n, "cascades", () => n.GLOBAL_CASCADES_COUNT, v => n.GLOBAL_CASCADES_COUNT = v, cascades);

                switch (S.SunShadowQuality.Value)
                {
                    case SunShadowFilter.Hard:
                        Originals.Set(n, "hard", () => n.NGSS_HARD_SHADOWS, v => n.NGSS_HARD_SHADOWS = v, true);
                        break;
                    case SunShadowFilter.Reduced:
                        Originals.Set(n, "pcss", () => n.NGSS_PCSS_ENABLED, v => n.NGSS_PCSS_ENABLED = v, false);
                        if (n.NGSS_FILTER_SAMPLERS > 16)
                            Originals.Set(n, "filter", () => n.NGSS_FILTER_SAMPLERS, v => n.NGSS_FILTER_SAMPLERS = v, 16);
                        if (n.NGSS_TEST_SAMPLERS > 8)
                            Originals.Set(n, "test", () => n.NGSS_TEST_SAMPLERS, v => n.NGSS_TEST_SAMPLERS = v, 8);
                        break;
                }
            }

            foreach (var contact in Object.FindObjectsOfType<NGSS_ContactShadows>())
            {
                var c = contact;
                if (shadowsOff)
                    Originals.Set(c, "enabled", () => c.enabled, v => c.enabled = v, false);
                int samples = S.ContactShadowSamples.Value;
                if (samples > 0 && c.m_raySamples > samples)
                    Originals.Set(c, "samples", () => c.m_raySamples, v => c.m_raySamples = v, samples);
            }
        }

        private static bool GameShadowsSetToOff()
        {
            var sm = PerformanceTweaksPlugin.SettingsManager;
            if (sm == null) return false;
            try
            {
                var dropdown = HarmonyLib.Traverse.Create(sm).Field("shadowsDropdown").GetValue<UnityEngine.UI.Dropdown>();
                return dropdown != null && dropdown.value == 3;
            }
            catch { return false; }
        }

        private static void ApplyLights()
        {
            if (!S.DisableLampShadows.Value) return;
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                var l = light;
                if (l.type == LightType.Directional || l.shadows == LightShadows.None) continue;
                Originals.Set(l, "shadows", () => l.shadows, v => l.shadows = v, LightShadows.None);
            }
        }

        // ---------------------------------------------------------------- Weather Maker

        private static void ApplyWeatherProfiles()
        {
            var profiles = new HashSet<WeatherMakerPerformanceProfileScript>();
            if (WeatherMakerScript.Instance != null && WeatherMakerScript.Instance.PerformanceProfile != null)
                profiles.Add(WeatherMakerScript.Instance.PerformanceProfile);
            if (PerformanceTweaksPlugin.SettingsManager != null && PerformanceTweaksPlugin.SettingsManager.performanceProf != null)
                profiles.Add(PerformanceTweaksPlugin.SettingsManager.performanceProf);

            foreach (var profile in profiles) ApplyWeatherProfile(profile);
        }

        private static void ApplyWeatherProfile(WeatherMakerPerformanceProfileScript p)
        {
            float q = Mathf.Clamp(S.CloudQuality.Value, 0.1f, 1f);
            if (q < 1f)
            {
                var range = p.VolumetricCloudSampleCount;
                var scaled = new RangeOfIntegers
                {
                    Minimum = Mathf.Max(8, Mathf.RoundToInt(range.Minimum * q)),
                    Maximum = Mathf.Max(16, Mathf.RoundToInt(range.Maximum * q))
                };
                if (!Originals.IsTracked(p, "cloudSamples") && (scaled.Minimum < range.Minimum || scaled.Maximum < range.Maximum))
                    Originals.Set(p, "cloudSamples", () => p.VolumetricCloudSampleCount, v => p.VolumetricCloudSampleCount = v, scaled);

                ScaleInt(p, "dirLightSamples", () => p.VolumetricCloudDirLightSampleCount, v => p.VolumetricCloudDirLightSampleCount = v, q, 2);
                ScaleInt(p, "cloudShadowSamples", () => p.VolumetricCloudShadowSampleCount, v => p.VolumetricCloudShadowSampleCount = v, q, 2);
                ScaleInt(p, "dirLightRaySamples", () => p.VolumetricCloudDirLightRaySampleCount, v => p.VolumetricCloudDirLightRaySampleCount = v, q, 4);
                if (q <= 0.5f)
                    Originals.Set(p, "dirLightDetails", () => p.VolumetricCloudDirLightSampleDetails, v => p.VolumetricCloudDirLightSampleDetails = v, false);
            }

            CapInt(p, "cloudShadowTex", () => p.VolumetricCloudShadowTextureSize, v => p.VolumetricCloudShadowTextureSize = v, S.CloudShadowTextureSize.Value);
            CapInt(p, "reflectionTex", () => p.ReflectionTextureSize, v => p.ReflectionTextureSize = v, S.WeatherReflectionSize.Value);
            if (S.WeatherReflectionSize.Value > 0)
            {
                Originals.Set(p, "reflectionShadows", () => p.ReflectionShadows, v => p.ReflectionShadows = v, ShadowQuality.Disable);
                Originals.Set(p, "cloudReflections", () => p.VolumetricCloudAllowReflections, v => p.VolumetricCloudAllowReflections = v, false);
            }

            if (S.DisableFogLights.Value)
                Originals.Set(p, "fogLights", () => p.EnableFogLights, v => p.EnableFogLights = v, false);

            int cap = S.FogSampleCap.Value;
            CapInt(p, "fogNoise", () => p.FogNoiseSampleCount, v => p.FogNoiseSampleCount = v, cap);
            CapInt(p, "fogShafts", () => p.FogFullScreenSunShaftSampleCount, v => p.FogFullScreenSunShaftSampleCount = v, cap);
            CapInt(p, "atmoShafts", () => p.AtmosphericLightShaftSampleCount, v => p.AtmosphericLightShaftSampleCount = v, cap);
            CapInt(p, "aurora", () => p.AuroraSampleCount, v => p.AuroraSampleCount = v, cap);
            CapInt(p, "auroraSub", () => p.AuroraSubSampleCount, v => p.AuroraSubSampleCount = v, cap > 0 ? Mathf.Max(1, cap / 8) : 0);

            if (S.DisableVolumetricClouds.Value)
                Originals.Set(p, "clouds", () => p.EnableVolumetricClouds, v => p.EnableVolumetricClouds = v, false);
        }

        private static void CapInt(Object owner, string key, Func<int> get, Action<int> set, int cap)
        {
            if (cap > 0 && get() > cap) Originals.Set(owner, key, get, set, cap);
        }

        private static void ScaleInt(Object owner, string key, Func<int> get, Action<int> set, float scale, int min)
        {
            if (Originals.IsTracked(owner, key)) return; // already scaled once; don't compound
            int current = get();
            int scaled = Mathf.Max(min, Mathf.RoundToInt(current * scale));
            if (scaled < current) Originals.Set(owner, key, get, set, scaled);
        }

        // ---------------------------------------------------------------- Cameras / post-processing

        private static void ApplyMonitorCameras()
        {
            var proj = GlobalRef.ProjectionOptimization;
            float cap = S.MonitorCameraFps.Value;
            if (proj != null && cap > 0f && proj.fps > cap)
                Originals.Set(proj, "fps", () => proj.fps, v => proj.fps = v, cap);
        }

        private static void ApplyColorblind()
        {
            if (!S.SkipIdleColorblindFilter.Value) return;
            foreach (var filter in Object.FindObjectsOfType<Wilberforce.Colorblind>())
                SyncColorblind(filter);
        }

        public static void SyncColorblind(Wilberforce.Colorblind cb)
        {
            if (cb == null) return;
            if (cb.Type == 0)
                Originals.Set(cb, "enabled", () => cb.enabled, v => cb.enabled = v, false);
            else if (Originals.IsTracked(cb, "enabled"))
                Originals.Restore(cb, "enabled"); // player picked a real colorblind mode: put the filter back
        }

        private static void ApplyPostProcessing()
        {
            foreach (var layer in Object.FindObjectsOfType<PostProcessLayer>())
            {
                var l = layer;
                var mode = l.antialiasingMode;
                switch (S.AntiAliasing.Value)
                {
                    case AntiAliasingOverride.DowngradeToFXAA:
                        if (mode == PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing || mode == PostProcessLayer.Antialiasing.TemporalAntialiasing)
                            Originals.Set(l, "aa", () => l.antialiasingMode, v => l.antialiasingMode = v, PostProcessLayer.Antialiasing.FastApproximateAntialiasing);
                        break;
                    case AntiAliasingOverride.ForceNone:
                        Originals.Set(l, "aa", () => l.antialiasingMode, v => l.antialiasingMode = v, PostProcessLayer.Antialiasing.None);
                        break;
                }
                if (l.antialiasingMode == PostProcessLayer.Antialiasing.FastApproximateAntialiasing)
                    Originals.Set(l, "fxaaFast", () => l.fastApproximateAntialiasing.fastMode, v => l.fastApproximateAntialiasing.fastMode = v, true);
            }

            foreach (var volume in Object.FindObjectsOfType<PostProcessVolume>())
            {
                // Use .profile like the game does so we edit the same instance its menu toggles edit.
                PostProcessProfile profile;
                try { profile = volume.profile; } catch { continue; }
                if (profile == null) continue;

                if (S.DisableMotionBlur.Value) DisableEffect<MotionBlur>(profile);
                if (S.ForceDisableAmbientOcclusion.Value) DisableEffect<AmbientOcclusion>(profile);
                if (S.ForceDisableScreenSpaceReflections.Value) DisableEffect<ScreenSpaceReflections>(profile);
            }
        }

        private static void DisableEffect<T>(PostProcessProfile profile) where T : PostProcessEffectSettings
        {
            if (!profile.TryGetSettings<T>(out var fx) || fx == null) return;
            Originals.Set(profile, typeof(T).Name, () => fx.enabled.value, v => fx.enabled.value = v, false);
        }

        // ---------------------------------------------------------------- Reflection probes

        private static void ApplyReflectionProbes()
        {
            throttledProbes.RemoveAll(p => p == null);
            if (S.ReflectionProbeInterval.Value <= 0f) return;

            foreach (var probe in Object.FindObjectsOfType<ReflectionProbe>())
            {
                var p = probe;
                if (p.mode != ReflectionProbeMode.Realtime || p.refreshMode != ReflectionProbeRefreshMode.EveryFrame) continue;
                Originals.Set(p, "refresh", () => p.refreshMode, v => p.refreshMode = v, ReflectionProbeRefreshMode.ViaScripting);
                Originals.Set(p, "slicing", () => p.timeSlicingMode, v => p.timeSlicingMode = v, ReflectionProbeTimeSlicingMode.IndividualFaces);
                if (!throttledProbes.Contains(p)) throttledProbes.Add(p);
                p.RenderProbe();
            }
            if (throttledProbes.Count > 0)
                PerformanceTweaksPlugin.Log.LogInfo($"Throttled {throttledProbes.Count} realtime reflection probe(s).");
        }

        /// <summary>Called every frame; refreshes one throttled probe at a time so the cost is spread out.</summary>
        public static void TickReflectionProbes()
        {
            if (throttledProbes.Count == 0 || Time.unscaledTime < nextProbeRender) return;
            float interval = Mathf.Max(0.25f, S.ReflectionProbeInterval.Value);
            nextProbeRender = Time.unscaledTime + interval / throttledProbes.Count;

            probeCursor = (probeCursor + 1) % throttledProbes.Count;
            var probe = throttledProbes[probeCursor];
            if (probe == null) { throttledProbes.RemoveAt(probeCursor); return; }
            if (probe.isActiveAndEnabled && probe.refreshMode == ReflectionProbeRefreshMode.ViaScripting)
                probe.RenderProbe();
        }

        public static void ForgetProbes() => throttledProbes.Clear();

        // ---------------------------------------------------------------- Terrain

        private static void ApplyTerrain()
        {
            foreach (var terrain in Terrain.activeTerrains)
            {
                var t = terrain;
                if (t == null) continue;
                if (S.TerrainMinPixelError.Value > 0f && t.heightmapPixelError < S.TerrainMinPixelError.Value)
                    Originals.Set(t, "pixelError", () => t.heightmapPixelError, v => t.heightmapPixelError = v, S.TerrainMinPixelError.Value);
                CapFloat(t, "basemap", () => t.basemapDistance, v => t.basemapDistance = v, S.TerrainMaxBasemapDistance.Value);
                CapFloat(t, "treeDist", () => t.treeDistance, v => t.treeDistance = v, S.TerrainMaxTreeDistance.Value);
                CapFloat(t, "billboard", () => t.treeBillboardDistance, v => t.treeBillboardDistance = v, S.TerrainMaxBillboardStart.Value);
                CapFloat(t, "detailDist", () => t.detailObjectDistance, v => t.detailObjectDistance = v, S.TerrainMaxDetailDistance.Value);
                CapFloat(t, "detailDensity", () => t.detailObjectDensity, v => t.detailObjectDensity = v, S.TerrainMaxDetailDensity.Value);
            }
        }

        private static void CapFloat(Object owner, string key, Func<float> get, Action<float> set, float cap)
        {
            if (cap > 0f && get() > cap) Originals.Set(owner, key, get, set, cap);
        }
    }
}
