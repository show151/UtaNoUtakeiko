using UnityEngine;

namespace Uta.GameFlow
{
    // Marks the fixed position where the next note will be judged.
    public sealed class ActiveNoteLane : MonoBehaviour
    {
        [SerializeField] private GameSessionManager sessionManager;
        [SerializeField] private NoteDisplay noteDisplay;
        [SerializeField, Min(0.1f)] private float width = 1f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.28f;
        private GameObject lane;
        private Sprite sprite;

        private void Awake()
        {
            lane = new GameObject("Active Note Lane");
            lane.transform.SetParent(transform, false);
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1f);
            var renderer = lane.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0f, 0f, 0f, opacity);
            renderer.sortingOrder = 1; // In front of staff, behind notes and judgments.
            lane.SetActive(false);
        }

        private void OnEnable()
        {
            if (sessionManager != null) sessionManager.OnStateChanged += OnStateChanged;
            RefreshVisibility();
        }

        private void OnDisable()
        {
            if (sessionManager != null) sessionManager.OnStateChanged -= OnStateChanged;
            if (lane != null) lane.SetActive(false);
        }

        private void OnDestroy()
        {
            if (sprite != null) Destroy(sprite);
        }

        private void OnStateChanged(GameSessionState previous, GameSessionState next) => RefreshVisibility();

        private void RefreshVisibility()
        {
            if (lane != null) lane.SetActive(sessionManager != null && noteDisplay != null &&
                sessionManager.CurrentState == GameSessionState.Playing);
        }

        private void LateUpdate()
        {
            if (lane == null || !lane.activeSelf || noteDisplay == null) return;
            var camera = Camera.main;
            if (camera == null || !camera.orthographic) return;
            lane.transform.position = new Vector3(noteDisplay.InputLaneWorldX, camera.transform.position.y, 0f);
            lane.transform.localScale = new Vector3(width, camera.orthographicSize * 2f, 1f);
        }
    }
}
