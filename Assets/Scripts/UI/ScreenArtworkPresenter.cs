using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtaNoUtakeiko.Gameplay;

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
        [SerializeField] private float countdownSeconds = 3f;

        private Canvas screenCanvas;
        private Image backgroundImage;
        private Image characterImage;
        private SpriteRenderer wallpaperRenderer;
        private Camera gameplayCamera;
        private RectTransform uiRoot;
        private Text selectionValue;
        private InputField playerNameInput;
        private Text rankingListText;
        private int difficultyIndex;
        private int songIndex;
        private int playerRank = 11;
        private string playerName = "あなた";
        private bool countdownRunning;

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
                playConnector = GetComponent<GameSessionPlayConnector>();

            BuildScreen();
            ApplyState(sessionManager != null
                ? sessionManager.CurrentState
                : GameSessionState.Title);
        }

        private void Update()
        {
            if (sessionManager == null || Keyboard.current == null)
                return;

            GameSessionState state = sessionManager.CurrentState;
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
            bool useWorldWallpaper = isPlaying || isPlayback;

            if (wallpaperRenderer != null)
            {
                wallpaperRenderer.sprite = playWallpaper;
                wallpaperRenderer.enabled = useWorldWallpaper && playWallpaper != null;
                if (wallpaperRenderer.enabled)
                    FitWallpaperToCamera();
            }

            // Keep the input and judgment scene unobstructed.
            screenCanvas.gameObject.SetActive(!isPlaying);

            bool isFullScreenSelection =
                state == GameSessionState.DifficultySelect ||
                state == GameSessionState.SongSelect;
            Sprite backdrop = useWorldWallpaper || isFullScreenSelection
                ? null
                : titleArtwork;
            backgroundImage.sprite = backdrop;
            backgroundImage.color = isFullScreenSelection
                ? new Color(0.035f, 0.08f, 0.14f, 1f)
                : Color.white;
            backgroundImage.gameObject.SetActive(backdrop != null || isFullScreenSelection);

            Sprite character = CharacterFor(state);
            characterImage.sprite = character;
            characterImage.gameObject.SetActive(character != null);

            RenderState(state);
        }

        private void RenderState(GameSessionState state)
        {
            if (uiRoot == null)
                return;

            ClearChildren(uiRoot);
            selectionValue = null;
            playerNameInput = null;
            rankingListText = null;

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

                case GameSessionState.Countdown:
                    AddText("準備", 64, new Vector2(0.5f, 0.62f), new Vector2(900, 120));
                    AddText("まもなくスタート", 34, new Vector2(0.5f, 0.48f), new Vector2(1000, 80));
                    if (!countdownRunning)
                        StartCoroutine(CountdownToGameplay());
                    break;

                case GameSessionState.Playback:
                    // Vocal playback runs automatically; character artwork is shown.
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

        private void BuildCarousel(string title, string value, string selectLabel, string confirmLabel)
        {
            AddText(title, 56, new Vector2(0.5f, 0.75f), new Vector2(1400, 110));
            AddText("‹", 70, new Vector2(0.22f, 0.5f), new Vector2(120, 150));
            selectionValue = AddText(value, 74, new Vector2(0.5f, 0.5f), new Vector2(900, 180));
            AddText("›", 70, new Vector2(0.78f, 0.5f), new Vector2(120, 150));
            AddActionButton(selectLabel, new Vector2(0.36f, 0.2f), SelectNext);
            AddActionButton(confirmLabel, new Vector2(0.64f, 0.2f), Confirm);
        }

        private void BuildResult()
        {
            ScoreResult result = playConnector != null
                ? playConnector.CurrentScoreResult
                : null;
            float rate = result != null ? result.AchievementRate : 0f;
            string rank = RankLabel(rate);
            string message = rate >= 95f ? "すごくきれいな歌だったよ！"
                : rate >= 70f ? "一緒に歌えてうれしかったよ！"
                : "次はもっと一緒に歌おうね！";

            AddText("リザルト", 56, new Vector2(0.5f, 0.86f), new Vector2(1000, 100));
            AddText($"スコア  {result?.Score ?? 0:N0}", 42,
                new Vector2(0.5f, 0.69f), new Vector2(1100, 80));
            AddText($"達成率  {rate:F1}%　ランク {rank}", 36,
                new Vector2(0.5f, 0.59f), new Vector2(1200, 80));
            if (result != null)
            {
                AddText(
                    $"Perfect {result.PerfectCount}　Great {result.GreatCount}　Good {result.GoodCount}\nBad {result.BadCount}　Miss {result.MissCount}",
                    30, new Vector2(0.5f, 0.46f), new Vector2(1400, 100));
            }
            AddText($"ウタ「{message}」", 30,
                new Vector2(0.5f, 0.32f), new Vector2(1300, 90));
            AddActionButton("ランキングへ", new Vector2(0.5f, 0.16f), Confirm);
        }

        private void BuildRanking()
        {
            ScoreResult result = playConnector != null
                ? playConnector.CurrentScoreResult
                : null;
            float rate = result != null ? result.AchievementRate : 0f;
            playerRank = CalculateDemoRank(rate);
            string savedName = PlayerPrefs.GetString("Uta.PlayerName", "あなた");

            AddText("ランキング", 54, new Vector2(0.5f, 0.9f), new Vector2(1100, 100));

            if (playerRank <= 10)
            {
                AddText("10位以内！名前を入力してください", 28,
                    new Vector2(0.5f, 0.81f), new Vector2(1300, 60));
                playerNameInput = CreateNameInput(savedName);
            }

            var rows = new List<string>();
            int demoIndex = 0;
            for (int rank = 1; rank <= 10; rank++)
            {
                if (rank == playerRank)
                {
                    playerName = savedName;
                    rows.Add($"{rank}位　{playerName}　{rate:F1}%");
                }
                else if (rank < playerRank)
                {
                    rows.Add($"{rank}位　{DemoNames[demoIndex]}　{DemoRates[demoIndex]:F1}%");
                    demoIndex++;
                }
                else if (playerRank <= 10)
                {
                    int idx = rank - 2;
                    rows.Add($"{rank}位　{DemoNames[Mathf.Clamp(idx, 0, 9)]}　{DemoRates[Mathf.Clamp(idx, 0, 9)]:F1}%");
                }
            }

            if (playerRank <= 10)
            {
                rankingListText = AddText(string.Join("\n", rows), 27,
                    new Vector2(0.5f, 0.49f), new Vector2(1100, 470));
            }
            else
            {
                var topRows = new List<string>();
                for (int i = 0; i < 7; i++)
                    topRows.Add($"{i + 1}位　{DemoNames[i]}　{DemoRates[i]:F1}%");
                topRows.Add("…");
                topRows.Add($"{playerRank}位　{savedName}　{rate:F1}%");
                topRows.Add("…");
                rankingListText = AddText(string.Join("\n", topRows), 27,
                    new Vector2(0.5f, 0.49f), new Vector2(1100, 470));
            }

            AddActionButton("次へ", new Vector2(0.5f, 0.12f), Confirm);
        }

        private InputField CreateNameInput(string initialValue)
        {
            var root = CreateUiObject("Player Name Input", uiRoot);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.75f);
            rect.anchoredPosition = new Vector2(0, 0);
            rect.sizeDelta = new Vector2(520, 54);

            var image = root.AddComponent<Image>();
            image.color = new Color(0.04f, 0.07f, 0.12f, 0.92f);

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
            placeholder.color = new Color(1, 1, 1, 0.45f);

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

        private IEnumerator CountdownToGameplay()
        {
            countdownRunning = true;
            yield return new WaitForSeconds(Mathf.Max(0f, countdownSeconds));
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
                case GameSessionState.Result:
                    return normalCharacter;
                default:
                    return null;
            }
        }
    }
}
