
using System;
using System.Collections;
using UnityEngine;

public class NoteProgression : MonoBehaviour
{
    public event Action OnCompleted;

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

    public void StopPlayback()
    {
        isPlaying = false;
        isAdvancing = false;
        StopAllCoroutines();

        if (judgmentSystem != null)
            judgmentSystem.enabled = false;
    }

    private void HandleJudgment(string result)
    {
        if (!isPlaying || isAdvancing)
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
