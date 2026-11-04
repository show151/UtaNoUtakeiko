
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
    [SerializeField, Min(0f)] private float holdSuccessDisplaySeconds = 0.6f;
    [SerializeField, Min(0f)] private float holdScrollDuration = 0.8f;

    private int currentNoteIndex;
    private bool isPlaying;
    private bool isAdvancing;
    private bool isTimedPreview;
    private bool isGameplayInput;
    private TimedInputTrial gameplayTrial;
    private readonly List<TimedInputTrialResult> completedTrialResults = new List<TimedInputTrialResult>();
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
            judgmentSystem.OnPressCaptureCanceled += HandleGameplayPressCanceled;
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
            judgmentSystem.OnPressCaptureCanceled -= HandleGameplayPressCanceled;
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
    // Input units advance only after a real button release.
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

        Debug.Log($"[Gameplay] Player-controlled input started: {units.Count} units");
        return true;
    }

    private void HandleGameplayPressStarted(float capturedPosition)
    {
        if (!isGameplayInput || gameplayTrial == null)
            return;

        if (isAdvancing || pendingPressIndex >= 0)
            return;

        int active = currentNoteIndex;
        pendingPressIndex = active >= 0 &&
                            active < gameplayTrial.Count &&
                            !gameplayTrial.IsResolved(active)
            ? active : -1;
        timedPressStartedAt = Time.unscaledTime;
        pendingIsHold = pendingPressIndex >= 0 &&
            currentGameplayUnits != null && currentGameplayUnits[pendingPressIndex].IsHold;
        pendingUnitDuration = pendingPressIndex >= 0 && currentGameplayUnits != null && pendingIsHold
            ? currentGameplayUnits[pendingPressIndex].RequiredHoldSeconds : 0f;
        if (pendingPressIndex >= 0)
            judgmentUI.ShowTimedCapturedPitch(
                judgmentSystem.CapturedPitchJudgment, pendingIsHold);
        Debug.Log($"[InputTrace] CAPTURE unit={pendingPressIndex + 1}/" +
                  $"{gameplayTrial.Count} frame={Time.frameCount}");
    }

    private void HandleGameplayPressCanceled()
    {
        if (!isGameplayInput || isAdvancing)
            return;

        Debug.Log($"[InputTrace] CANCEL unit={currentNoteIndex + 1}/{notePositions.Length}");
        pendingPressIndex = -1;
        pendingIsHold = false;
        pendingUnitDuration = 0f;
        judgmentUI.ShowTimedInputPrompt(currentGameplayUnits[currentNoteIndex].IsHold);
        noteDisplay.ResetInputProgress(currentNoteIndex);
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
                // The HOLD bar reflects HOLD alone; pitch remains a separate judgment.
                bool succeeded = !result.TimedOut &&
                    (currentGameplayUnits[i].IsHold
                        ? result.HoldSucceeded == true
                        : result.PitchJudgment != "Miss");
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

    private void Update()
    {
        if (!isGameplayInput || gameplayTrial == null ||
            pendingPressIndex < 0 || !pendingIsHold ||
            pendingUnitDuration <= 0f ||
            gameplayTrial.IsResolved(pendingPressIndex))
            return;

        float heldSeconds = Time.unscaledTime - timedPressStartedAt;
        float fraction = Mathf.Clamp01(heldSeconds / pendingUnitDuration);
        noteDisplay.ShowHoldProgress(pendingPressIndex, fraction);

        if (fraction < 1f)
            return;

        int completedIndex = pendingPressIndex;
        isAdvancing = true;
        bool accepted = gameplayTrial.TryRecordHoldSuccess(
            completedIndex, judgmentSystem.CapturedPitchJudgment,
            judgmentSystem.CapturedPosition, heldSeconds);
        if (!accepted)
        {
            isAdvancing = false;
            return;
        }

        pendingPressIndex = -1;
        pendingIsHold = false;
        pendingUnitDuration = 0f;
        StartCoroutine(AdvanceGameplay(completedIndex + 1, scrollBeforeRelease: true));
    }

    // Successful HOLDs pause briefly to show the result, then scroll even
    // while held. Input remains locked until scrolling and release finish.
    private IEnumerator AdvanceGameplay(int nextIndex, bool scrollBeforeRelease)
    {
        if (scrollBeforeRelease && holdSuccessDisplaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(holdSuccessDisplaySeconds);
            if (!isGameplayInput)
                yield break;
        }

        if (!scrollBeforeRelease)
        {
            while (isGameplayInput && judgmentSystem.IsInputHeld)
                yield return null;

            if (!isGameplayInput)
                yield break;

            // Keep early-release HOLD results readable.
            if (currentGameplayUnits != null && nextIndex > 0 &&
                currentGameplayUnits[nextIndex - 1].IsHold)
            {
                yield return new WaitForSecondsRealtime(0.75f);
                if (!isGameplayInput)
                    yield break;
            }
        }

        if (nextIndex < notePositions.Length)
        {
            bool previousWasHold = currentGameplayUnits != null && nextIndex > 0 &&
                currentGameplayUnits[nextIndex - 1].IsHold;
            Debug.Log($"[Gameplay] Slide to input {nextIndex + 1}/{notePositions.Length}");
            yield return previousWasHold
                ? noteDisplay.ScrollToNote(nextIndex, Mathf.Max(0f, holdScrollDuration))
                : noteDisplay.ScrollToNote(nextIndex);
        }

        if (!isGameplayInput || currentGameplayUnits == null)
            yield break;

        // Also wait when the previous press was released and another press
        // began during the slide; no held press can become the next input.
        while (isGameplayInput && judgmentSystem.IsInputHeld)
            yield return null;

        if (!isGameplayInput || currentGameplayUnits == null)
            yield break;

        if (nextIndex >= notePositions.Length)
        {
            CompleteGameplayInput();
            yield break;
        }

        currentNoteIndex = nextIndex;
        judgmentUI.ClearJudgment();
        judgmentSystem.ResetJudgment(notePositions[nextIndex]);
        judgmentUI.ShowTimedInputPrompt(currentGameplayUnits[nextIndex].IsHold);
        isAdvancing = false;
    }

    private void CompleteGameplayInput()
    {
        if (!isGameplayInput || gameplayTrial == null ||
            gameplayTrial.ResolvedCount != gameplayTrial.Count)
            return;

        Debug.Log(
            $"[Gameplay] Player-controlled input finished: " +
            $"{gameplayTrial.ResolvedCount}/{gameplayTrial.Count}");

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
            // Never resolve more than one unit from the same physical release.
            // Lock advancement before notifying results or moving any sprites.
            if (isAdvancing)
                return;

            // A HOLD remains active until the actual input device is released.
            // This also guards against unexpected duplicate judgment callbacks.
            if (judgmentSystem.IsInputHeld)
            {
                Debug.LogWarning("[Gameplay] Release ignored: Space/pedal still held.");
                return;
            }

            int sourceIndex = pendingPressIndex;
            bool wasHold = pendingIsHold;
            pendingPressIndex = -1;
            pendingIsHold = false;
            pendingUnitDuration = 0f;
            if (sourceIndex < 0 || sourceIndex != currentNoteIndex)
                return;

            isAdvancing = true;
            Debug.Log($"[InputTrace] RESOLVE unit={sourceIndex + 1}/" +
                      $"{gameplayTrial.Count} held={heldSeconds:F3}s frame={Time.frameCount}");
            bool accepted = gameplayTrial.TryRecordPlayerRelease(
                sourceIndex, result, position, heldSeconds,
                wasHold ? 0f : holdToleranceSeconds);

            if (!accepted)
            {
                Debug.LogWarning(
                    $"[Gameplay] Invalid/duplicate release: {sourceIndex}");
                isAdvancing = false;
                judgmentSystem.ResetJudgment(notePositions[currentNoteIndex]);
                return;
            }

            int next = sourceIndex + 1;
            if (next >= notePositions.Length && !wasHold)
            {
                CompleteGameplayInput();
                return;
            }

            StartCoroutine(AdvanceGameplay(next, scrollBeforeRelease: false));
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
