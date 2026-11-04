using UnityEngine;
using UnityEngine.UI;

namespace Uta.GameFlow
{
    /// <summary>
    /// Displays replaceable artwork without covering the gameplay render layers.
    /// The title and menu states share the title artwork; the play wallpaper is
    /// drawn by a low-sorting-order SpriteRenderer behind the existing game scene.
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

        private Canvas screenCanvas;
        private Image backgroundImage;
        private Image characterImage;
        private SpriteRenderer wallpaperRenderer;
        private Camera gameplayCamera;

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

        private void LateUpdate()
        {
            if (wallpaperRenderer != null && wallpaperRenderer.enabled)
                FitWallpaperToCamera();
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
                typeof(CanvasScaler));
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

            var wallpaperObject = new GameObject("Play Wallpaper Background");
            wallpaperRenderer = wallpaperObject.AddComponent<SpriteRenderer>();
            wallpaperRenderer.sortingOrder = -1000;
            wallpaperRenderer.enabled = false;
            gameplayCamera = Camera.main;
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
                wallpaperRenderer.enabled = useWorldWallpaper &&
                                            playWallpaper != null;
                if (wallpaperRenderer.enabled)
                    FitWallpaperToCamera();
            }

            // Screen-space artwork is disabled while input is active so it can
            // never cover notes, judgment visuals, or their existing controls.
            screenCanvas.gameObject.SetActive(!isPlaying);

            Sprite backdrop = useWorldWallpaper ? null : titleArtwork;
            backgroundImage.sprite = backdrop;
            backgroundImage.gameObject.SetActive(backdrop != null);

            Sprite character = CharacterFor(state);
            characterImage.sprite = character;
            characterImage.gameObject.SetActive(character != null);
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

            float scale = Mathf.Max(
                visibleWidth / spriteSize.x,
                visibleHeight / spriteSize.y);

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
