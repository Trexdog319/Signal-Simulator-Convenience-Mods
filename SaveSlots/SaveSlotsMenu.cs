using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UIEventTrigger = UnityEngine.EventSystems.EventTrigger;
using UnityEngine.UI;

namespace SignalSimMods.SaveSlots
{
    /// <summary>
    /// Main-menu integration: a SAVE FILES button cloned from the game's own menu buttons (so it looks and
    /// behaves the same), which opens a panel for picking, creating, renaming and deleting save files.
    /// </summary>
    internal class SaveSlotsMenu : MonoBehaviour
    {
        private MenuControl menu;
        private Traverse t;
        private Button slotButton;
        private Text slotButtonText;
        private bool open;

        private SlotInfo renaming;
        private string renameText = "";
        private SlotInfo confirmDelete;
        private Vector2 scroll;
        private float rowWidth;   // usable width inside a slot row, so long text can't push the buttons off the panel

        private GUIStyle panel, row, rowSelected, title, nameStyle, warnStyle, detail, summary, button, dangerButton, field, hint;
        private Font gameFont;

        private void Start()
        {
            menu = GetComponent<MenuControl>();
            t = Traverse.Create(menu);
            StartCoroutine(Init());
        }

        private IEnumerator Init()
        {
            // One frame later, so the game has loaded its settings (difficulty lives there) before we set the slot's.
            yield return null;
            ApplySlot();
            CreateButton();
        }

        private GameObject Field(string n) { return t.Field(n).GetValue<GameObject>(); }
        private Button ButtonField(string n) { return t.Field(n).GetValue<Button>(); }

        // ------------------------------------------------------------------ main menu button

        private void CreateButton()
        {
            var template = ButtonField("btn_Difficulty");
            var start = ButtonField("startGameButton");
            if (template == null || start == null)
            {
                SaveSlotsPlugin.Log.LogWarning("Main menu buttons not found; save files menu unavailable.");
                return;
            }
            var go = Instantiate(template.gameObject, template.transform.parent, false);
            go.name = "btn_SaveFiles";
            // The difficulty button carries its pop-out menu as children; keep only the label.
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                var child = go.transform.GetChild(i);
                if (child.name != "Text") DestroyImmediate(child.gameObject);
            }
            go.transform.SetSiblingIndex(start.transform.GetSiblingIndex() + 1);   // right under NEW GAME

            slotButton = go.GetComponent<Button>();
            slotButton.onClick = new Button.ButtonClickedEvent();
            slotButton.onClick.AddListener(() => { if (open) Close(); else Open(); });

            // The cloned hover trigger would highlight the Difficulty button; point it at this one instead.
            var trigger = go.GetComponent<UIEventTrigger>();
            if (trigger != null)
            {
                trigger.triggers.Clear();
                AddTrigger(trigger, EventTriggerType.PointerEnter, () => menu.OnPointerEnter(slotButton));
                AddTrigger(trigger, EventTriggerType.PointerExit, () => menu.OnPointerExit(slotButton));
            }
            menu.ButtonReset(slotButton);

            slotButtonText = go.GetComponentInChildren<Text>(true);
            gameFont = slotButtonText != null ? slotButtonText.font : null;
            UpdateButtonLabel();
        }

        private static void AddTrigger(UIEventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new UIEventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private void UpdateButtonLabel()
        {
            if (slotButtonText == null) return;
            string n = SlotStore.Current.displayName.ToUpperInvariant();
            if (n.Length > 12) n = n.Substring(0, 11) + "...";
            slotButtonText.text = "SAVE: " + n;
        }

        /// <summary>Point the game at the selected save and refresh what the menu shows for it.</summary>
        private void ApplySlot()
        {
            SlotStore.ApplySelection();
            SlotStore.ApplyDifficulty();
            try { t.Method("CheckDifficulitySelection").GetValue(); } catch { }   // refresh the Easy/Normal/Hard ticks
            var cont = ButtonField("continueButton");
            if (cont != null) cont.gameObject.SetActive(SlotStore.HasGame(SlotStore.Current));
            UpdateButtonLabel();
        }

        // ------------------------------------------------------------------ panel open/close

        private void Open()
        {
            open = true;
            renaming = null;
            confirmDelete = null;
            // Behave like the game's DIFFICULTY sub-menu: same title slot, close the other pop-outs.
            var diffMenu = Field("difficultyMenu");
            var diffText = Field("difficultyExplane");
            if (diffMenu != null) diffMenu.SetActive(false);
            if (diffText != null) diffText.SetActive(false);
            var credits = Field("CreditsPage");
            if (credits != null) credits.SetActive(false);
            var menuText = t.Field("menuText").GetValue<Text>();
            if (menuText != null) menuText.text = "SAVE FILES\n-";
        }

        private void Close()
        {
            open = false;
            var menuText = t.Field("menuText").GetValue<Text>();
            if (menuText != null && menuText.text == "SAVE FILES\n-") menuText.text = "MAIN MENU\n-";
        }

        private void Update()
        {
            if (!open) return;
            // Another part of the menu took over (Difficulty, Options, Credits, loading a game): step aside.
            var main = Field("MainMenu");
            var diff = Field("difficultyMenu");
            var options = Field("OptionsMenu");
            var credits = Field("CreditsPage");
            var loading = Field("loadingCanvas");
            var warning = Field("warningMassage");
            if ((main != null && !main.activeInHierarchy) || (diff != null && diff.activeSelf) || (options != null && options.activeSelf)
                || (credits != null && credits.activeSelf) || (loading != null && loading.activeSelf) || (warning != null && warning.activeSelf))
                open = false;
            if (Input.GetKeyDown(KeyCode.Escape) && renaming == null && confirmDelete == null) Close();
        }

        // ------------------------------------------------------------------ panel drawing

        private static Texture2D Tex(Color c)
        {
            var tex = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }

        private void Styles()
        {
            if (panel != null) return;
            panel = new GUIStyle { padding = new RectOffset(22, 22, 18, 18) };
            panel.normal.background = Tex(new Color(0.06f, 0.07f, 0.08f, 0.9f));
            row = new GUIStyle { padding = new RectOffset(14, 12, 10, 10), margin = new RectOffset(0, 0, 0, 6) };
            row.normal.background = Tex(new Color(0.2f, 0.2f, 0.2f, 0.85f));
            row.hover.background = Tex(new Color(0.27f, 0.27f, 0.27f, 0.9f));
            rowSelected = new GUIStyle(row);
            rowSelected.normal.background = rowSelected.hover.background = Tex(new Color(0f, 0.42f, 0.5f, 0.9f));   // the game's hover teal
            title = new GUIStyle { font = gameFont, fontSize = 26, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            nameStyle = new GUIStyle { font = gameFont, fontSize = 19, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip };
            nameStyle.normal.textColor = Color.white;
            detail = new GUIStyle { font = gameFont, fontSize = 14, wordWrap = true };
            detail.normal.textColor = new Color(0.78f, 0.8f, 0.82f);
            summary = new GUIStyle(detail) { wordWrap = true };   // wraps within the fixed text width set per row
            warnStyle = new GUIStyle(nameStyle) { wordWrap = true, clipping = TextClipping.Overflow };
            hint = new GUIStyle(detail) { fontSize = 14 };
            hint.normal.textColor = new Color(0.65f, 0.68f, 0.7f);
            button = new GUIStyle { font = gameFont, fontSize = 15, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(14, 14, 7, 7), margin = new RectOffset(4, 4, 2, 2) };
            button.normal.background = Tex(new Color(0.2f, 0.2f, 0.2f, 1f));
            button.hover.background = Tex(new Color(0f, 0.5f, 0.6f, 1f));
            button.active.background = Tex(new Color(0f, 0.38f, 0.46f, 1f));
            button.normal.textColor = button.hover.textColor = button.active.textColor = Color.white;
            dangerButton = new GUIStyle(button);
            dangerButton.normal.background = Tex(new Color(0.5f, 0.2f, 0.2f, 1f));
            dangerButton.hover.background = Tex(new Color(0.7f, 0.25f, 0.25f, 1f));
            field = new GUIStyle(GUI.skin.textField) { font = gameFont, fontSize = 18, padding = new RectOffset(8, 8, 6, 6) };
        }

        private void OnGUI()
        {
            if (!open) return;
            Styles();
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            var rect = new Rect(Mathf.Max(430f, w * 0.3f), 150f, Mathf.Min(760f, w - Mathf.Max(430f, w * 0.3f) - 40f), 740f);

            rowWidth = rect.width - panel.padding.horizontal - row.padding.horizontal - 20f;   // 20 = scrollbar
            GUILayout.BeginArea(rect, panel);
            GUILayout.Label("SAVE FILES", title);
            GUILayout.Space(4f);
            GUILayout.Label("Pick a save, then press CONTINUE. Pick an empty one and press NEW GAME to start a separate game. "
                            + "Saves never affect each other.", hint);
            GUILayout.Space(12f);

            scroll = GUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            var current = SlotStore.Current;
            foreach (var s in SlotStore.Slots.ToArray()) DrawSlot(s, s == current);
            GUILayout.EndScrollView();

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+  NEW SAVE FILE", button, GUILayout.Height(40f)))
            {
                var s = SlotStore.Create();
                Select(s);
                renaming = s;
                renameText = s.displayName;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", button, GUILayout.Width(120f), GUILayout.Height(40f))) Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawSlot(SlotInfo s, bool selected)
        {
            GUILayout.BeginVertical(selected ? rowSelected : row);

            if (renaming == s)
            {
                GUILayout.Label("Name this save:", detail);
                GUI.SetNextControlName("slotRename");
                renameText = GUILayout.TextField(renameText, 32, field);
                GUI.FocusControl("slotRename");
                GUILayout.BeginHorizontal();
                bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
                if (GUILayout.Button("SAVE NAME", button) || enter)
                {
                    SlotStore.Rename(s, renameText);
                    renaming = null;
                    UpdateButtonLabel();
                }
                if (GUILayout.Button("CANCEL", button)) renaming = null;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else if (confirmDelete == s)
            {
                GUILayout.Label("Delete \"" + s.displayName + "\" for good? Its progress can't be recovered.", warnStyle, GUILayout.Width(rowWidth));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("DELETE", dangerButton))
                {
                    SlotStore.Delete(s);
                    confirmDelete = null;
                    ApplySlot();
                }
                if (GUILayout.Button("CANCEL", button)) confirmDelete = null;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                // Text gets exactly the width the buttons leave (three 96px buttons plus margins).
                float textWidth = rowWidth - (selected ? 2 : 3) * 104f - 8f;
                GUILayout.BeginVertical(GUILayout.Width(textWidth));
                GUILayout.Label((selected ? "> " : "") + s.displayName + (selected ? "   (selected)" : ""), nameStyle, GUILayout.Width(textWidth));
                GUILayout.Label(SlotStore.Summary(s), summary, GUILayout.Width(textWidth));
                GUILayout.EndVertical();
                if (!selected && GUILayout.Button("SELECT", button, GUILayout.Width(96f))) Select(s);
                if (GUILayout.Button("RENAME", button, GUILayout.Width(96f))) { renaming = s; renameText = s.displayName; confirmDelete = null; }
                if (GUILayout.Button("DELETE", dangerButton, GUILayout.Width(96f))) { confirmDelete = s; renaming = null; }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void Select(SlotInfo s)
        {
            SlotStore.Select(s);
            ApplySlot();
            SaveSlotsPlugin.Log.LogInfo("Selected save file \"" + s.displayName + "\" (" + s.saveName + ".dat).");
        }
    }
}
