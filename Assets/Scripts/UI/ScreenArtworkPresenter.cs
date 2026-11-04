using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtaNoUtakeiko.Gameplay;
using UtaNoUtakeiko.Data;

namespace Uta.GameFlow
{
    /// <summary>
    /// Displays replaceable artwork and the title, selection, result, and
    /// prototype ranking screens. Gameplay artwork stays behind the game scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenArtworkPresenter : MonoBehaviour
    {
        [SerializeField] private GameSessionManager sessionManager;
        [SerializeField] private GameSessionPlayConnector playConnector;
        private Component sessionTimeoutComponent;
        private PropertyInfo remainingTimeProperty;
        [SerializeField] private GameSessionPlaybackConnector playbackConnector;

        [Header("Artwork (Sprite / 2D and UI)")]
        [SerializeField] private Sprite titleArtwork;
        [SerializeField] private Sprite normalCharacter;
        [SerializeField] private Sprite singingGoodCharacter;
        [SerializeField] private Sprite singingBadCharacter;
        [SerializeField] private Sprite playWallpaper;

        [Header("Result artwork selection")]
        [SerializeField, Range(0f, 100f)]
        private float goodResultThresholdPercent = 70f;

        [Header("Prototype selection options")]
        [SerializeField] private string[] difficultyLabels =
            { "かんたん", "ふつう", "むずかしい" };
        [SerializeField] private string[] songSymbols = { "☆" };
        [SerializeField] private float countdownSeconds = 5f;
        [SerializeField, Min(0f)] private float finishDisplaySeconds = 2f;

        private Canvas screenCanvas;
        private Image backgroundImage;
        private Image characterImage;
        private SpriteRenderer wallpaperRenderer;
        private Camera gameplayCamera;
        private RectTransform uiRoot;
        private Text selectionValue;
        private Text countdownText;
        private InputField playerNameInput;
        private Text rankingListText;
        private Text gameplayTimerText;
        private Text playbackLyricText;
        private Text playbackPitchText;
        private RectTransform playbackScorePanel;
        private readonly List<PlaybackNoteVisual> playbackNoteVisuals = new List<PlaybackNoteVisual>();

        private sealed class PlaybackNoteVisual
        {
            public Note Note;
            public float StartSeconds;
            public float EndSeconds;
            public int PitchStep;
            public bool IsPlayerPitch;
            public Text Text;
        }
        private int difficultyIndex;
        private int songIndex;
        private int playerRank = 11;
        private string playerName = "あなた";
        private bool countdownRunning;
        private bool finishDisplayRunning;

        private static readonly string[] DemoNames =
            { "あおい", "ゆき", "ほし", "みなと", "そら", "りん", "はる", "つき", "うみ", "こと" };
        private static readonly float[] DemoRates =
            { 99f, 97f, 95f, 93f, 91f, 89f, 87f, 84f, 80f, 75f };

        private Font uiFont;

        private void OnEnable()
        {
            if (sessionManager == null)
                sessionManager = GetComponent<GameSessionManager>();

            if (sessionManager != null)
                sessionManager.OnStateChanged += HandleStateChanged;

            if (screenCanvas != null)
            {
                screenCanvas.gameObject.SetActive(true);
                ApplyState(sessionManager != null
                    ? sessionManager.CurrentState
                    : GameSessionState.Title);
            }
        }

        private void OnDisable()
        {
            if (sessionManager != null)
                sessionManager.OnStateChanged -= HandleStateChanged;

            if (screenCanvas != null)
                screenCanvas.gameObject.SetActive(false);

            if (wallpaperRenderer != null)
                wallpaperRenderer.enabled = false;
        }

        private void Start()
        {
            if (sessionManager == null)
                sessionManager = GetComponent<GameSessionManager>();
            if (playConnector == null)
                playConnector = GetComponent<GameSessionPlayConnector>()
                    ?? FindFirstObjectByType<GameSessionPlayConnector>();
            ResolveSessionTimeout();
            if (playbackConnector == null)
                playbackConnector = GetComponent<GameSessionPlaybackConnector>()
                    ?? FindFirstObjectByType<GameSessionPlaybackConnector>();

            BuildScreen();
            ApplyState(sessionManager != null
                ? sessionManager.CurrentState
                : GameSessionState.Title);
        }

        private void Update()
        {
            if (sessionManager == null)
                return;

            GameSessionState state = sessionManager.CurrentState;
            UpdateGameplayTimer(state);
            if (state == GameSessionState.Playback)
                UpdatePlaybackScore();

            if (Keyboard.current == null)
                return;
            if (state == GameSessionState.DifficultySelect ||
                state == GameSessionState.SongSelect)
            {
                if (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                    Keyboard.current.spaceKey.wasPressedThisFrame)
                    SelectNext();
                else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
                    SelectNext();

                if (Keyboard.current.enterKey.wasPressedThisFrame ||
                    Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                    Confirm();
            }
            else if (Keyboard.current.enterKey.wasPressedThisFrame ||
                     Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                Confirm();
            }
        }

        private void LateUpdate()
        {
            if (wallpaperRenderer != null && wallpaperRenderer.enabled)
                FitWallpaperToCamera();
        }

        private void HandleStateChanged(GameSessionState previous, GameSessionState next)
        {
            ApplyState(next);
        }

        private void BuildScreen()
        {
            var canvasObject = new GameObject(
                "Artwork Screen Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            screenCanvas = canvasObject.GetComponent<Canvas>();
            screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            screenCanvas.sortingOrder = 20;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            backgroundImage = CreateImage(
                "Screen Artwork Background", canvasObject.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                Color.white);

            characterImage = CreateImage(
                "Screen Character Artwork", canvasObject.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-300f, 70f), new Vector2(600f, 850f),
                Color.white);
            characterImage.rectTransform.pivot = new Vector2(0.5f, 0f);
            characterImage.preserveAspect = true;

            var uiObject = new GameObject("Screen UI", typeof(RectTransform));
            uiObject.transform.SetParent(canvasObject.transform, false);
            uiRoot = uiObject.GetComponent<RectTransform>();
            Stretch(uiRoot);

            var wallpaperObject = new GameObject("Play Wallpaper Background");
            wallpaperRenderer = wallpaperObject.AddComponent<SpriteRenderer>();
            wallpaperRenderer.sortingOrder = -1000;
            wallpaperRenderer.enabled = false;
            gameplayCamera = Camera.main;

            uiFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Arial" }, 32);
        }

        private static Image CreateImage(
            string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var imageObject = new GameObject(
                objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void ApplyState(GameSessionState state)
        {
            if (screenCanvas == null)
                return;

            bool isPlaying = state == GameSessionState.Playing;
            bool isPlayback = state == GameSessionState.Playback;
            bool isCountdown = state == GameSessionState.Countdown;
            bool isFinish = state == GameSessionState.GameFinished;
            bool isResultOrLater =
                state == GameSessionState.Result ||
                state == GameSessionState.Ranking ||
                state == GameSessionState.Announcement;
            bool isFullScreenSelection =
                state == GameSessionState.DifficultySelect ||
                state == GameSessionState.SongSelect;
            bool useWorldWallpaper = isPlaying || isCountdown;

            if (wallpaperRenderer != null)
            {
                wallpaperRenderer.sprite = playWallpaper;
                wallpaperRenderer.enabled = useWorldWallpaper && playWallpaper != null;
                if (wallpaperRenderer.enabled)
                    FitWallpaperToCamera();
            }

            // Keep the input and judgment scene unobstructed.
            screenCanvas.gameObject.SetActive(true);

            // Show title art only on the title screen. Selection and
            // post-play screens use the same wallpaper as gameplay, at its
            // original colors. The countdown keeps its translucent dimmer over
            // the gameplay wallpaper rendered by the world-space SpriteRenderer.
            Sprite backdrop = state == GameSessionState.Title
                ? titleArtwork
                : (isFullScreenSelection || isPlayback || isResultOrLater)
                    ? playWallpaper
                    : null;
            backgroundImage.sprite = backdrop;
            if (isCountdown)
                backgroundImage.color = new Color(0f, 0f, 0f, 0.62f);
            else if (isFinish)
                backgroundImage.color = new Color(0.035f, 0.08f, 0.14f, 1f);
            else
                backgroundImage.color = Color.white;

            backgroundImage.gameObject.SetActive(
                backdrop != null || isFullScreenSelection ||
                isCountdown || isFinish || isPlayback || isResultOrLater);

            Sprite character = CharacterFor(state);
            characterImage.sprite = character;
            characterImage.gameObject.SetActive(character != null);
            SetCharacterLayout(state);

            RenderState(state);
        }

        private void RenderState(GameSessionState state)
        {
            if (uiRoot == null)
                return;

            ClearChildren(uiRoot);
            selectionValue = null;
            countdownText = null;
            playerNameInput = null;
            rankingListText = null;
            gameplayTimerText = null;
            playbackLyricText = null;
            playbackPitchText = null;
            playbackScorePanel = null;
            playbackNoteVisuals.Clear();

            switch (state)
            {
                case GameSessionState.Title:
                    AddText("タイトル", 70, new Vector2(0.5f, 0.76f), new Vector2(1300, 120));
                    AddActionButton("はじめる", new Vector2(0.5f, 0.2f), Confirm);
                    break;

                case GameSessionState.DifficultySelect:
                    BuildCarousel(
                        "難易度を選択",
                        SafeLabel(difficultyLabels, difficultyIndex),
                        "選択", "決定");
                    break;

                case GameSessionState.SongSelect:
                    BuildCarousel(
                        "曲を選択",
                        SafeLabel(songSymbols, songIndex),
                        "選択", "決定");
                    break;

                case GameSessionState.Playing:
                    gameplayTimerText = AddText(
                        "残り時間 00:00", 48,
                        new Vector2(0.86f, 0.94f), new Vector2(420, 90));
                    break;

                case GameSessionState.Countdown:
                    countdownText = AddText(
                        Mathf.Max(1, Mathf.CeilToInt(countdownSeconds)).ToString(),
                        180, new Vector2(0.5f, 0.5f), new Vector2(600, 300));
                    if (!countdownRunning)
                        StartCoroutine(CountdownToGameplay());
                    break;

                case GameSessionState.GameFinished:
                    AddText("Finish！", 180, new Vector2(0.5f, 0.5f), new Vector2(1200, 320));
                    if (!finishDisplayRunning)
                        StartCoroutine(FinishDisplayToPlayback());
                    break;

                case GameSessionState.Playback:
                    BuildPlaybackScore();
                    break;

                case GameSessionState.Result:
                    BuildResult();
                    break;

                case GameSessionState.Ranking:
                    BuildRanking();
                    break;

                case GameSessionState.Announcement:
                    AddText("また遊んでね", 64, new Vector2(0.5f, 0.6f), new Vector2(1100, 120));
                    AddActionButton("タイトルへ", new Vector2(0.5f, 0.2f), Confirm);
                    break;

                case GameSessionState.Error:
                    AddText("エラー", 64, new Vector2(0.5f, 0.6f), new Vector2(900, 120));
                    AddActionButton("タイトルへ", new Vector2(0.5f, 0.2f), Confirm);
                    break;
            }
        }

        private void ResolveSessionTimeout()
        {
            if (sessionTimeoutComponent != null)
                return;

            // The project currently contains an imported GameSessionTimeout
            // type with the same name, so resolve the attached component by
            // its runtime type name instead of a compile-time generic type.
            sessionTimeoutComponent = GetComponent("GameSessionTimeout");
            if (sessionTimeoutComponent == null)
            {
                var components = FindObjectsByType<MonoBehaviour>(
                    FindObjectsSortMode.None);
                foreach (var component in components)
                {
                    if (component != null &&
                        component.GetType().Name == "GameSessionTimeout")
                    {
                        sessionTimeoutComponent = component;
                        break;
                    }
                }
            }

            if (sessionTimeoutComponent != null)
                remainingTimeProperty = sessionTimeoutComponent.GetType()
                    .GetProperty("RemainingTime");
        }

        private void UpdateGameplayTimer(GameSessionState state)
        {
            if (state != GameSessionState.Playing || gameplayTimerText == null)
                return;

            ResolveSessionTimeout();
            float remainingTime = 0f;
            if (sessionTimeoutComponent != null && remainingTimeProperty != null)
            {
                object value = remainingTimeProperty.GetValue(sessionTimeoutComponent);
                if (value is float timerValue)
                    remainingTime = timerValue;
            }

            int seconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
            gameplayTimerText.text = $"残り時間 {seconds / 60:00}:{seconds % 60:00}";
        }

        private void BuildPlaybackScore()
        {
            var panelImage = CreateImage(
                "Playback Score Panel", uiRoot,
                new Vector2(0.40f, 0.13f), new Vector2(0.90f, 0.9f),
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.1f, 0.78f));
            playbackScorePanel = panelImage.rectTransform;

            var title = CreateChildText(
                "Playback Score Title", playbackScorePanel, "♪ うたっているよ",
                38, new Vector2(0.5f, 0.88f), new Vector2(820, 70));
            title.color = new Color(0.7f, 0.9f, 1f, 1f);

            var measureText = CreateChildText(
                "Playback Measure", playbackScorePanel, "", 24,
                new Vector2(0.5f, 0.78f), new Vector2(700, 50));
            measureText.color = new Color(0.75f, 0.82f, 0.9f, 1f);

            var targetLegend = CreateChildText(
                "Target Pitch Legend", playbackScorePanel, "● 本来の音", 24,
                new Vector2(0.3f, 0.68f), new Vector2(300, 52));
            targetLegend.color = new Color(0.92f, 0.96f, 1f, 1f);
            var inputLegend = CreateChildText(
                "Player Pitch Legend", playbackScorePanel, "● 入力結果", 24,
                new Vector2(0.7f, 0.68f), new Vector2(300, 52));
            inputLegend.color = new Color(1f, 0.78f, 0.2f, 1f);

            playbackPitchText = CreateChildText(
                "Playback Pitch Values", playbackScorePanel, "", 24,
                new Vector2(0.5f, 0.56f), new Vector2(820, 48));
            playbackPitchText.color = new Color(1f, 0.96f, 0.84f, 1f);

            var lowerLedger = CreateImage(
                "Lower C Ledger Line", playbackScorePanel,
                new Vector2(0.32f, 0.42f), new Vector2(0.68f, 0.42f),
                new Vector2(0f, -40f), new Vector2(0f, 2f),
                new Color(0.75f, 0.84f, 0.92f, 0.78f));
            lowerLedger.raycastTarget = false;

            for (int i = 0; i < 5; i++)
            {
                var line = CreateImage(
                    "Staff Line " + (i + 1), playbackScorePanel,
                    new Vector2(0.08f, 0.42f), new Vector2(0.94f, 0.42f),
                    new Vector2(0f, i * 40f), new Vector2(0f, 2f),
                    new Color(0.75f, 0.84f, 0.92f, 0.78f));
                line.raycastTarget = false;
            }

            playbackLyricText = CreateChildText(
                "Current Lyric", playbackScorePanel, "", 42,
                new Vector2(0.5f, 0.18f), new Vector2(820, 100));
            playbackLyricText.color = Color.white;

            var chart = playConnector != null ? playConnector.CurrentSongChart : null;
            var plan = playConnector != null ? playConnector.CurrentPlaybackPlan : null;
            if (chart == null || plan == null || plan.Sections == null)
                return;

            double beatsPerMeasure =
                chart.TimeSignatureNumerator * 4.0 /
                Mathf.Max(1, chart.TimeSignatureDenominator);
            float secondsPerBeat = 60f / Mathf.Max(1, chart.Bpm);

            var capturedPitchByNoteId = new Dictionary<string, UnitResult>();
            var vocalScore = playConnector.CurrentVocalScoreResult;
            if (vocalScore != null && vocalScore.UnitResults != null)
            {
                foreach (var result in vocalScore.UnitResults)
                {
                    if (result != null && !string.IsNullOrWhiteSpace(result.UnitId))
                        capturedPitchByNoteId[result.UnitId] = result;
                }
            }

            foreach (var section in plan.Sections)
            {
                if (section == null || section.Notes == null)
                    continue;

                var orderedNoteIndices = new List<int>();
                for (int i = 0; i < section.Notes.Count; i++)
                    orderedNoteIndices.Add(i);
                orderedNoteIndices.Sort((a, b) =>
                {
                    int measureOrder = section.Notes[a].MeasureNumber.CompareTo(
                        section.Notes[b].MeasureNumber);
                    return measureOrder != 0
                        ? measureOrder
                        : section.Notes[a].NoteIndexInMeasure.CompareTo(
                            section.Notes[b].NoteIndexInMeasure);
                });

                int currentMeasure = -1;
                double elapsedBeats = 0d;
                foreach (int noteIndex in orderedNoteIndices)
                {
                    var note = section.Notes[noteIndex];
                    if (note == null)
                        continue;
                    string sourceNoteId =
                        section.SourceNoteIds != null &&
                        noteIndex < section.SourceNoteIds.Count
                            ? section.SourceNoteIds[noteIndex]
                            : note.NoteId;

                    if (note.MeasureNumber != currentMeasure)
                    {
                        currentMeasure = note.MeasureNumber;
                        elapsedBeats = 0d;
                    }

                    float startSeconds = (float)(
                        ((note.MeasureNumber - 1) * beatsPerMeasure + elapsedBeats)
                        * secondsPerBeat);
                    elapsedBeats += note.DurationBeats;
                    float endSeconds = startSeconds + note.DurationBeats * secondsPerBeat;

                    AddPlaybackNoteVisual(
                        note, startSeconds, endSeconds,
                        note.IsRest ? 4 : note.TargetPitchStep, false,
                        note.IsRest ? "𝄽" : "●", note.IsRest ? 30 : 42,
                        "Playback Note");

                    if (!note.IsRest && !string.IsNullOrWhiteSpace(sourceNoteId) &&
                        capturedPitchByNoteId.TryGetValue(
                        sourceNoteId, out var captured) &&
                        captured.InputState == InputState.Completed &&
                        captured.CapturedPitchStep.HasValue)
                    {
                        AddPlaybackNoteVisual(
                            note, startSeconds, endSeconds,
                            captured.CapturedPitchStep.Value, true, "●", 42,
                            "Player Input Pitch");
                    }
                }
            }

            playbackNoteVisuals.Sort(
                (a, b) => a.StartSeconds.CompareTo(b.StartSeconds));
        }

        private void AddPlaybackNoteVisual(
            Note note, float startSeconds, float endSeconds,
            int pitchStep, bool isPlayerPitch, string symbol, int fontSize,
            string objectName)
        {
            var noteText = CreateChildText(
                objectName, playbackScorePanel, symbol, fontSize,
                new Vector2(0.5f, 0.42f), new Vector2(80, 80));
            playbackNoteVisuals.Add(new PlaybackNoteVisual
            {
                Note = note,
                StartSeconds = startSeconds,
                EndSeconds = endSeconds,
                PitchStep = pitchStep,
                IsPlayerPitch = isPlayerPitch,
                Text = noteText
            });
        }

        private void UpdatePlaybackScore()
        {
            if (playbackScorePanel == null)
                return;

            if (playbackConnector == null)
                playbackConnector = FindFirstObjectByType<GameSessionPlaybackConnector>();
            float now = playbackConnector != null
                ? playbackConnector.CurrentPlaybackTimeSeconds
                : 0f;
            float panelWidth = playbackScorePanel.rect.width;
            float centerX = panelWidth * 0.5f;
            const float pixelsPerSecond = 86f;
            int activeMeasure = 1;
            string activeLyric = "";

            var chart = playConnector != null ? playConnector.CurrentSongChart : null;
            if (chart != null && chart.Bpm > 0 &&
                chart.TimeSignatureNumerator > 0 &&
                chart.TimeSignatureDenominator > 0)
            {
                float secondsPerMeasure =
                    60f / chart.Bpm *
                    chart.TimeSignatureNumerator * 4f /
                    chart.TimeSignatureDenominator;
                activeMeasure = Mathf.FloorToInt(now / secondsPerMeasure) + 1;
                if (chart.LyricLines != null)
                {
                    foreach (var line in chart.LyricLines)
                    {
                        if (line != null &&
                            activeMeasure >= line.StartMeasure &&
                            activeMeasure <= line.EndMeasure)
                        {
                            activeLyric = line.Text;
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < playbackNoteVisuals.Count; i++)
            {
                var visual = playbackNoteVisuals[i];
                float x = centerX + (visual.StartSeconds - now) * pixelsPerSecond;
                bool visible = x >= 20f && x <= panelWidth - 20f;
                visual.Text.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                float y = visual.Note != null && visual.Note.IsRest
                    ? 80f
                    : PitchStepToStaffOffset(
                        visual.IsPlayerPitch && visual.Note != null
                            ? visual.Note.TargetPitchStep
                            : visual.PitchStep);
                visual.Text.rectTransform.anchoredPosition =
                    new Vector2(x - centerX, y);
                bool current = now >= visual.StartSeconds && now < visual.EndSeconds;
                if (visual.IsPlayerPitch)
                    visual.Text.gameObject.SetActive(visible && current);

                visual.Text.color = visual.IsPlayerPitch
                    ? (current
                        ? new Color(1f, 0.82f, 0.26f, 1f)
                        : new Color(0.86f, 0.62f, 0.16f, 1f))
                    : new Color(0.92f, 0.96f, 1f, 1f);
            }

            Note activeTargetNote = null;
            int? activeInputPitchStep = null;
            for (int i = 0; i < playbackNoteVisuals.Count; i++)
            {
                var visual = playbackNoteVisuals[i];
                if (now < visual.StartSeconds || now >= visual.EndSeconds)
                    continue;

                if (visual.IsPlayerPitch)
                    activeInputPitchStep = visual.PitchStep;
                else if (visual.Note != null && !visual.Note.IsRest)
                    activeTargetNote = visual.Note;
            }

            if (playbackPitchText != null)
            {
                string targetPitch = activeTargetNote != null
                    ? PitchStepLabel(activeTargetNote.TargetPitchStep)
                    : "—";
                string inputPitch = activeInputPitchStep.HasValue
                    ? PitchStepLabel(activeInputPitchStep.Value)
                    : "—";
                playbackPitchText.text =
                    $"本来の音: {targetPitch}　入力結果: {inputPitch}";
            }

            if (string.IsNullOrEmpty(activeLyric))
                activeLyric = FallbackLyricForMeasure(
                    chart != null ? chart.SongId : null, activeMeasure);

            if (playbackLyricText != null)
                playbackLyricText.text = string.IsNullOrEmpty(activeLyric)
                    ? ""
                    : activeLyric;

            var measureLabel = playbackScorePanel.Find("Playback Measure");
            var measure = measureLabel != null ? measureLabel.GetComponent<Text>() : null;
            if (measure != null)
                measure.text = $"第 {activeMeasure} 小節";
        }

        private static float PitchStepToStaffOffset(int semitoneStep)
        {
            int octave = Mathf.FloorToInt(semitoneStep / 12f);
            int pitchClass = semitoneStep - octave * 12;
            int diatonicStep;
            switch (pitchClass)
            {
                case 0:
                case 1: diatonicStep = 0; break;
                case 2:
                case 3: diatonicStep = 1; break;
                case 4: diatonicStep = 2; break;
                case 5:
                case 6: diatonicStep = 3; break;
                case 7:
                case 8: diatonicStep = 4; break;
                case 9:
                case 10: diatonicStep = 5; break;
                default: diatonicStep = 6; break;
            }

            // Pitch steps are semitones from C4. Convert to diatonic staff
            // positions with E4 on the bottom line; accidentals stay on that
            // note position and remain readable in the pitch value above.
            return (octave * 7 + diatonicStep - 2) * 20f;
        }

        private static string PitchStepLabel(int semitoneStep)
        {
            string[] pitchNames =
                { "C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B" };
            int octave = Mathf.FloorToInt(semitoneStep / 12f) + 4;
            int pitchClass = semitoneStep - Mathf.FloorToInt(semitoneStep / 12f) * 12;
            return $"{pitchNames[pitchClass]}{octave}";
        }

        private static string FallbackLyricForMeasure(string songId, int measure)
        {
            // Keep the prototype readable with older serialized chart assets.
            if (songId != "TWINKLE_TWINKLE")
                return "";

            if (measure >= 5 && measure <= 8)
                return "まばたきしては みんなをみてる";
            return measure >= 1 && measure <= 12
                ? "きらきらひかる おそらのほしよ"
                : "";
        }

        private Text CreateChildText(
            string objectName, Transform parent, string value, int fontSize,
            Vector2 anchor, Vector2 size)
        {
            var text = CreateText(
                objectName, parent, value, fontSize, TextAnchor.MiddleCenter);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            text.color = Color.white;
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            return text;
        }

        private void SetCharacterLayout(GameSessionState state)
        {
            var rect = characterImage.rectTransform;
            if (state == GameSessionState.Playback)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(36f, 42f);
                rect.sizeDelta = new Vector2(720f, 930f);
            }
            else if (state == GameSessionState.DifficultySelect ||
                     state == GameSessionState.SongSelect)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-70f, 180f);
                rect.sizeDelta = new Vector2(590f, 770f);
            }
            else if (state == GameSessionState.Result)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-20f, 10f);
                rect.sizeDelta = new Vector2(440f, 590f);
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-55f, 42f);
                rect.sizeDelta = new Vector2(620f, 900f);
            }
        }

        private void BuildCarousel(string title, string value, string selectLabel, string confirmLabel)
        {
            // Keep the entire choice flow in the left column.
            AddText(title, 56, new Vector2(0.27f, 0.78f), new Vector2(900, 110));
            AddText("‹", 70, new Vector2(0.07f, 0.53f), new Vector2(110, 150));
            selectionValue = AddText(value, 74, new Vector2(0.27f, 0.53f), new Vector2(660, 180));
            AddText("›", 70, new Vector2(0.47f, 0.53f), new Vector2(110, 150));
            AddActionButton(selectLabel, new Vector2(0.18f, 0.25f), SelectNext);
            AddActionButton(confirmLabel, new Vector2(0.37f, 0.25f), Confirm);

            // Reserve the right column for Uta and her dialogue.
            CreateImage(
                "Uta Dialogue Background", uiRoot,
                new Vector2(0.61f, 0.045f), new Vector2(0.98f, 0.22f),
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.08f, 0.15f, 0.76f));
            var characterLine = AddText(
                "どれにする？いっしょに選ぼう！", 32,
                new Vector2(0.795f, 0.132f), new Vector2(690, 110));
            characterLine.color = Color.white;
        }

        private void BuildResult()
        {
            ScoreResult result = playConnector != null
                ? playConnector.CurrentScoreResult
                : null;
            float rate = result != null ? result.AchievementRate : 0f;
            string rank = RankLabel(rate);
            string message = rate >= 95f ? "すごい！ほとんど音が合ってたよ。次はパーフェクトを目指そう♪"
                : rate >= 70f ? "一緒に歌えてうれしかったよ！次も楽しく歌おうね♪"
                : "最後までよく頑張ったね！次はもっと一緒に歌おう♪";

            CreateImage(
                "Result Header Background", uiRoot,
                new Vector2(0.04f, 0.88f), new Vector2(0.98f, 0.98f),
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.1f, 0.82f));
            AddText("リザルト", 52, new Vector2(0.5f, 0.93f), new Vector2(1000, 82));

            var scorePanelImage = CreateImage(
                "Result Score Panel", uiRoot,
                new Vector2(0.04f, 0.15f), new Vector2(0.62f, 0.85f),
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.1f, 0.82f));
            RectTransform scorePanel = scorePanelImage.rectTransform;

            var scoreLabel = CreateChildText(
                "Result Score Label", scorePanel, "TOTAL SCORE", 28,
                new Vector2(0.5f, 0.86f), new Vector2(900, 54));
            scoreLabel.color = new Color(0.78f, 0.86f, 0.96f, 1f);
            CreateChildText(
                "Result Score Value", scorePanel, $"{result?.Score ?? 0:N0}", 78,
                new Vector2(0.5f, 0.68f), new Vector2(980, 112));

            var rankLine = CreateChildText(
                "Result Rank Line", scorePanel, $"達成率  {rate:F1}%　　 ランク  {rank}", 38,
                new Vector2(0.5f, 0.49f), new Vector2(980, 76));
            rankLine.color = new Color(1f, 0.84f, 0.39f, 1f);

            var rateTrack = CreateImage(
                "Result Achievement Track", scorePanel,
                new Vector2(0.12f, 0.36f), new Vector2(0.88f, 0.36f),
                Vector2.zero, new Vector2(0f, 16f), new Color(0.48f, 0.58f, 0.72f, 0.65f));
            CreateImage(
                "Result Achievement Fill", rateTrack.transform,
                Vector2.zero, new Vector2(Mathf.Clamp01(rate / 100f), 1f),
                Vector2.zero, Vector2.zero, new Color(1f, 0.77f, 0.22f, 1f));

            string judgmentSummary = result != null
                ? $"Perfect  {result.PerfectCount}　 Great  {result.GreatCount}　 Good  {result.GoodCount}\nBad  {result.BadCount}　 Miss  {result.MissCount}"
                : "判定結果はありません";
            var judgments = CreateChildText(
                "Result Judgment Summary", scorePanel, judgmentSummary, 31,
                new Vector2(0.5f, 0.17f), new Vector2(1000, 128));
            judgments.color = new Color(0.95f, 0.97f, 1f, 1f);

            var messagePanelImage = CreateImage(
                "Uta Result Message Panel", uiRoot,
                new Vector2(0.65f, 0.61f), new Vector2(0.98f, 0.86f),
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.1f, 0.84f));
            RectTransform messagePanel = messagePanelImage.rectTransform;
            var messageHeading = CreateChildText(
                "Uta Result Message Heading", messagePanel, "ウタからひとこと", 28,
                new Vector2(0.5f, 0.78f), new Vector2(580, 58));
            messageHeading.color = new Color(1f, 0.84f, 0.39f, 1f);
            CreateChildText(
                "Uta Result Message", messagePanel, message, 30,
                new Vector2(0.5f, 0.42f), new Vector2(590, 145));

            AddActionButton("ランキングへ　›", new Vector2(0.33f, 0.095f), Confirm);
        }

        private void BuildRanking()
        {
            ScoreResult result = playConnector != null
                ? playConnector.CurrentScoreResult
                : null;
            float rate = result != null ? result.AchievementRate : 0f;
            playerRank = CalculateDemoRank(rate);
            string savedName = PlayerPrefs.GetString("Uta.PlayerName", "あなた");

            CreateImage(
                "Ranking Header Background", uiRoot,
                new Vector2(0.04f, 0.88f), new Vector2(0.98f, 0.98f),
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.1f, 0.82f));
            AddText("ランキング", 52, new Vector2(0.5f, 0.93f), new Vector2(1000, 82));

            var listPanelImage = CreateImage(
                "Leaderboard Panel", uiRoot,
                new Vector2(0.04f, 0.15f), new Vector2(0.64f, 0.85f),
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.1f, 0.82f));
            RectTransform listPanel = listPanelImage.rectTransform;

            var detailsPanelImage = CreateImage(
                "Ranking Player Details Panel", uiRoot,
                new Vector2(0.66f, 0.15f), new Vector2(0.98f, 0.85f),
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.1f, 0.84f));
            RectTransform detailsPanel = detailsPanelImage.rectTransform;

            var rows = new List<string>();
            var playerRows = new List<bool>();
            if (playerRank <= 10)
            {
                int demoIndex = 0;
                for (int rank = 1; rank <= 10; rank++)
                {
                    bool isPlayer = rank == playerRank;
                    if (isPlayer)
                    {
                        playerName = savedName;
                        rows.Add($"{rank,2}位　{playerName}　{rate:F1}%");
                    }
                    else
                    {
                        rows.Add($"{rank,2}位　{DemoNames[demoIndex]}　{DemoRates[demoIndex]:F1}%");
                        demoIndex++;
                    }
                    playerRows.Add(isPlayer);
                }
            }
            else
            {
                for (int i = 0; i < 7; i++)
                {
                    rows.Add($"{i + 1,2}位　{DemoNames[i]}　{DemoRates[i]:F1}%");
                    playerRows.Add(false);
                }
                rows.Add("・・・");
                playerRows.Add(false);
                rows.Add($"{playerRank,2}位　{savedName}　{rate:F1}%");
                playerRows.Add(true);
                rows.Add("・・・");
                playerRows.Add(false);
            }

            float rowStep = 0.88f / rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                float rowY = 0.94f - rowStep * (i + 0.5f);
                if (playerRows[i])
                {
                    CreateImage(
                        "Player Rank Highlight", listPanel,
                        new Vector2(0.035f, rowY - rowStep * 0.44f),
                        new Vector2(0.965f, rowY + rowStep * 0.44f),
                        Vector2.zero, Vector2.zero, new Color(0.24f, 0.31f, 0.43f, 0.96f));
                }
            }

            rankingListText = CreateChildText(
                "Leaderboard Rows", listPanel, string.Join("\n", rows), 30,
                new Vector2(0.5f, 0.5f), new Vector2(1100, 650));
            rankingListText.alignment = TextAnchor.MiddleLeft;
            rankingListText.color = new Color(0.97f, 0.98f, 1f, 1f);
            rankingListText.lineSpacing = rows.Count == 10 ? 1.2f : 1.3f;

            CreateChildText(
                "Ranking Place Label", detailsPanel, "今回の順位", 29,
                new Vector2(0.5f, 0.84f), new Vector2(520, 58));
            var rankValue = CreateChildText(
                "Ranking Place Value", detailsPanel, $"{playerRank}位", 64,
                new Vector2(0.5f, 0.69f), new Vector2(520, 92));
            rankValue.color = new Color(1f, 0.84f, 0.39f, 1f);

            if (playerRank <= 10)
            {
                var prompt = CreateChildText(
                    "Ranking Name Prompt", detailsPanel,
                    "10位以内！名前を登録して\nランキングに残そう", 27,
                    new Vector2(0.5f, 0.52f), new Vector2(540, 92));
                prompt.color = new Color(0.9f, 0.94f, 1f, 1f);
                CreateChildText(
                    "Ranking Name Label", detailsPanel, "表示する名前", 23,
                    new Vector2(0.5f, 0.39f), new Vector2(500, 48));
                playerNameInput = CreateNameInput(savedName, new Vector2(0.82f, 0.31f));
                AddActionButton("登録して次へ　›", new Vector2(0.82f, 0.205f), Confirm);
            }
            else
            {
                var encouragement = CreateChildText(
                    "Ranking Encouragement", detailsPanel,
                    "次はもっと上を目指そう！", 30,
                    new Vector2(0.5f, 0.48f), new Vector2(540, 100));
                encouragement.color = new Color(0.9f, 0.94f, 1f, 1f);
                AddActionButton("次へ　›", new Vector2(0.82f, 0.205f), Confirm);
            }
        }

        private InputField CreateNameInput(string initialValue, Vector2 anchor)
        {
            var root = CreateUiObject("Player Name Input", uiRoot);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(480, 66);

            var image = root.AddComponent<Image>();
            image.color = new Color(0.04f, 0.07f, 0.12f, 0.96f);

            var input = root.AddComponent<InputField>();
            var text = CreateText("Name Text", root.transform, "", 28, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(14, 4);
            text.rectTransform.offsetMax = new Vector2(-14, -4);
            text.color = Color.white;
            var placeholder = CreateText("Placeholder", root.transform, "名前", 26, TextAnchor.MiddleLeft);
            Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(14, 4);
            placeholder.rectTransform.offsetMax = new Vector2(-14, -4);
            placeholder.color = new Color(1, 1, 1, 0.6f);

            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = initialValue;
            input.characterLimit = 12;
            input.lineType = InputField.LineType.SingleLine;
            input.onValueChanged.AddListener(value =>
            {
                string previousName = playerName;
                playerName = string.IsNullOrWhiteSpace(value) ? "あなた" : value.Trim();
                if (rankingListText != null)
                    rankingListText.text = rankingListText.text.Replace(previousName, playerName);
            });
            return input;
        }

        private void SelectNext()
        {
            if (sessionManager == null)
                return;

            if (sessionManager.CurrentState == GameSessionState.DifficultySelect)
            {
                int count = Mathf.Max(1, difficultyLabels == null ? 0 : difficultyLabels.Length);
                difficultyIndex = (difficultyIndex + 1) % count;
                if (selectionValue != null)
                    selectionValue.text = SafeLabel(difficultyLabels, difficultyIndex);
            }
            else if (sessionManager.CurrentState == GameSessionState.SongSelect)
            {
                int count = Mathf.Max(1, songSymbols == null ? 0 : songSymbols.Length);
                songIndex = (songIndex + 1) % count;
                if (selectionValue != null)
                    selectionValue.text = SafeLabel(songSymbols, songIndex);
            }
        }

        private void Confirm()
        {
            if (sessionManager == null)
                return;

            switch (sessionManager.CurrentState)
            {
                case GameSessionState.Title:
                    sessionManager.TryTransition(GameSessionState.DifficultySelect);
                    break;
                case GameSessionState.DifficultySelect:
                    sessionManager.TryTransition(GameSessionState.SongSelect);
                    break;
                case GameSessionState.SongSelect:
                    sessionManager.TryTransition(GameSessionState.Countdown);
                    break;
                case GameSessionState.Result:
                    sessionManager.TryTransition(GameSessionState.Ranking);
                    break;
                case GameSessionState.Ranking:
                    if (playerNameInput != null)
                    {
                        playerName = string.IsNullOrWhiteSpace(playerNameInput.text)
                            ? "あなた"
                            : playerNameInput.text.Trim();
                        PlayerPrefs.SetString("Uta.PlayerName", playerName);
                        PlayerPrefs.Save();
                    }
                    sessionManager.TryTransition(GameSessionState.Announcement);
                    break;
                case GameSessionState.Announcement:
                case GameSessionState.Error:
                    sessionManager.RecoverToTitle();
                    break;
            }
        }

        private IEnumerator FinishDisplayToPlayback()
        {
            finishDisplayRunning = true;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, finishDisplaySeconds));
            finishDisplayRunning = false;

            if (sessionManager != null &&
                sessionManager.CurrentState == GameSessionState.GameFinished)
                sessionManager.TryTransition(GameSessionState.Playback);
        }

        private IEnumerator CountdownToGameplay()
        {
            countdownRunning = true;
            int remaining = Mathf.Max(1, Mathf.CeilToInt(countdownSeconds));

            while (remaining > 0 &&
                   sessionManager != null &&
                   sessionManager.CurrentState == GameSessionState.Countdown)
            {
                if (countdownText != null)
                    countdownText.text = remaining.ToString();

                yield return new WaitForSecondsRealtime(1f);
                remaining--;
            }

            countdownRunning = false;
            if (sessionManager != null &&
                sessionManager.CurrentState == GameSessionState.Countdown)
                sessionManager.TryTransition(GameSessionState.Playing);
        }

        private int CalculateDemoRank(float rate)
        {
            for (int i = 0; i < DemoRates.Length; i++)
            {
                if (rate > DemoRates[i])
                    return i + 1;
            }
            return 11;
        }

        private static string RankLabel(float rate)
        {
            if (rate >= 95f) return "S";
            if (rate >= 90f) return "A";
            if (rate >= 80f) return "B";
            if (rate >= 70f) return "C";
            return "D";
        }

        private static string SafeLabel(string[] values, int index)
        {
            return values != null && values.Length > 0
                ? values[Mathf.Clamp(index, 0, values.Length - 1)]
                : "☆";
        }

        private void AddActionButton(string label, Vector2 anchor, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = CreateUiObject(label + " Button", uiRoot);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(300, 76);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.3f, 0.46f, 0.94f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var labelText = CreateText("Label", buttonObject.transform, label, 30, TextAnchor.MiddleCenter);
            Stretch(labelText.rectTransform);
            labelText.color = Color.white;
        }

        private Text AddText(string value, int fontSize, Vector2 anchor, Vector2 size)
        {
            var text = CreateText("Text", uiRoot, value, fontSize, TextAnchor.MiddleCenter);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            text.color = Color.white;
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            return text;
        }

        private Text CreateText(string objectName, Transform parent, string value, int fontSize, TextAnchor alignment)
        {
            var obj = CreateUiObject(objectName, parent);
            var text = obj.AddComponent<Text>();
            text.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(16, fontSize - 8);
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private void FitWallpaperToCamera()
        {
            if (wallpaperRenderer == null || playWallpaper == null)
                return;

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;
            if (gameplayCamera == null || !gameplayCamera.orthographic)
                return;

            float visibleHeight = gameplayCamera.orthographicSize * 2f;
            float visibleWidth = visibleHeight * gameplayCamera.aspect;
            Vector2 spriteSize = playWallpaper.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
                return;

            float scale = Mathf.Max(visibleWidth / spriteSize.x, visibleHeight / spriteSize.y);
            Transform wallpaperTransform = wallpaperRenderer.transform;
            wallpaperTransform.position = new Vector3(
                gameplayCamera.transform.position.x,
                gameplayCamera.transform.position.y,
                0f);
            wallpaperTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private Sprite CharacterFor(GameSessionState state)
        {
            switch (state)
            {
                case GameSessionState.Playback:
                    float rate = playConnector != null &&
                                 playConnector.CurrentVocalScoreResult != null
                        ? playConnector.CurrentVocalScoreResult.AchievementRate
                        : 0f;
                    return rate >= goodResultThresholdPercent
                        ? singingGoodCharacter
                        : singingBadCharacter;
                case GameSessionState.DifficultySelect:
                case GameSessionState.SongSelect:
                case GameSessionState.Result:
                    return normalCharacter;
                default:
                    return null;
            }
        }
    }
}
