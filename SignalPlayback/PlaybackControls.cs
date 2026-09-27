using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalSimMods.SignalPlayback
{
    /// <summary>
    /// Lives on the laptop's signal database (DBMain) and adds a seek bar, elapsed / total time, pause,
    /// skip back / forward and loop to the existing PLAY/STOP button. The game's own button keeps working;
    /// this only reads and nudges the same DBaudio AudioSource it plays through.
    /// </summary>
    internal class PlaybackControls : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<DBMain, AudioSource> AudioRef = AccessTools.FieldRefAccess<DBMain, AudioSource>("dbAudio");
        private static readonly AccessTools.FieldRef<DBMain, Button> PlayButtonRef = AccessTools.FieldRefAccess<DBMain, Button>("audioButton");
        private static readonly AccessTools.FieldRef<DBMain, RawImage> VideoRef = AccessTools.FieldRefAccess<DBMain, RawImage>("audioVideo");
        private static readonly AccessTools.FieldRef<DBMain, SignalModel> SelectedRef = AccessTools.FieldRefAccess<DBMain, SignalModel>("printSignal");
        private static readonly System.Reflection.MethodInfo PlayAudioVideo = AccessTools.Method(typeof(DBMain), "PlayAudioVideo");

        private static readonly Color Accent = new Color32(0xFF, 0x6D, 0x6D, 0xFF); // the red the database uses for labels

        private DBMain db;
        private AudioSource audio;
        private Button playButton;
        private RawImage video;

        private Slider seekBar;
        private Text timeLabel;
        private Button pauseButton, backButton, forwardButton, loopButton;
        private Text pauseText, loopText;

        private SignalModel selected;
        private AudioClip previewClip;   // clip for the selected signal, loaded ahead of PLAY to show its length
        private bool previewLoading;
        private float pendingStart = -1f; // position chosen while stopped; applied when PLAY starts
        private bool paused;
        private float pausedAt;
        private bool wasPlaying;
        private bool dragging;

        public bool Paused => paused;

        public static PlaybackControls Attach(DBMain db)
        {
            var controls = db.gameObject.GetComponent<PlaybackControls>();
            if (controls == null) controls = db.gameObject.AddComponent<PlaybackControls>();
            controls.Init(db);
            return controls;
        }

        private void Init(DBMain owner)
        {
            if (seekBar != null) return;
            db = owner;
            audio = AudioRef(db);
            playButton = PlayButtonRef(db);
            video = VideoRef(db);
            if (audio == null || playButton == null)
            {
                SignalPlaybackPlugin.Log.LogWarning("Signal database layout not recognised; playback controls not added.");
                enabled = false;
                return;
            }
            audio.loop = SignalPlaybackPlugin.Loop.Value;
            BuildButtons();
            BuildSeekRow();
            SignalPlaybackPlugin.Log.LogInfo("Playback controls added to the signal database.");
        }

        // ---------------------------------------------------------------- UI construction

        private void BuildButtons()
        {
            var playRt = (RectTransform)playButton.transform;
            float half = playRt.sizeDelta.x / 2f;
            const float gap = 6f, near = 90f, far = 70f;
            string skip = SignalPlaybackPlugin.SkipSeconds.Value.ToString("0.#");

            // Mirrored around PLAY:  [-5s] [PAUSE] [PLAY/STOP] [+5s] [LOOP]
            float nearX = half + gap + near / 2f;               // 90-wide button next to PLAY
            float farX = half + gap + near + gap + far / 2f;    // 70-wide button beyond it
            float nearNarrowX = half + gap + far / 2f;          // 70-wide button next to PLAY
            float farWideX = half + gap + far + gap + near / 2f; // 90-wide button beyond it

            pauseButton = CloneButton("Pause", "PAUSE", near, -nearX, TogglePause, out pauseText);
            backButton = CloneButton("Back", "-" + skip + "s", far, -farX, () => Skip(-SignalPlaybackPlugin.SkipSeconds.Value), out _);
            forwardButton = CloneButton("Forward", "+" + skip + "s", far, nearNarrowX, () => Skip(SignalPlaybackPlugin.SkipSeconds.Value), out _);
            loopButton = CloneButton("Loop", "", near, farWideX, ToggleLoop, out loopText);
            RefreshLoopLabel();
        }

        /// <summary>Copies the game's PLAY button so the new buttons match the laptop's style exactly.</summary>
        private Button CloneButton(string name, string label, float width, float x, UnityEngine.Events.UnityAction onClick, out Text text)
        {
            var playRt = (RectTransform)playButton.transform;
            var go = Instantiate(playButton.gameObject, playRt.parent, false);
            go.name = "mod_playback_" + name;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = playRt.anchorMin;
            rt.anchorMax = playRt.anchorMax;
            rt.pivot = playRt.pivot;
            rt.sizeDelta = new Vector2(width, playRt.sizeDelta.y);
            rt.anchoredPosition = playRt.anchoredPosition + new Vector2(x, 0f);

            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);

            var textTransform = go.transform.Find("Text");
            text = textTransform != null ? textTransform.GetComponent<Text>() : go.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 6;
                text.resizeTextMaxSize = Mathf.Max(text.fontSize, 6);
            }
            return button;
        }

        /// <summary>A thin seek bar + time readout in the empty strip just above the database footer.</summary>
        private void BuildSeekRow()
        {
            var footer = playButton.transform.parent != null ? playButton.transform.parent.parent : null;
            if (footer == null) footer = playButton.transform.parent;

            var row = NewUI("mod_playback_SeekRow", footer);
            row.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; // footer has a layout group
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(0f, 13f);

            const float labelWidth = 120f;

            // Seek bar
            var sliderRt = NewUI("SeekBar", row);
            sliderRt.anchorMin = Vector2.zero;
            sliderRt.anchorMax = Vector2.one;
            sliderRt.offsetMin = Vector2.zero;
            sliderRt.offsetMax = new Vector2(-(labelWidth + 8f), 0f);

            var background = NewUI("Background", sliderRt);
            Stretch(background, 0f);
            var bgImage = background.gameObject.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.55f);

            var fillArea = NewUI("Fill Area", sliderRt);
            Stretch(fillArea, 2f);
            var fill = NewUI("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.sizeDelta = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = Accent;
            fillImage.raycastTarget = false;

            var handleArea = NewUI("Handle Slide Area", sliderRt);
            Stretch(handleArea, 0f);
            handleArea.offsetMin = new Vector2(3f, 0f);
            handleArea.offsetMax = new Vector2(-3f, 0f);
            var handle = NewUI("Handle", handleArea);
            handle.sizeDelta = new Vector2(6f, 0f);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.white;

            seekBar = sliderRt.gameObject.AddComponent<Slider>();
            seekBar.fillRect = fill;
            seekBar.handleRect = handle;
            seekBar.targetGraphic = handleImage;
            seekBar.direction = Slider.Direction.LeftToRight;
            seekBar.minValue = 0f;
            seekBar.maxValue = 1f;
            seekBar.navigation = new Navigation { mode = Navigation.Mode.None };
            var drag = sliderRt.gameObject.AddComponent<SeekBarDrag>();
            drag.Owner = this;

            // Time readout
            var labelRt = NewUI("Time", row);
            labelRt.anchorMin = new Vector2(1f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.pivot = new Vector2(1f, 0.5f);
            labelRt.sizeDelta = new Vector2(labelWidth, 0f);
            labelRt.anchoredPosition = Vector2.zero;
            timeLabel = labelRt.gameObject.AddComponent<Text>();
            var playText = playButton.GetComponentInChildren<Text>();
            timeLabel.font = playText != null ? playText.font : db.newFont;
            timeLabel.fontSize = 11;
            timeLabel.alignment = TextAnchor.MiddleRight;
            timeLabel.color = Color.white;
            timeLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            timeLabel.verticalOverflow = VerticalWrapMode.Overflow;
            timeLabel.raycastTarget = false;
        }

        private static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            return rt;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        // ---------------------------------------------------------------- Game hooks

        /// <summary>Called after the player picks a signal in the list (the game stops playback there).</summary>
        public void OnSignalSelected()
        {
            selected = SelectedRef(db);
            paused = false;
            pendingStart = -1f;
            previewClip = null;
            previewLoading = false;
            ShowStopped();
            if (isActiveAndEnabled && selected != null)
            {
                var forSignal = selected;
                previewLoading = true;
                StartCoroutine(SignalClips.Resolve(forSignal, clip =>
                {
                    if (selected != forSignal) return; // player already picked another signal
                    previewClip = clip;
                    previewLoading = false;
                }));
            }
        }

        /// <summary>The game's PLAY/STOP only knows playing vs. not; while paused its button means STOP.</summary>
        public void StopFromPause()
        {
            paused = false;
            pendingStart = -1f;
            audio.Stop();
            wasPlaying = false;
            ShowStopped();
        }

        /// <summary>What the game does when STOP is pressed, also used when a signal simply finishes.</summary>
        private void ShowStopped()
        {
            var label = playButton.GetComponentInChildren<Text>();
            if (label != null) label.text = "PLAY";
            if (video != null) video.enabled = false;
            if (SignalLibrary.audioControlPanel != null) SignalLibrary.audioControlPanel.volume = SignalLibrary.audioVol;
        }

        // ---------------------------------------------------------------- Controls

        private bool Playing => audio != null && audio.isPlaying;

        private AudioClip CurrentClip => (Playing || paused) && audio.clip != null ? audio.clip : previewClip;

        private float Length
        {
            get { var clip = CurrentClip; return clip != null ? clip.length : 0f; }
        }

        private float Position
        {
            get
            {
                if (Playing || paused) return audio.time;
                return pendingStart >= 0f ? pendingStart : 0f;
            }
        }

        private void TogglePause()
        {
            if (paused)
            {
                audio.UnPause();
                paused = false;
                wasPlaying = true;
                if (video != null) video.enabled = true;
                // The game's waveform scroll loop exits once it sees the audio paused (it checks every 0.1 s).
                // Restart it, unless the old loop hasn't had a chance to exit yet (it would then run twice).
                if (PlayAudioVideo != null && Time.time - pausedAt > 0.15f)
                    db.StartCoroutine((IEnumerator)PlayAudioVideo.Invoke(db, null));
            }
            else if (Playing)
            {
                audio.Pause();
                paused = true;
                pausedAt = Time.time;
            }
        }

        private void ToggleLoop()
        {
            SignalPlaybackPlugin.Loop.Value = !SignalPlaybackPlugin.Loop.Value;
            audio.loop = SignalPlaybackPlugin.Loop.Value;
            RefreshLoopLabel();
        }

        private void RefreshLoopLabel()
        {
            if (loopText != null) loopText.text = SignalPlaybackPlugin.Loop.Value ? "LOOP: ON" : "LOOP: OFF";
        }

        private void Skip(float seconds) => Seek(Position + seconds);

        internal void Seek(float seconds)
        {
            float length = Length;
            if (length <= 0f) return;
            // Setting time exactly at the end throws off some decoders, so stop just short of it.
            seconds = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, length - 0.05f));
            if (Playing || paused) audio.time = seconds;
            else pendingStart = seconds;
        }

        internal void BeginDrag() => dragging = true;

        internal void EndDrag()
        {
            dragging = false;
            Seek(seekBar.value * Length);
        }

        // ---------------------------------------------------------------- Per frame

        private void OnDisable()
        {
            // Leaving the database deactivates DBaudio, which stops playback outright.
            paused = false;
            dragging = false;
        }

        private void Update()
        {
            if (seekBar == null || audio == null) return;

            bool playing = Playing;
            if (playing && !wasPlaying)
            {
                // PLAY was just pressed: honour a position picked with the seek bar / skip buttons while stopped.
                if (pendingStart > 0f) audio.time = Mathf.Min(pendingStart, Mathf.Max(0f, Length - 0.05f));
                pendingStart = -1f;
            }
            else if (!playing && wasPlaying && !paused)
            {
                // Stopped or finished on its own. The game leaves "STOP" showing and the control panel
                // muted when a signal simply ends, so put both back.
                ShowStopped();
            }
            wasPlaying = playing;

            HandleKeys();
            RefreshUI();
        }

        private void HandleKeys()
        {
            if (!DBMainMenu.LaptopUIOpen) return;
            if (SignalPlaybackPlugin.SkipBackKey.Value.IsDown()) Skip(-SignalPlaybackPlugin.SkipSeconds.Value);
            if (SignalPlaybackPlugin.SkipForwardKey.Value.IsDown()) Skip(SignalPlaybackPlugin.SkipSeconds.Value);
            if (SignalPlaybackPlugin.PauseKey.Value.IsDown()) TogglePause();
        }

        private void RefreshUI()
        {
            float length = Length;
            bool hasClip = length > 0f;
            float position = dragging ? seekBar.value * length : Position;

            if (!dragging) seekBar.SetValueWithoutNotify(hasClip ? Mathf.Clamp01(position / length) : 0f);
            seekBar.interactable = hasClip;
            backButton.interactable = hasClip;
            forwardButton.interactable = hasClip;
            pauseButton.interactable = Playing || paused;
            pauseText.text = paused ? "RESUME" : "PAUSE";

            if (!hasClip) timeLabel.text = previewLoading ? "LOADING..." : "--:-- / --:--";
            else timeLabel.text = Format(position) + " / " + Format(length);
        }

        private static string Format(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60) + ":" + (total % 60).ToString("00");
        }
    }

    /// <summary>Seeks once when the player lets go, instead of on every drag step.</summary>
    internal class SeekBarDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public PlaybackControls Owner;

        public void OnPointerDown(PointerEventData eventData) => Owner.BeginDrag();

        public void OnPointerUp(PointerEventData eventData) => Owner.EndDrag();
    }
}
