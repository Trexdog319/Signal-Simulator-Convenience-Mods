using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SignalSimMods.Shared
{
    /// <summary>
    /// Adds rows to the game's Options > Gameplay panel by cloning the existing
    /// "Disable HUD" (toggle) and "FPS FOV" (slider) rows, so injected controls
    /// match the game's own look. The panel is a VerticalLayoutGroup, so appended
    /// rows position themselves. Both mods compile this file in; rows are tagged
    /// by name so each mod's rows are only added once per scene.
    /// </summary>
    internal static class SettingsMenu
    {
        private const string RowPrefix = "mod_";

        /// <summary>Call from a SettingsManager.Awake postfix.</summary>
        public static bool TryGetTemplates(SettingsManager settings, out Transform container, out GameObject toggleRow, out GameObject sliderRow)
        {
            container = null;
            toggleRow = null;
            sliderRow = null;
            var hud = Traverse.Create(settings).Field("hudDisable").GetValue<Toggle>();
            var fov = Traverse.Create(settings).Field("fpsFOVslider").GetValue<Slider>();
            if (hud == null || fov == null || hud.transform.parent == null || fov.transform.parent == null)
                return false;
            toggleRow = hud.transform.parent.gameObject;
            sliderRow = fov.transform.parent.gameObject;
            container = toggleRow.transform.parent;
            return container != null;
        }

        public static Toggle AddToggle(Transform container, GameObject template, string id, string label, bool value, Action<bool> onChanged)
        {
            if (container.Find(RowPrefix + id) != null) return null;
            var row = UnityEngine.Object.Instantiate(template, container, false);
            row.name = RowPrefix + id;
            row.SetActive(true);
            SetLabel(row, template, label);

            var toggle = row.GetComponentInChildren<Toggle>(true);
            toggle.name = RowPrefix + id + "_Toggle";
            // Drop the cloned persistent listener (it would call SettingsManager.OnHUDChange).
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.group = null;
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        public static Slider AddSlider(Transform container, GameObject template, string id, string label,
            float min, float max, float value, float step, Func<float, string> format, Action<float> onChanged)
        {
            if (container.Find(RowPrefix + id) != null) return null;
            var row = UnityEngine.Object.Instantiate(template, container, false);
            row.name = RowPrefix + id;
            row.SetActive(true);
            SetLabel(row, template, label);

            var slider = row.GetComponentInChildren<Slider>(true);
            slider.name = RowPrefix + id + "_Slider";
            slider.onValueChanged = new Slider.SliderEvent();
            slider.wholeNumbers = false;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = Mathf.Clamp(value, min, max);

            var valueText = slider.transform.Find("text");
            Text text = valueText != null ? valueText.GetComponent<Text>() : null;
            if (text != null) text.text = format(slider.value);

            slider.onValueChanged.AddListener(v =>
            {
                float snapped = step > 0f ? Mathf.Round(v / step) * step : v;
                if (text != null) text.text = format(snapped);
                onChanged(snapped);
            });
            return slider;
        }

        private static void SetLabel(GameObject row, GameObject template, string label)
        {
            var text = row.GetComponent<Text>();
            if (text == null) return;
            // Mirror the game's label casing (e.g. if its labels are ALL CAPS).
            var original = template.GetComponent<Text>().text ?? "";
            bool upper = original.Length > 0 && original == original.ToUpperInvariant() && original != original.ToLowerInvariant();
            text.text = upper ? label.ToUpperInvariant() : label;
            // Labels share a fixed 160px column with the game's own; shrink longer ones instead of wrapping.
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Max(6, Mathf.RoundToInt(text.fontSize * 0.6f));
        }
    }
}
