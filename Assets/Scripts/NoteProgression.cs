
using System;
using System.Collections;
using UnityEngine;

public class NoteProgression : MonoBehaviour
{
    public event Action OnCompleted;

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
            judgmentSystem.OnJudged += HandleJudgment;
    }

    private void OnDisable()
    {
        if (judgmentSystem != null)
            judgmentSystem.OnJudged -= HandleJudgment;

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

        notePositions = (float[])positions.Clone();
        currentNoteIndex = 0;
        isPlaying = true;
        judgmentSystem.enabled = true;

        noteDisplay.Initialize(notePositions);
        judgmentUI.ClearJudgment();
        judgmentSystem.ResetJudgment(notePositions[0]);

        Debug.Log($"Note 1/{notePositions.Length}");
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
}
