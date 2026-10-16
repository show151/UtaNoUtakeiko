
using System.Collections;
using UnityEngine;

public class NoteDisplay : MonoBehaviour
{

    private readonly System.Collections.Generic.List<SpriteRenderer> noteRenderers
        = new System.Collections.Generic.List<SpriteRenderer>();

    [SerializeField] private Sprite noteSprite;

    [Header("Layout")]
    [SerializeField] private float startX = -2.5f;
    [SerializeField] private float noteSpacing = 1.5f;
    [SerializeField] private float minY = -3f;
    [SerializeField] private float maxY = 3f;
    [SerializeField] private float noteSize = 0.35f;

    [Header("Scroll")]
    [SerializeField] private float scrollDuration = 0.4f;

    private Vector3 initialPosition;

    public void Initialize(float[] notePositions)
    {
        noteRenderers.Clear();

        initialPosition = transform.position;

        foreach (Transform child in transform)
        {
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

            // 判定済みの音符をスクロールに合わせてフェードアウト
            int previousIndex = noteIndex - 1;

            if (previousIndex >= 0 &&
                previousIndex < noteRenderers.Count)
            {
                SpriteRenderer renderer = noteRenderers[previousIndex];

                Color color = renderer.color;
                color.a = 1f - t;
                renderer.color = color;
            }

            yield return null;
        }

        transform.position = to;

        // スクロール完了時に判定済みの音符を非表示にする
        int completedIndex = noteIndex - 1;

        if (completedIndex >= 0 &&
            completedIndex < noteRenderers.Count)
        {
            noteRenderers[completedIndex].enabled = false;
        }
    }
}
