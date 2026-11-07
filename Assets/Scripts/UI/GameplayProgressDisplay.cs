using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UtaNoUtakeiko.Gameplay;

namespace Uta.GameFlow
{
    // Counts input units once each, including the individual notes in a stair.
    public sealed class GameplayProgressModel
    {
        private readonly HashSet<string> resolved = new HashSet<string>();
        public void Reset() => resolved.Clear();
        public void Record(string unitId)
        {
            if (!string.IsNullOrEmpty(unitId)) resolved.Add(unitId);
        }
        public float Fraction(int total) => total > 0 ? Mathf.Clamp01((float)resolved.Count / total) : 0f;
    }

    public sealed class GameplayProgressDisplay : MonoBehaviour
    {
        [SerializeField] private GameSessionManager sessionManager;
        [SerializeField] private GameSessionPlayConnector playConnector;
        [SerializeField] private NoteProgression noteProgression;

        private readonly GameplayProgressModel progress = new GameplayProgressModel();
        private GameObject canvasObject;
        private RectTransform fillRect;

        private void Awake()
        {
            canvasObject = new GameObject("Gameplay Progress Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 21;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // A dark outer frame keeps the bar readable against bright artwork.
            var frame = MakeImage("Progress Frame", canvasObject.transform, new Color32(15, 25, 40, 255));
            var rect = frame.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.32f, 0.95f);
            rect.sizeDelta = new Vector2(660f, 38f);
            var track = MakeImage("Progress Track", frame.transform, new Color32(100, 114, 126, 255));
            Stretch(track.rectTransform, 5f);
            var fill = MakeImage("Progress Fill", track.transform, new Color32(255, 201, 74, 255));
            fillRect = fill.rectTransform;
            Stretch(fillRect, 0f);
            SetFraction(0f);
            canvasObject.SetActive(false);
        }

        private static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private void OnEnable()
        {
            if (sessionManager != null) sessionManager.OnStateChanged += OnStateChanged;
            if (noteProgression != null) noteProgression.OnGameplayInputResolved += OnInputResolved;
            if (canvasObject != null) canvasObject.SetActive(
                sessionManager != null && sessionManager.CurrentState == GameSessionState.Playing);
        }

        private void OnDisable()
        {
            if (sessionManager != null) sessionManager.OnStateChanged -= OnStateChanged;
            if (noteProgression != null) noteProgression.OnGameplayInputResolved -= OnInputResolved;
            if (canvasObject != null) canvasObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject);
        }

        private void OnStateChanged(GameSessionState previous, GameSessionState next)
        {
            if (next == GameSessionState.Playing)
            {
                progress.Reset();
                SetFraction(0f);
            }
            canvasObject.SetActive(next == GameSessionState.Playing);
        }

        private void OnInputResolved(TimedInputTrialResult result)
        {
            if (sessionManager == null || sessionManager.CurrentState != GameSessionState.Playing || result == null)
                return;
            progress.Record(result.UnitId);
            int total = playConnector != null && playConnector.CurrentInputUnits != null
                ? playConnector.CurrentInputUnits.Count : 0;
            SetFraction(progress.Fraction(total));
        }

        private void SetFraction(float fraction)
        {
            if (fillRect == null) return;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }
    }
}
