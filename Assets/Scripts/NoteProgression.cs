
using System;
using System.Collections;
using UnityEngine;

public class NoteProgression : MonoBehaviour
{
    public event Action OnCompleted;

    // Separate preview completion event; preview must never finalize a scored session.
    public event Action OnTimedPreviewCompleted;

    // NoteId、判定名、実際の入力位置
    public event Action<string, string, float> OnNoteJudged;

    // NoteId、通常判定、入力位置、押下時間
    public event Action<string, string, float, float>
        OnNoteJudgedWithDuration;

    private string[] noteIds;

    [SerializeField] private JudgmentSystem judgmentSystem;
    [SerializeField] private NoteDisplay noteDisplay;
    [SerializeField] private JudgmentUI judgmentUI;

    [SerializeField]
    private float[] notePositions =
    {
        0.25f, 0.75f, 0.5f, 0.9f, 0.1f
    };

    [SerializeField] private float delayAfterJudgment = 1f;

    private int currentNoteIndex;
    private bool isPlaying;
    private bool isAdvancing;
    private bool isTimedPreview;

    private void OnEnable()
    {
        if (judgmentSystem != null)
        {
            judgmentSystem.OnJudged += HandleJudgment;
            judgmentSystem.OnJudgedWithPosition += HandleJudgmentWithPosition;
            judgmentSystem.OnJudgedWithPositionAndDuration
                += HandleJudgmentWithDuration;
        }
    }

    private void OnDisable()
    {
        if (judgmentSystem != null)
        {
            judgmentSystem.OnJudged -= HandleJudgment;
            judgmentSystem.OnJudgedWithPosition -= HandleJudgmentWithPosition;
            judgmentSystem.OnJudgedWithPositionAndDuration
                -= HandleJudgmentWithDuration;
        }

        StopPlayback();
    }

    public void StartPlay(float[] positions)
    {
        if (positions == null || positions.Length == 0)
        {
            Debug.LogError("譜面データが空です。");
            return;
        }

        StopPlayback();

        noteIds = null;

        notePositions = (float[])positions.Clone();
        currentNoteIndex = 0;
        isPlaying = true;
        judgmentSystem.enabled = true;

        noteDisplay.Initialize(notePositions);
        judgmentUI.ClearJudgment();
        judgmentSystem.ResetJudgment(notePositions[0]);

        Debug.Log($"Note 1/{notePositions.Length}");
    }

    public void StartPlay(float[] positions, string[] ids)
    {
        if (positions == null ||
            ids == null ||
            positions.Length == 0 ||
            positions.Length != ids.Length ||
            Array.Exists(ids, string.IsNullOrWhiteSpace))
        {
            Debug.LogError("音符位置とNoteIdの対応が不正です。");
            return;
        }

        StartPlay(positions);

        if (isPlaying)
            noteIds = (string[])ids.Clone();
    }

    // Preview-only route: follow scheduled BPM-based input starts, ignoring player input.
    // The existing judgment/scoring path is intentionally untouched.
    public bool StartTimedPreview(
        float[] positions,
        string[] ids,
        float[] startSeconds,
        float totalSeconds)
    {
        if (positions == null || ids == null || startSeconds == null ||
            positions.Length == 0 || positions.Length != ids.Length ||
            positions.Length != startSeconds.Length ||
            float.IsNaN(totalSeconds) || float.IsInfinity(totalSeconds) ||
            totalSeconds <= 0f ||
            Array.Exists(ids, string.IsNullOrWhiteSpace))
        {
            Debug.LogError("時間軸プレビューのデータが不正です。");
            return false;
        }

        for (int i = 0; i < startSeconds.Length; i++)
        {
            if (float.IsNaN(startSeconds[i]) ||
                float.IsInfinity(startSeconds[i]) ||
                startSeconds[i] < 0f || startSeconds[i] >= totalSeconds ||
                (i > 0 && startSeconds[i] <= startSeconds[i - 1]))
            {
                Debug.LogError("プレビューの入力開始時刻が不正です。");
                return false;
            }
        }

        if (judgmentSystem == null || noteDisplay == null || judgmentUI == null)
        {
            Debug.LogError("プレビューに必要なコンポーネントがありません。");
            return false;
        }

        StopPlayback();
        notePositions = (float[])positions.Clone();
        noteIds = (string[])ids.Clone();
        currentNoteIndex = 0;
        isPlaying = true;
        isTimedPreview = true;
        judgmentSystem.enabled = false;

        noteDisplay.Initialize(notePositions);
        noteDisplay.SeekToNote(0);
        judgmentUI.ClearJudgment();
        StartCoroutine(RunTimedPreview((float[])startSeconds.Clone(), totalSeconds));
        return true;
    }

    private IEnumerator RunTimedPreview(float[] starts, float totalSeconds)
    {
        float startedAt = Time.time;
        int index = 0;
        Debug.Log($"[Issue #53] 70% timing preview started: " +
                  $"{starts.Length} input units / {totalSeconds:F2}s");

        while (isTimedPreview && Time.time - startedAt < totalSeconds)
        {
            float elapsed = Time.time - startedAt;
            while (index + 1 < starts.Length && elapsed >= starts[index + 1])
            {
                index++;
                currentNoteIndex = index;
                noteDisplay.SeekToNote(index);
                judgmentUI.ClearJudgment();
                Debug.Log($"[Issue #53] Input {index + 1}/{starts.Length} at {elapsed:F2}s");
            }
            yield return null;
        }

        if (!isTimedPreview)
            yield break;

        Debug.Log($"[Issue #53] 70% timing preview finished: {totalSeconds:F2}s");
        StopPlayback();
        OnTimedPreviewCompleted?.Invoke();
    }

    public void StopPlayback()
    {
        isPlaying = false;
        isAdvancing = false;
        isTimedPreview = false;
        StopAllCoroutines();

        if (judgmentSystem != null)
            judgmentSystem.enabled = false;
    }

    private void HandleJudgment(string result)
    {
        if (!isPlaying || isAdvancing || isTimedPreview)
            return;

        isAdvancing = true;
        StartCoroutine(AdvanceAfterDelay());
    }

    private IEnumerator AdvanceAfterDelay()
    {
        yield return new WaitForSeconds(delayAfterJudgment);

        int nextIndex = currentNoteIndex + 1;

        if (nextIndex >= notePositions.Length)
        {
            StopPlayback();
            Debug.Log("Note input completed!");
            OnCompleted?.Invoke();
            yield break;
        }

        yield return noteDisplay.ScrollToNote(nextIndex);

        if (!isPlaying)
            yield break;

        currentNoteIndex = nextIndex;
        judgmentUI.ClearJudgment();
        judgmentSystem.ResetJudgment(notePositions[currentNoteIndex]);
        isAdvancing = false;
    }

    private void HandleJudgmentWithPosition(
        string result,
        float position)
    {
        if (!isPlaying ||
            isTimedPreview ||
            isAdvancing ||
            noteIds == null ||
            currentNoteIndex < 0 ||
            currentNoteIndex >= noteIds.Length)
            {
                return;
            }

        string noteId = noteIds[currentNoteIndex];

        Debug.Log(
            $"判定取得: NoteId={noteId}, " +
            $"Result={result}, Position={position:F3}"
        );

        OnNoteJudged?.Invoke(noteId, result, position);
    }

    private void HandleJudgmentWithDuration(
        string result,
        float position,
        float heldSeconds)
    {
        if (!isPlaying ||
            isTimedPreview ||
            isAdvancing ||
            noteIds == null ||
            currentNoteIndex < 0 ||
            currentNoteIndex >= noteIds.Length)
        {
            return;
        }

        string noteId = noteIds[currentNoteIndex];

        OnNoteJudgedWithDuration?.Invoke(
            noteId,
            result,
            position,
            heldSeconds
        );
    }
}
