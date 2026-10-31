
using System.Collections;
using UnityEngine;

public class NoteProgression : MonoBehaviour
{
    [SerializeField] private JudgmentSystem judgmentSystem;
    [SerializeField] private NoteDisplay noteDisplay;
    [SerializeField] private JudgmentUI judgmentUI;


    [SerializeField]
    private float[] notePositions =
    {
        0.25f, 0.75f, 0.5f, 0.9f, 0.1f
    };

    [SerializeField] private float delayAfterJudgment = 1f;

    private int currentNoteIndex = 0;

    private void OnEnable()
    {
        if (judgmentSystem != null)
            judgmentSystem.OnJudged += HandleJudgment;
    }

    private void OnDisable()
    {
        if (judgmentSystem != null)
            judgmentSystem.OnJudged -= HandleJudgment;
    }

    private void Start()
    {
        if (notePositions == null || notePositions.Length == 0)
        {
            Debug.LogError("���ʃf�[�^���ݒ肳��Ă��܂���B");
            enabled = false;
            return;
        }

        noteDisplay.Initialize(notePositions);

        currentNoteIndex = 0;
        judgmentSystem.ResetJudgment(notePositions[0]);

        Debug.Log(
            $"Note 1/{notePositions.Length}, " +
            $"Target: {notePositions[0]:F2}"
        );
    }

    private void HandleJudgment(string result)
    {
        StartCoroutine(AdvanceAfterDelay());
    }

    private IEnumerator AdvanceAfterDelay()
    {
        yield return new WaitForSeconds(delayAfterJudgment);

        int nextIndex = currentNoteIndex + 1;

        if (nextIndex >= notePositions.Length)
        {
            Debug.Log("Game Finished!");
            yield break;
        }

        yield return StartCoroutine(
            noteDisplay.ScrollToNote(nextIndex)
        );

        currentNoteIndex = nextIndex;


        judgmentUI.ClearJudgment();


        judgmentSystem.ResetJudgment(
            notePositions[currentNoteIndex]
        );

        Debug.Log(
            $"Note {currentNoteIndex + 1}/{notePositions.Length}, " +
            $"Target: {notePositions[currentNoteIndex]:F2}"
        );
    }
}
