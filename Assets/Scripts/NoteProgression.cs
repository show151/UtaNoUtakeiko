
using System.Collections;
using UnityEngine;

public class NoteProgression : MonoBehaviour
{
    [SerializeField] private JudgmentSystem judgmentSystem;
    [SerializeField]
    private float[] notePositions =
    {
        0.25f, 0.75f, 0.5f, 0.9f, 0.1f
    };

    [SerializeField] private float delayAfterJudgment = 1f;

    private int currentNoteIndex = 0;

    private void OnEnable()
    {
        judgmentSystem.OnJudged += HandleJudgment;
    }

    private void OnDisable()
    {
        judgmentSystem.OnJudged -= HandleJudgment;
    }


    private void Start()
    {
        if (notePositions == null || notePositions.Length == 0)
        {
            Debug.LogError("譜面データが設定されていません。");
            return;
        }

        currentNoteIndex = 0;

        float firstTarget = Mathf.Clamp01(notePositions[0]);

        judgmentSystem.ResetJudgment(firstTarget);

        Debug.Log(
            $"Note 1/{notePositions.Length}, " +
            $"Target: {firstTarget:F2}"
        );
    }


    private void HandleJudgment(string result)
    {
        StartCoroutine(AdvanceAfterDelay());
    }

    private IEnumerator AdvanceAfterDelay()
    {
        yield return new WaitForSeconds(delayAfterJudgment);

        currentNoteIndex++;

        if (currentNoteIndex >= notePositions.Length)
        {
            Debug.Log("Game Finished!");
            yield break;
        }

        judgmentSystem.ResetJudgment(
            notePositions[currentNoteIndex]
        );

        Debug.Log(
            $"Note {currentNoteIndex + 1}/{notePositions.Length}, " +
            $"Target: {notePositions[currentNoteIndex]:F2}"
        );
    }
}
