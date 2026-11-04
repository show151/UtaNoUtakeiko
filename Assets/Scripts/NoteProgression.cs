
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

    // Production gameplay input results for grouped TAP/HOLD scoring and vocals.
    public event Action OnGameplayCompleted;
    public event Action<TimedInputTrialResult> OnGameplayInputResolved;
    public IReadOnlyList<TimedInputTrialResult> LastGameplayResults { get; private set; }

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
    private bool isGameplayInput;
    private TimedInputTrial gameplayTrial;
    private readonly List<TimedInputTrialResult> completedTrialResults = new List<TimedInputTrialResult>();
    private float gameplayStartedAt;
    private int pendingPressIndex = -1;
    private float holdToleranceSeconds = 0.05f;
    private float timedPressStartedAt;
    private float pendingUnitDuration;
    private List<InputUnit> currentGameplayUnits;
    private bool pendingIsHold;

    private void OnEnable()
    {
        if (judgmentSystem != null)
        {
            judgmentSystem.OnJudged += HandleJudgment;
            judgmentSystem.OnPressPositionCaptured += HandleGameplayPressStarted;
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
            judgmentSystem.OnPressPositionCaptured -= HandleGameplayPressStarted;
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

    // Production gameplay: the player must press/release Space or an input device.
    // BPM timeline advances independently; unplayed units auto-resolve as Miss.
    public bool StartGameplayInput(
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
            Debug.LogError("本番ゲームプレイの設定が不正です。");
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
        LastGameplayResults = null;
        currentGameplayUnits = new List<InputUnit>(units);
        gameplayTrial = newTrial;
        holdToleranceSeconds = holdTolerance;
        gameplayTrial.OnResolved += HandleGameplayResolved;
        notePositions = (float[])positions.Clone();
        noteIds = new string[units.Count];
        for (int i = 0; i < units.Count; i++)
            noteIds[i] = units[i].UnitId;

        currentNoteIndex = 0;
        pendingPressIndex = -1;
        isPlaying = true;
        isGameplayInput = true;
        noteDisplay.InitializeInputUnits(notePositions, units);
        noteDisplay.SeekToNote(0);
        judgmentUI.SetTimedInputFeedback(true);
        judgmentUI.ShowTimedInputPrompt(units[0].IsHold);
        judgmentSystem.enabled = true;
        judgmentSystem.ResetJudgment(notePositions[0]);

        gameplayStartedAt = Time.time;
        StartCoroutine(RunGameplayInput(
            new List<InputUnit>(units),
            units[units.Count - 1].EndSeconds));
        return true;
    }

    private void HandleGameplayPressStarted(float capturedPosition)
    {
        if (!isGameplayInput || gameplayTrial == null)
            return;

        float elapsed = Time.time - gameplayStartedAt;
        int active = gameplayTrial.ActiveIndexAt(elapsed);
        pendingPressIndex = active >= 0 && !gameplayTrial.IsResolved(active)
            ? active : -1;
        timedPressStartedAt = Time.time;
        pendingIsHold = pendingPressIndex >= 0 &&
            currentGameplayUnits != null && currentGameplayUnits[pendingPressIndex].IsHold;
        pendingUnitDuration = pendingPressIndex >= 0 && currentGameplayUnits != null
            ? currentGameplayUnits[pendingPressIndex].DurationSeconds : 0f;
    }

    private void HandleGameplayResolved(TimedInputTrialResult result)
    {
        completedTrialResults.Add(result);
        if (currentGameplayUnits != null)
        {
            for (int i = 0; i < currentGameplayUnits.Count; i++)
            {
                if (currentGameplayUnits[i].UnitId != result.UnitId)
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
        OnGameplayInputResolved?.Invoke(result);
    }

    private IEnumerator RunGameplayInput(
        List<InputUnit> units, float totalSeconds)
    {
        int index = 0;
        Debug.Log(
            $"[Gameplay] Input started: " +
            $"{units.Count} units / {totalSeconds:F2}s");

        while (isGameplayInput)
        {
            float elapsed = Time.time - gameplayStartedAt;
            gameplayTrial.Expire(elapsed);

            if (pendingPressIndex >= 0 && pendingIsHold &&
                !gameplayTrial.IsResolved(pendingPressIndex) &&
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

        if (!isGameplayInput)
            yield break;

        gameplayTrial.Expire(totalSeconds +
                          TimedInputTrial.ReleaseGraceSeconds);
        int resolved = gameplayTrial.ResolvedCount;
        Debug.Log(
            $"[Gameplay] Input finished: " +
            $"{resolved}/{gameplayTrial.Count} resolved / " +
            $"{totalSeconds:F2}s + release grace");

        LastGameplayResults = completedTrialResults.AsReadOnly();
        StopPlayback();
        OnGameplayCompleted?.Invoke();
    }

    public void StopPlayback()
    {
        isPlaying = false;
        isAdvancing = false;
        isTimedPreview = false;
        isGameplayInput = false;
        pendingPressIndex = -1;
        pendingIsHold = false;
        pendingUnitDuration = 0f;
        currentGameplayUnits = null;
        if (judgmentUI != null)
            judgmentUI.SetTimedInputFeedback(false);
        if (gameplayTrial != null)
        {
            gameplayTrial.OnResolved -= HandleGameplayResolved;
            gameplayTrial = null;
        }
        StopAllCoroutines();

        if (judgmentSystem != null)
            judgmentSystem.enabled = false;
    }

    private void HandleJudgment(string result)
    {
        if (!isPlaying || isAdvancing || isTimedPreview || isGameplayInput)
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
            isGameplayInput ||
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
        if (isGameplayInput && gameplayTrial != null)
        {
            int sourceIndex = pendingPressIndex;
            pendingPressIndex = -1;
            pendingIsHold = false;
            if (sourceIndex >= 0)
            {
                bool accepted = gameplayTrial.TryRecordRelease(
                    sourceIndex, Time.time - gameplayStartedAt,
                    result, position, heldSeconds, holdToleranceSeconds);
                if (!accepted)
                    Debug.Log($"[Issue #53] Late/duplicate release ignored: {sourceIndex}");
            }

            if (isGameplayInput && currentNoteIndex < notePositions.Length)
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
