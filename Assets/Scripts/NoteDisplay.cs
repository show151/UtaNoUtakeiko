
using System.Collections;
using UnityEngine;

public class NoteDisplay : MonoBehaviour
{

    private readonly System.Collections.Generic.List<SpriteRenderer> noteRenderers
        = new System.Collections.Generic.List<SpriteRenderer>();

    [SerializeField] private Sprite noteSprite;

    // Gameplay input visuals: a connected-pitch unit is shown as a long bar.
    private readonly System.Collections.Generic.List<SpriteRenderer> holdBodyRenderers
        = new System.Collections.Generic.List<SpriteRenderer>();
    private readonly System.Collections.Generic.List<bool> holdFlags
        = new System.Collections.Generic.List<bool>();

    [Header("Layout")]
    [SerializeField] private float startX = -2.5f;
    [SerializeField] private float noteSpacing = 1.5f;
    [SerializeField] private float minY = -3f;
    [SerializeField] private float maxY = 3f;
    [SerializeField] private float noteSize = 0.35f;

    [Header("Scroll")]
    [SerializeField] private float scrollDuration = 0.4f;

    private Vector3 initialPosition;

    private void Awake()
    {
        // シーン開始時の位置を一度だけ保存
        initialPosition = transform.position;
    }

    public void Initialize(float[] notePositions)
    {
        // 毎回、最初の譜面位置へ戻す
        transform.position = initialPosition;

        noteRenderers.Clear();
        holdBodyRenderers.Clear();
        holdFlags.Clear();

        foreach (Transform child in transform)
        {
            // 以前の音符を即座に非表示
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        for (int i = 0; i < notePositions.Length; i++)
        {
            GameObject note = new GameObject($"Note_{i + 1}");
            note.transform.SetParent(transform, false);

            float x = startX + i * noteSpacing;
            float y = Mathf.Lerp(
                minY,
                maxY,
                Mathf.Clamp01(notePositions[i])
            );

            note.transform.localPosition = new Vector3(x, y, 0f);
            note.transform.localScale =
                new Vector3(noteSize, noteSize, 1f);

            SpriteRenderer renderer =
                note.AddComponent<SpriteRenderer>();

            renderer.sprite = noteSprite;
            renderer.color = new Color32(36, 91, 133, 255);
            renderer.sortingOrder = 2;

            noteRenderers.Add(renderer);
            holdBodyRenderers.Add(null);
            holdFlags.Add(false);
        }
    }



    // Render a continuous same-pitch input as a connected bar. Original
    // per-note data and the 70% timeline are intentionally not changed.
    public void InitializeInputUnits(
        float[] positions,
        System.Collections.Generic.IReadOnlyList<UtaNoUtakeiko.Data.InputUnit> units)
    {
        if (positions == null || units == null || positions.Length != units.Count)
        {
            Debug.LogError("入力区間の表示データが不正です。");
            return;
        }

        Initialize(positions);
        for (int i = 0; i < units.Count; i++)
        {
            if (!units[i].IsHold)
                continue;

            holdFlags[i] = true;
            SpriteRenderer head = noteRenderers[i];
            head.color = new Color32(255, 175, 66, 255);

            // Reuse the configured note sprite; no additional art asset is required.
            GameObject body = new GameObject("HoldBody");
            body.transform.SetParent(head.transform, false);
            body.transform.localPosition = new Vector3(0.75f, 0f, 0f);
            body.transform.localScale = new Vector3(1.55f, 0.38f, 1f);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = noteSprite;
            bodyRenderer.color = new Color32(255, 175, 66, 210);
            bodyRenderer.sortingOrder = head.sortingOrder - 1;
            holdBodyRenderers[i] = bodyRenderer;
        }
    }

    public void ShowHoldProgress(int index, float progress)
    {
        if (index < 0 || index >= noteRenderers.Count || !holdFlags[index])
            return;

        float fraction = Mathf.Clamp01(progress);
        noteRenderers[index].color = Color.Lerp(
            new Color32(255, 175, 66, 255),
            new Color32(91, 235, 168, 255),
            fraction);
        if (holdBodyRenderers[index] != null)
            holdBodyRenderers[index].color = noteRenderers[index].color;
    }

    public void ShowInputResolution(int index, bool succeeded)
    {
        if (index < 0 || index >= noteRenderers.Count)
            return;

        Color color = succeeded
            ? new Color32(91, 235, 168, 255)
            : new Color32(165, 165, 175, 255);

        noteRenderers[index].color = color;
        if (holdBodyRenderers[index] != null)
            holdBodyRenderers[index].color = color;
    }

    // #53: jump to the scheduled input unit without adding scroll animation time.
    // Legacy ScrollToNote remains unchanged for the current gameplay route.
    public void SeekToNote(int noteIndex)
    {
        if (noteIndex < 0 || noteIndex >= noteRenderers.Count)
            return;

        transform.position = initialPosition +
            Vector3.left * (noteIndex * noteSpacing);

        for (int i = 0; i < noteRenderers.Count; i++)
        {
            SpriteRenderer renderer = noteRenderers[i];

            if (renderer != null)
                renderer.enabled = i >= noteIndex;

            // SpriteRenderer.enabled on the note does not propagate to
            // child SpriteRenderers. Hide hold bodies explicitly.
            if (holdBodyRenderers[i] != null)
                holdBodyRenderers[i].enabled = i >= noteIndex;
        }
    }


    public IEnumerator ScrollToNote(int noteIndex)
    {
        Vector3 from = transform.position;
        Vector3 to = initialPosition +
            Vector3.left * (noteIndex * noteSpacing);

        float elapsed = 0f;

        while (elapsed < scrollDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / scrollDuration);
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(from, to, t);

            // Fade out the previous note while scrolling.
            int previousIndex = noteIndex - 1;

            if (previousIndex >= 0 &&
                previousIndex < noteRenderers.Count)
            {
                SpriteRenderer renderer = noteRenderers[previousIndex];

                Color color = renderer.color;
                color.a = 1f - t;
                renderer.color = color;

                // The hold bar uses its own child SpriteRenderer.
                if (holdBodyRenderers[previousIndex] != null)
                {
                    var body = holdBodyRenderers[previousIndex];
                    Color bodyColor = body.color;
                    bodyColor.a = 1f - t;
                    body.color = bodyColor;
                }
            }

            yield return null;
        }

        transform.position = to;

        // Hide the previous note after the scroll finishes.
        int completedIndex = noteIndex - 1;

        if (completedIndex >= 0 &&
            completedIndex < noteRenderers.Count)
        {
            noteRenderers[completedIndex].enabled = false;
            if (holdBodyRenderers[completedIndex] != null)
                holdBodyRenderers[completedIndex].enabled = false;
        }
    }
}
