
using System.Collections;
using UnityEngine;

public class NoteDisplay : MonoBehaviour
{

    private readonly System.Collections.Generic.List<SpriteRenderer> noteRenderers
        = new System.Collections.Generic.List<SpriteRenderer>();

    [SerializeField] private Sprite noteSprite;

    // A solid white square sprite is used for the HOLD shaft instead of
    // stretching the rounded/irregular note sprite into a triangle.
    private Sprite holdShaftSprite;
    private readonly System.Collections.Generic.List<SpriteRenderer> holdCapRenderers
        = new System.Collections.Generic.List<SpriteRenderer>();
    private const float HoldBarLength = 3f;
    private const float HoldBarThickness = 1f;

    // Gameplay input visuals: a connected-pitch unit is shown as a long bar.
    private readonly System.Collections.Generic.List<SpriteRenderer> holdBodyRenderers
        = new System.Collections.Generic.List<SpriteRenderer>();
    private readonly System.Collections.Generic.List<SpriteRenderer> holdProgressRenderers
        = new System.Collections.Generic.List<SpriteRenderer>();
    private readonly System.Collections.Generic.List<bool> holdFlags
        = new System.Collections.Generic.List<bool>();

    [Header("Layout")]
    [SerializeField] private float startX = -2.5f;
    [SerializeField] private float noteSpacing = 1.5f;
    [SerializeField] private float minY = -3f;
    [SerializeField] private float maxY = 3f;
    [SerializeField] private float noteSize = 1f;

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
        holdCapRenderers.Clear();
        holdProgressRenderers.Clear();
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
            holdCapRenderers.Add(null);
            holdProgressRenderers.Add(null);
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
        float nextX = startX;
        for (int i = 0; i < units.Count; i++)
        {
            SpriteRenderer head = noteRenderers[i];
            Vector3 position = head.transform.localPosition;
            position.x = nextX;
            head.transform.localPosition = position;
            // Keep the following note beyond the HOLD's end cap.
            nextX += noteSpacing + (units[i].IsHold ? HoldBarLength * noteSize : 0f);
            if (!units[i].IsHold)
                continue;

            holdFlags[i] = true;
            head.color = new Color32(255, 175, 66, 255);

            if (holdShaftSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                holdShaftSprite = Sprite.Create(
                    texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), texture.width);
            }

            GameObject body = new GameObject("HoldBody");
            body.transform.SetParent(head.transform, false);
            body.transform.localPosition = new Vector3(
                HoldBarLength * 0.5f, 0f, 0f);
            body.transform.localScale = new Vector3(
                HoldBarLength, HoldBarThickness, 1f);

            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = holdShaftSprite;
            bodyRenderer.color = new Color32(255, 175, 66, 255);
            bodyRenderer.sortingOrder = head.sortingOrder - 1;
            holdBodyRenderers[i] = bodyRenderer;

            // A bright overlay grows from the head to the end of the HOLD bar.
            GameObject progress = new GameObject("HoldProgress");
            progress.transform.SetParent(head.transform, false);
            progress.transform.localPosition = Vector3.zero;
            progress.transform.localScale = new Vector3(0f,
                HoldBarThickness, 1f);
            SpriteRenderer progressRenderer = progress.AddComponent<SpriteRenderer>();
            progressRenderer.sprite = holdShaftSprite;
            progressRenderer.color = new Color32(255, 255, 150, 255);
            progressRenderer.sortingOrder = head.sortingOrder;
            progressRenderer.enabled = false;
            holdProgressRenderers[i] = progressRenderer;

            // Draw the note head and cap above the lit portion.
            head.sortingOrder = progressRenderer.sortingOrder + 1;
            GameObject cap = new GameObject("HoldEndCap");
            cap.transform.SetParent(head.transform, false);
            cap.transform.localPosition = new Vector3(
                HoldBarLength, 0f, 0f);
            cap.transform.localScale = Vector3.one;
            SpriteRenderer capRenderer = cap.AddComponent<SpriteRenderer>();
            capRenderer.sprite = noteSprite;
            capRenderer.color = new Color32(255, 175, 66, 255);
            capRenderer.sortingOrder = head.sortingOrder;
            holdCapRenderers[i] = capRenderer;
        }
    }

    public void ShowHoldProgress(int index, float progress)
    {
        if (index < 0 || index >= noteRenderers.Count || !holdFlags[index])
            return;

        float fraction = Mathf.Clamp01(progress);
        SpriteRenderer fill = holdProgressRenderers[index];
        if (fill == null)
            return;
        float length = HoldBarLength * fraction;
        fill.transform.localPosition = new Vector3(length * 0.5f, 0f, 0f);
        fill.transform.localScale = new Vector3(length,
            HoldBarThickness, 1f);
        fill.enabled = fraction > 0f && noteRenderers[index].enabled;
        // The first circle lights at press; the far circle lights when the
        // left-to-right fill reaches the end of the bar.
        noteRenderers[index].color = fraction >= 1f
            ? new Color32(91, 235, 168, 255)
            : new Color32(255, 255, 150, 255);
        if (holdCapRenderers[index] != null)
            holdCapRenderers[index].color = fraction >= 1f
                ? new Color32(91, 235, 168, 255)
                : new Color32(255, 175, 66, 255);
    }

    public void ResetInputProgress(int index)
    {
        if (index < 0 || index >= noteRenderers.Count || !holdFlags[index])
            return;

        SpriteRenderer fill = holdProgressRenderers[index];
        if (fill != null)
        {
            fill.enabled = false;
            fill.transform.localPosition = Vector3.zero;
            Vector3 scale = fill.transform.localScale;
            scale.x = 0f;
            fill.transform.localScale = scale;
            fill.color = new Color32(255, 255, 150, 255);
        }

        Color original = new Color32(255, 175, 66, 255);
        noteRenderers[index].color = original;
        if (holdBodyRenderers[index] != null)
            holdBodyRenderers[index].color = original;
        if (holdCapRenderers[index] != null)
            holdCapRenderers[index].color = original;
    }

    public void ShowInputResolution(int index, bool succeeded)
    {
        if (index < 0 || index >= noteRenderers.Count)
            return;

        // A completed HOLD always lights the whole bar, including a release
        // at the threshold between Update frames.
        if (holdFlags[index] && succeeded)
            ShowHoldProgress(index, 1f);

        Color color = succeeded
            ? new Color32(91, 235, 168, 255)
            : new Color32(165, 165, 175, 255);

        noteRenderers[index].color = color;
        if (holdProgressRenderers[index] != null)
            holdProgressRenderers[index].color = succeeded
                ? new Color32(185, 255, 215, 255)
                : new Color32(165, 165, 175, 255);
        if (holdBodyRenderers[index] != null)
            holdBodyRenderers[index].color = color;
        if (holdCapRenderers[index] != null)
            holdCapRenderers[index].color = color;
    }

    // #53: jump to the scheduled input unit without adding scroll animation time.
    // Legacy ScrollToNote remains unchanged for the current gameplay route.
    public void SeekToNote(int noteIndex)
    {
        if (noteIndex < 0 || noteIndex >= noteRenderers.Count)
            return;

        transform.position = initialPosition +
            Vector3.left * (noteRenderers[noteIndex].transform.localPosition.x - startX);

        for (int i = 0; i < noteRenderers.Count; i++)
        {
            SpriteRenderer renderer = noteRenderers[i];

            if (renderer != null)
                renderer.enabled = i >= noteIndex;

            // SpriteRenderer.enabled on the note does not propagate to
            // child SpriteRenderers. Hide hold bodies explicitly.
            if (holdBodyRenderers[i] != null)
                holdBodyRenderers[i].enabled = i >= noteIndex;
            if (holdCapRenderers[i] != null)
                holdCapRenderers[i].enabled = i >= noteIndex;
            if (holdProgressRenderers[i] != null)
                holdProgressRenderers[i].enabled = i >= noteIndex &&
                    holdProgressRenderers[i].transform.localScale.x > 0f;
        }
    }


    private void OnDestroy()
    {
        if (holdShaftSprite != null)
            Destroy(holdShaftSprite);
    }

    public IEnumerator ScrollToNote(int noteIndex)
    {
        Vector3 from = transform.position;
        Vector3 to = initialPosition +
            Vector3.left * (noteRenderers[noteIndex].transform.localPosition.x - startX);

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
                if (holdProgressRenderers[previousIndex] != null)
                {
                    var fill = holdProgressRenderers[previousIndex];
                    Color fillColor = fill.color;
                    fillColor.a = 1f - t;
                    fill.color = fillColor;
                }
                if (holdCapRenderers[previousIndex] != null)
                {
                    var cap = holdCapRenderers[previousIndex];
                    Color capColor = cap.color;
                    capColor.a = 1f - t;
                    cap.color = capColor;
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
            if (holdCapRenderers[completedIndex] != null)
                holdCapRenderers[completedIndex].enabled = false;
            if (holdProgressRenderers[completedIndex] != null)
                holdProgressRenderers[completedIndex].enabled = false;
        }
    }
}
