using BepInEx.Configuration;
using UnityEngine;

namespace SignalSimMods.PerformanceTweaks
{
    internal enum AntiAliasingOverride { Keep, DowngradeToFXAA, ForceNone }
    internal enum SunShadowFilter { Keep, Reduced, Hard }

    /// <summary>All user-tunable options. For numeric caps, 0 means "leave the game's value alone".</summary>
    internal static class Settings
    {
        // General
        public static ConfigEntry<bool> StartOn;
        public static ConfigEntry<KeyboardShortcut> ToggleKey;
        public static ConfigEntry<KeyboardShortcut> OverlayKey;
        public static ConfigEntry<int> BackgroundFps;

        // Rendering
        public static ConfigEntry<bool> SkipIdleColorblindFilter;
        public static ConfigEntry<float> MonitorCameraFps;
        public static ConfigEntry<AntiAliasingOverride> AntiAliasing;
        public static ConfigEntry<bool> DisableMotionBlur;
        public static ConfigEntry<bool> ForceDisableAmbientOcclusion;
        public static ConfigEntry<bool> ForceDisableScreenSpaceReflections;
        public static ConfigEntry<float> ReflectionProbeInterval;

        // Shadows
        public static ConfigEntry<bool> FixShadowsOff;
        public static ConfigEntry<float> MaxShadowDistance;
        public static ConfigEntry<int> ShadowCascades;
        public static ConfigEntry<SunShadowFilter> SunShadowQuality;
        public static ConfigEntry<int> ContactShadowSamples;
        public static ConfigEntry<bool> DisableLampShadows;

        // Weather (Weather Maker)
        public static ConfigEntry<float> CloudQuality;
        public static ConfigEntry<int> CloudShadowTextureSize;
        public static ConfigEntry<int> WeatherReflectionSize;
        public static ConfigEntry<bool> DisableFogLights;
        public static ConfigEntry<int> FogSampleCap;
        public static ConfigEntry<bool> DisableVolumetricClouds;

        // Terrain
        public static ConfigEntry<float> TerrainMinPixelError;
        public static ConfigEntry<float> TerrainMaxBasemapDistance;
        public static ConfigEntry<float> TerrainMaxTreeDistance;
        public static ConfigEntry<float> TerrainMaxBillboardStart;
        public static ConfigEntry<float> TerrainMaxDetailDistance;
        public static ConfigEntry<float> TerrainMaxDetailDensity;

        // Quality
        public static ConfigEntry<float> MaxLodBias;
        public static ConfigEntry<int> MaxPixelLights;
        public static ConfigEntry<bool> DisableSoftParticles;
        public static ConfigEntry<bool> LimitAnisotropic;
        public static ConfigEntry<int> ParticleRaycastBudget;

        // Indoors
        public static ConfigEntry<bool> FullDetailIndoors;
        public static ConfigEntry<float> IndoorLodBias;
        public static ConfigEntry<int> IndoorPixelLights;

        /// <summary>
        /// 1.0 had "Enabled" (default true). It no longer does anything, so drop it from existing config files
        /// rather than leave a misleading "Enabled = true" behind.
        /// </summary>
        private static void RemoveOldEnabledSetting(ConfigFile cfg)
        {
            try
            {
                var orphans = HarmonyLib.Traverse.Create(cfg).Property("OrphanedEntries")
                    .GetValue<System.Collections.Generic.Dictionary<ConfigDefinition, string>>();
                if (orphans != null && orphans.Remove(new ConfigDefinition("General", "Enabled"))) cfg.Save();
            }
            catch { /* cosmetic only */ }
        }

        public static void Bind(ConfigFile cfg)
        {
            // New key rather than a new default for "Enabled": existing configs have Enabled = true written in them.
            StartOn = cfg.Bind("General", "StartOn", false,
                "Turn the tweaks on automatically when the game starts. Off by default: press ToggleKey (F9) in-game to turn them on.");
            RemoveOldEnabledSetting(cfg);
            ToggleKey = cfg.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F9),
                "Turn all tweaks on/off in-game (restores the game's original values when off).");
            OverlayKey = cfg.Bind("General", "OverlayKey", new KeyboardShortcut(KeyCode.F10),
                "Show/hide a small FPS / frame-time overlay.");
            BackgroundFps = cfg.Bind("General", "BackgroundFps", 10,
                new ConfigDescription("Frame cap while the game window is not focused (saves CPU/GPU/heat when alt-tabbed). 0 = off.",
                    new AcceptableValueRange<int>(0, 60)));

            SkipIdleColorblindFilter = cfg.Bind("Rendering", "SkipIdleColorblindFilter", true,
                "The colorblind filter does a full-screen pass on every camera even when set to 'Normal'. Skip it in that case (no visual change).");
            MonitorCameraFps = cfg.Bind("Rendering", "MonitorCameraFps", 10f,
                new ConfigDescription("Cap how often the in-room monitor/projection cameras re-render per second (lower = cheaper, choppier screens). 0 = keep game value.",
                    new AcceptableValueRange<float>(0f, 60f)));
            AntiAliasing = cfg.Bind("Rendering", "AntiAliasing", AntiAliasingOverride.DowngradeToFXAA,
                "DowngradeToFXAA replaces SMAA/TAA with cheap FXAA. ForceNone disables AA. Keep = use the in-game setting.");
            DisableMotionBlur = cfg.Bind("Rendering", "DisableMotionBlur", true, "Force post-process motion blur off (not exposed in game menu).");
            ForceDisableAmbientOcclusion = cfg.Bind("Rendering", "ForceDisableAmbientOcclusion", false,
                "Force SSAO off regardless of the in-game toggle. Normally just use the game's own AO toggle.");
            ForceDisableScreenSpaceReflections = cfg.Bind("Rendering", "ForceDisableScreenSpaceReflections", false,
                "Force SSR off regardless of the in-game toggle. Normally just use the game's own SSR toggle.");
            ReflectionProbeInterval = cfg.Bind("Rendering", "ReflectionProbeInterval", 4f,
                new ConfigDescription("Realtime reflection probes that update every frame are switched to update once every N seconds (time-sliced). 0 = keep.",
                    new AcceptableValueRange<float>(0f, 60f)));

            FixShadowsOff = cfg.Bind("Shadows", "FixShadowsOff", true,
                "The in-game 'Off' shadow setting only zeroes the distance; shadow passes still run. This truly disables them.");
            MaxShadowDistance = cfg.Bind("Shadows", "MaxShadowDistance", 60f,
                new ConfigDescription("Cap on sun shadow distance in meters (game 'High' = 250). 0 = no cap.", new AcceptableValueRange<float>(0f, 500f)));
            ShadowCascades = cfg.Bind("Shadows", "ShadowCascades", 2,
                new ConfigDescription("Sun shadow cascades: 1, 2 or 4. Fewer = fewer shadow renders per frame. 0 = keep.", new AcceptableValueList<int>(0, 1, 2, 4)));
            SunShadowQuality = cfg.Bind("Shadows", "SunShadowFilter", SunShadowFilter.Reduced,
                "NGSS soft shadow filtering. Reduced = no PCSS, 16 samples (from 48). Hard = hard-edged shadows (cheapest). Keep = unchanged.");
            ContactShadowSamples = cfg.Bind("Shadows", "ContactShadowSamples", 16,
                new ConfigDescription("Ray samples for grass/contact shadows (game uses 64). 0 = keep.", new AcceptableValueRange<int>(0, 64)));
            DisableLampShadows = cfg.Bind("Shadows", "DisableLampShadows", true,
                "Turn off shadows from point/spot lights (each point light shadow = 6 extra scene renders). Sun shadows unaffected.");

            CloudQuality = cfg.Bind("Weather", "CloudQuality", 0.5f,
                new ConfigDescription("Multiplier on volumetric cloud / light ray-march sample counts. 1 = game default.", new AcceptableValueRange<float>(0.1f, 1f)));
            CloudShadowTextureSize = cfg.Bind("Weather", "CloudShadowTextureSize", 512,
                new ConfigDescription("Max resolution of the cloud shadow map (game: 2048). 0 = keep.", new AcceptableValueList<int>(0, 256, 512, 1024, 2048)));
            WeatherReflectionSize = cfg.Bind("Weather", "ReflectionTextureSize", 256,
                new ConfigDescription("Max resolution of Weather Maker planar reflections (game: 1024). Below 128 disables water reflections. 0 = keep.",
                    new AcceptableValueList<int>(0, 64, 128, 256, 512, 1024)));
            DisableFogLights = cfg.Bind("Weather", "DisableFogLights", true, "Skip per-light scattering inside volumetric fog.");
            FogSampleCap = cfg.Bind("Weather", "FogSampleCap", 16,
                new ConfigDescription("Cap on fog noise / light shaft / aurora ray samples. 0 = keep.", new AcceptableValueRange<int>(0, 64)));
            DisableVolumetricClouds = cfg.Bind("Weather", "DisableVolumetricClouds", false,
                "Last resort for very weak GPUs: turn volumetric clouds off entirely (big visual change).");

            TerrainMinPixelError = cfg.Bind("Terrain", "MinPixelError", 8f,
                new ConfigDescription("Terrain mesh LOD tolerance; higher = fewer triangles (Unity default 5). 0 = keep.", new AcceptableValueRange<float>(0f, 50f)));
            TerrainMaxBasemapDistance = cfg.Bind("Terrain", "MaxBasemapDistance", 150f,
                new ConfigDescription("Beyond this, terrain uses a cheap pre-baked texture instead of full splat blending. 0 = keep.", new AcceptableValueRange<float>(0f, 2000f)));
            TerrainMaxTreeDistance = cfg.Bind("Terrain", "MaxTreeDistance", 800f,
                new ConfigDescription("Max distance trees are drawn. 0 = keep.", new AcceptableValueRange<float>(0f, 5000f)));
            TerrainMaxBillboardStart = cfg.Bind("Terrain", "MaxBillboardStart", 60f,
                new ConfigDescription("Distance at which trees switch to flat billboards. 0 = keep.", new AcceptableValueRange<float>(0f, 2000f)));
            TerrainMaxDetailDistance = cfg.Bind("Terrain", "MaxDetailDistance", 80f,
                new ConfigDescription("Max grass/detail draw distance (game Low = 100, High = 250). 0 = keep.", new AcceptableValueRange<float>(0f, 250f)));
            TerrainMaxDetailDensity = cfg.Bind("Terrain", "MaxDetailDensity", 0f,
                new ConfigDescription("Max grass/detail density 0-1 (the game's Foliage dropdown already sets this). 0 = keep.", new AcceptableValueRange<float>(0f, 1f)));

            MaxLodBias = cfg.Bind("Quality", "MaxLodBias", 0.8f,
                new ConfigDescription("Lower = models switch to simpler LODs sooner. 0 = keep.", new AcceptableValueRange<float>(0f, 4f)));
            MaxPixelLights = cfg.Bind("Quality", "MaxPixelLights", 2,
                new ConfigDescription("Max per-pixel lights per object in forward rendering; extras fall back to cheaper per-vertex. 0 = keep.", new AcceptableValueRange<int>(0, 8)));
            DisableSoftParticles = cfg.Bind("Quality", "DisableSoftParticles", true, "Soft particles need a depth read per particle pixel.");
            LimitAnisotropic = cfg.Bind("Quality", "LimitAnisotropic", true, "Use anisotropic filtering only where textures request it, instead of forcing it on everything.");
            ParticleRaycastBudget = cfg.Bind("Quality", "ParticleRaycastBudget", 64,
                new ConfigDescription("Max particle collision raycasts per frame (Unity default 256+). 0 = keep.", new AcceptableValueRange<int>(0, 4096)));

            FullDetailIndoors = cfg.Bind("Indoors", "FullDetailIndoors", true,
                "Inside the base, ignore MaxLodBias and MaxPixelLights so counters, servers and lamps don't pop. The indoor view distance is short, so this costs little.");
            IndoorLodBias = cfg.Bind("Indoors", "IndoorLodBias", 0f,
                new ConfigDescription("LOD bias to use inside the base. 0 = the game's own value. Raise (e.g. 2) if you still see pop-in indoors.", new AcceptableValueRange<float>(0f, 5f)));
            IndoorPixelLights = cfg.Bind("Indoors", "IndoorPixelLights", 0,
                new ConfigDescription("Per-pixel light limit inside the base. 0 = the game's own value.", new AcceptableValueRange<int>(0, 8)));
        }
    }
}
