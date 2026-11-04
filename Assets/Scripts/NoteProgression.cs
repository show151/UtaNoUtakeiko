
using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

public class NoteProgression : MonoBehaviour
{
    public event Action OnCompleted;

    // Separate preview completion event; preview must never finalize a scored session.
    public event Action OnTimedPreviewCompleted;

    // Isolated interactive test: results are never forwarded to the old score path.
    public event Action OnTimedTrialCompleted;
    public event Action<TimedInputTrialResult> OnTimedTrialResolved;
    public IReadOnlyList<TimedInputTrialResult> LastTimedTrialResults { get; private set; }

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
    private bool isTimedTrial;
    private TimedInputTrial timedTrial;
    private readonly List<TimedInputTrialResult> completedTrialResults = new List<TimedInputTrialResult>();
    private float trialStartedAt;
    private int pendingPressIndex = -1;
    private float holdToleranceSeconds = 0.05f;
    private float timedPressStartedAt;
    private float pendingUnitDuration;
    private List<InputUnit> currentTimedUnits;
    private bool pendingIsHold;

    private void OnEnable()
    {
        if (judgmentSystem != null)
        {
            judgmentSystem.OnJudged += HandleJudgment;
            judgmentSystem.OnPressPositionCaptured += HandleTimedPressStarted;
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
            judgmentSystem.OnPressPositionCaptured -= HandleTimedPressStarted;
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

    // Actual button input trial. Time always advances; missed units are auto-resolved.
    // Kept independent from the legacy JudgmentSession and vocal scoring until #53
    // is fully integrated.
    public bool StartTimedInputTrial(
        List<InputUnit> units,
        float[] positions,
        float holdTolerance = 0.05f)
    {
        if (units == null || units.Count == 0 ||
            positions == null || positions.Length != units.Count ||
            judgmentSystem == null || noteDisplay == null ||
            judgmentUI == null || float.IsNaN(holdTolerance) ||
            float.IsInfinity(holdTolerance) || holdTolerance < 0f)
        {
            Debug.LogError("時間制入力テストの設定が不正です。");
            return false;
        }

        TimedInputTrial newTrial;
        try
        {
            newTrial = new TimedInputTrial(units);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }

        StopPlayback();

        completedTrialResults.Clear();
        LastTimedTrialResults = null;
        currentTimedUnits = new List<InputUnit>(units);
        timedTrial = newTrial;
        holdToleranceSeconds = holdTolerance;
        timedTrial.OnResolved += HandleTrialResolved;
        notePositions = (float[])positions.Clone();
        noteIds = new string[units.Count];
        for (int i = 0; i < units.Count; i++)
            noteIds[i] = units[i].UnitId;

        currentNoteIndex = 0;
        pendingPressIndex = -1;
        isPlaying = true;
        isTimedTrial = true;
        noteDisplay.InitializeInputUnits(notePositions, units);
        noteDisplay.SeekToNote(0);
        judgmentUI.SetTimedInputFeedback(true);
        judgmentUI.ShowTimedInputPrompt(units[0].IsHold);
        judgmentSystem.enabled = true;
        judgmentSystem.ResetJudgment(notePositions[0]);

        trialStartedAt = Time.time;
        StartCoroutine(RunTimedInputTrial(
            new List<InputUnit>(units),
            units[units.Count - 1].EndSeconds));
        return true;
    }

    private void HandleTimedPressStarted(float capturedPosition)
    {
        if (!isTimedTrial || timedTrial == null)
            return;

        float elapsed = Time.time - trialStartedAt;
        int active = timedTrial.ActiveIndexAt(elapsed);
        pendingPressIndex = active >= 0 && !timedTrial.IsResolved(active)
            ? active : -1;
        timedPressStartedAt = Time.time;
        pendingIsHold = pendingPressIndex >= 0 &&
            currentTimedUnits != null && currentTimedUnits[pendingPressIndex].IsHold;
        pendingUnitDuration = pendingPressIndex >= 0 && currentTimedUnits != null
            ? currentTimedUnits[pendingPressIndex].DurationSeconds : 0f;
    }

    private void HandleTrialResolved(TimedInputTrialResult result)
    {
        completedTrialResults.Add(result);
        if (currentTimedUnits != null)
        {
            for (int i = 0; i < currentTimedUnits.Count; i++)
            {
                if (currentTimedUnits[i].UnitId != result.UnitId)
                    continue;
                bool succeeded = !result.TimedOut &&
                    result.PitchJudgment != "Miss" && result.HoldSucceeded != false;
                noteDisplay.ShowInputResolution(i, succeeded);
                if (i == currentNoteIndex)
                    judgmentUI.ShowTimedInputResult(
                        result.PitchJudgment, result.HoldSucceeded, result.TimedOut);
                break;
            }
        }
        Debug.Log(
            $"[Issue #53] Input result {result.UnitId}: " +
            $"{result.PitchJudgment}, hold={result.HoldSucceeded}, " +
            $"timeout={result.TimedOut}");
        OnTimedTrialResolved?.Invoke(result);
    }

    private IEnumerator RunTimedInputTrial(
        List<InputUnit> units, float totalSeconds)
    {
        int index = 0;
        Debug.Log(
            $"[Issue #53] Timed input trial started: " +
            $"{units.Count} units / {totalSeconds:F2}s");

        while (isTimedTrial)
        {
            float elapsed = Time.time - trialStartedAt;
            timedTrial.Expire(elapsed);

            if (pendingPressIndex >= 0 && pendingIsHold &&
                !timedTrial.IsResolved(pendingPressIndex) &&
                pendingUnitDuration > 0f)
            {
                float fraction = Mathf.Clamp01(
                    (Time.time - timedPressStartedAt) / pendingUnitDuration);
                noteDisplay.ShowHoldProgress(pendingPressIndex, fraction);
                if (pendingPressIndex == currentNoteIndex)
                    judgmentUI.ShowTimedHoldProgress(fraction);
            }

            while (index + 1 < units.Count &&
                   elapsed >= units[index + 1].StartSeconds)
            {
                index++;
                currentNoteIndex = index;
                noteDisplay.SeekToNote(index);
                judgmentUI.ClearJudgment();
                judgmentUI.ShowTimedInputPrompt(units[index].IsHold);
                if (!judgmentSystem.HasCapturedPress)
                    judgmentSystem.ResetJudgment(notePositions[index]);
            }

            if (elapsed >= totalSeconds +
                           TimedInputTrial.ReleaseGraceSeconds)
                break;

            yield return null;
        }

        if (!isTimedTrial)
            yield break;

        timedTrial.Expire(totalSeconds +
                          TimedInputTrial.ReleaseGraceSeconds);
        int resolved = timedTrial.ResolvedCount;
        Debug.Log(
            $"[Issue #53] Timed input trial finished: " +
            $"{resolved}/{timedTrial.Count} resolved / " +
            $"{totalSeconds:F2}s + release grace");

        LastTimedTrialResults = completedTrialResults.AsReadOnly();
        StopPlayback();
        OnTimedTrialCompleted?.Invoke();
    }

    public void StopPlayback()
    {
        isPlaying = false;
        isAdvancing = false;
        isTimedPreview = false;
        isTimedTrial = false;
        pendingPressIndex = -1;
        pendingIsHold = false;
        pendingUnitDuration = 0f;
        currentTimedUnits = null;
        if (judgmentUI != null)
            judgmentUI.SetTimedInputFeedback(false);
        if (timedTrial != null)
        {
            timedTrial.OnResolved -= HandleTrialResolved;
            timedTrial = null;
        }
        StopAllCoroutines();

        if (judgmentSystem != null)
            judgmentSystem.enabled = false;
    }

    private void HandleJudgment(string result)
    {
        if (!isPlaying || isAdvancing || isTimedPreview || isTimedTrial)
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
            isTimedTrial ||
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
        if (isTimedTrial && timedTrial != null)
        {
            int sourceIndex = pendingPressIndex;
            pendingPressIndex = -1;
            pendingIsHold = false;
            if (sourceIndex >= 0)
            {
                bool accepted = timedTrial.TryRecordRelease(
                    sourceIndex, Time.time - trialStartedAt,
                    result, position, heldSeconds, holdToleranceSeconds);
                if (!accepted)
                    Debug.Log($"[Issue #53] Late/duplicate release ignored: {sourceIndex}");
            }

            if (isTimedTrial && currentNoteIndex < notePositions.Length)
                judgmentSystem.ResetJudgment(notePositions[currentNoteIndex]);
            return;
        }

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
