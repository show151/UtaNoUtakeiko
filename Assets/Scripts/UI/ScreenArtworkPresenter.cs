using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Uta.GameFlow
{
    /// <summary>
    /// Presents the existing session states with replaceable artwork. The UI is
    /// generated at runtime so sprite references live on this component in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenArtworkPresenter : MonoBehaviour
    {
        [SerializeField] private GameSessionManager sessionManager;
        [SerializeField] private GameSessionPlayConnector playConnector;

        [Header("Typography")]
        [SerializeField] private TMP_FontAsset japaneseFont;

        [Header("Artwork (Sprite / 2D and UI)")]
        [SerializeField] private Sprite titleArtwork;
        [SerializeField] private Sprite normalCharacter;
        [SerializeField] private Sprite singingGoodCharacter;
        [SerializeField] private Sprite singingBadCharacter;
        [SerializeField] private Sprite playWallpaper;

        [Header("Result artwork selection")]
        [SerializeField, Range(0f, 100f)]
        private float goodResultThresholdPercent = 70f;

        private Canvas screenCanvas;
        private Image backgroundImage;
        private Image contrastShade;
        private GameObject informationPanel;
        private Image characterImage;
        private TMP_Text stateLabel;
        private TMP_Text heading;
        private TMP_Text description;
        private TMP_Text prompt;

        private void OnEnable()
        {
            if (sessionManager != null)
                sessionManager.OnStateChanged += HandleStateChanged;
            if (screenCanvas != null)
                screenCanvas.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (sessionManager != null)
                sessionManager.OnStateChanged -= HandleStateChanged;
            if (screenCanvas != null)
                screenCanvas.gameObject.SetActive(false);
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

        private void HandleStateChanged(
            GameSessionState previous,
            GameSessionState next)
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
                "Background", canvasObject.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.055f, 0.075f, 0.16f, 1f));
            contrastShade = CreateImage(
                "Artwork Contrast Shade", canvasObject.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.035f, 0.09f, 0.28f));
            contrastShade.raycastTarget = false;

            var panel = CreateImage(
                "Information Panel", canvasObject.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(100f, 0f), new Vector2(880f, 410f),
                new Color(0.035f, 0.055f, 0.13f, 0.88f));
            panel.rectTransform.pivot = new Vector2(0f, 0.5f);
            informationPanel = panel.gameObject;

            stateLabel = CreateText(
                "Current Screen", canvasObject.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(100f, 150f), new Vector2(760f, 48f),
                26f, new Color(0.55f, 0.88f, 1f, 1f),
                TextAlignmentOptions.Left);

            heading = CreateText(
                "Screen Heading", canvasObject.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(100f, 54f), new Vector2(780f, 112f),
                58f, Color.white, TextAlignmentOptions.Left);

            description = CreateText(
                "Screen Description", canvasObject.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(100f, -44f), new Vector2(780f, 80f),
                30f, new Color(0.88f, 0.92f, 1f, 1f),
                TextAlignmentOptions.Left);

            prompt = CreateText(
                "Action Prompt", canvasObject.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(100f, -145f), new Vector2(780f, 56f),
                28f, new Color(1f, 0.82f, 0.32f, 1f),
                TextAlignmentOptions.Left);

            characterImage = CreateImage(
                "Character Artwork", canvasObject.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-300f, 70f), new Vector2(600f, 850f),
                Color.white);
            characterImage.rectTransform.pivot = new Vector2(0.5f, 0f);
            characterImage.preserveAspect = true;
            characterImage.raycastTarget = false;
        }

        private static Image CreateImage(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            var imageObject = new GameObject(
                objectName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));
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

        private TMP_Text CreateText(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(
                objectName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = japaneseFont != null
                ? japaneseFont
                : Resources.Load<TMP_FontAsset>(
                    "Fonts & Materials/LiberationSans SDF");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private void ApplyState(GameSessionState state)
        {
            if (screenCanvas == null)
                return;

            bool isGameplay = state == GameSessionState.Playing ||
                              state == GameSessionState.Playback;
            Sprite backdrop = isGameplay ? playWallpaper : titleArtwork;
            backgroundImage.sprite = backdrop;
            bool activeInput = state == GameSessionState.Playing;
            backgroundImage.color = backdrop != null
                ? new Color(1f, 1f, 1f, activeInput ? 0.3f : 1f)
                : WithAlpha(StateFallbackColor(state), activeInput ? 0.18f : 1f);
            contrastShade.color = new Color(0.02f, 0.035f, 0.09f,
                activeInput ? 0.08f : 0.28f);

            informationPanel.SetActive(!activeInput);
            stateLabel.gameObject.SetActive(!activeInput);
            heading.gameObject.SetActive(!activeInput);
            description.gameObject.SetActive(!activeInput);
            prompt.gameObject.SetActive(!activeInput);

            stateLabel.text = StateName(state);
            heading.text = Heading(state);
            description.text = Description(state);
            prompt.text = Prompt(state);

            Sprite character = CharacterFor(state);
            characterImage.sprite = character;
            characterImage.gameObject.SetActive(character != null);
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
                case GameSessionState.Countdown:
                case GameSessionState.Playing:
                case GameSessionState.Result:
                case GameSessionState.Ranking:
                case GameSessionState.Announcement:
                    return normalCharacter;

                default:
                    return null;
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Color StateFallbackColor(GameSessionState state)
        {
            switch (state)
            {
                case GameSessionState.Playing:
                    return new Color(0.055f, 0.12f, 0.2f, 1f);
                case GameSessionState.Playback:
                    return new Color(0.15f, 0.075f, 0.22f, 1f);
                case GameSessionState.Error:
                    return new Color(0.24f, 0.035f, 0.06f, 1f);
                default:
                    return new Color(0.055f, 0.075f, 0.16f, 1f);
            }
        }

        private static string StateName(GameSessionState state)
        {
            switch (state)
            {
                case GameSessionState.Title: return "TITLE";
                case GameSessionState.DifficultySelect: return "DIFFICULTY SELECT";
                case GameSessionState.SongSelect: return "SONG SELECT";
                case GameSessionState.Countdown: return "COUNTDOWN";
                case GameSessionState.Playing: return "PLAYING";
                case GameSessionState.Playback: return "SINGING";
                case GameSessionState.Result: return "RESULT";
                case GameSessionState.Ranking: return "RANKING";
                case GameSessionState.Announcement: return "ANNOUNCEMENT";
                default: return "ERROR";
            }
        }

        private static string Heading(GameSessionState state)
        {
            switch (state)
            {
                case GameSessionState.Title: return "ウタの歌稽古";
                case GameSessionState.DifficultySelect: return "難易度を選択";
                case GameSessionState.SongSelect: return "曲を選択";
                case GameSessionState.Countdown: return "準備してください";
                case GameSessionState.Playing: return "あなたの番です";
                case GameSessionState.Playback: return "歌唱中";
                case GameSessionState.Result: return "リザルト";
                case GameSessionState.Ranking: return "ランキング";
                case GameSessionState.Announcement: return "お知らせ";
                default: return "エラーが発生しました";
            }
        }

        private static string Description(GameSessionState state)
        {
            switch (state)
            {
                case GameSessionState.Title: return "歌って、演奏して、いっしょに楽しもう";
                case GameSessionState.DifficultySelect: return "挑戦する難易度を選んでください";
                case GameSessionState.SongSelect: return "演奏する曲を選んでください";
                case GameSessionState.Countdown: return "音が始まるまでお待ちください";
                case GameSessionState.Playing: return "画面の音程に合わせて歌ってください";
                case GameSessionState.Playback: return "入力結果に合わせて歌います";
                case GameSessionState.Result: return "プレイ結果を確認してください";
                case GameSessionState.Ranking: return "今回の順位を確認してください";
                case GameSessionState.Announcement: return "X-LIVEのお知らせ";
                default: return "スタッフにお知らせください";
            }
        }

        private static string Prompt(GameSessionState state)
        {
            switch (state)
            {
                case GameSessionState.Playback:
                    return "";
                case GameSessionState.Error:
                    return "スタッフの操作をお待ちください";
                default:
                    return "ENTER で次へ進む";
            }
        }
    }
}
