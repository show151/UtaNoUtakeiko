
using System;
using UnityEngine;

public class JudgmentSystem : MonoBehaviour
{
    public event Action<string> OnJudged;

    // Used only by the Issue #53 timed input trial to pin the press to its unit.
    public event Action<float> OnPressPositionCaptured;
    public event Action OnPressCaptureCanceled;
    public event Action OnRedPress;
    public bool HasCapturedPress => hasCapturedPress;
    public float CapturedPosition => capturedPosition;
    public string CapturedPitchJudgment => GetPitchJudgment(capturedPosition);
    public bool IsInputHeld => inputManager != null && inputManager.IsPhysicallyPressed;

    public event Action<string, float> OnJudgedWithPosition;

    // 判定名、入力音程位置、実際の押下秒数
    public event Action<string, float, float>
        OnJudgedWithPositionAndDuration;

    [SerializeField] private MovingBar movingBar;
    [SerializeField] private InputManager inputManager;

    [Range(0f, 1f)]
    [SerializeField] private float targetPosition = 0.5f;

    private bool hasJudged;
    private bool hasCapturedPress;
    private float capturedPosition;

    private void OnEnable()
    {
        if (inputManager == null)
            return;

        inputManager.OnPressStarted += HandlePressStarted;
        inputManager.OnPressReleased += HandlePressReleased;
        inputManager.OnPressCanceled += HandlePressCanceled;
        inputManager.OnRedPress += HandleRedPress;
    }

    private void OnDisable()
    {
        if (inputManager != null)
        {
            inputManager.OnPressStarted -= HandlePressStarted;
            inputManager.OnPressReleased -= HandlePressReleased;
            inputManager.OnPressCanceled -= HandlePressCanceled;
            inputManager.OnRedPress -= HandleRedPress;
        }

        hasCapturedPress = false;
    }

    private void HandleRedPress() => OnRedPress?.Invoke();

    public void StartStairMovement() => movingBar?.StartStairMovement();
    public void StopStairMovement() => movingBar?.StopStairMovement();
    public float CurrentPosition => movingBar != null ? movingBar.NormalizedPosition : 0f;
    public string JudgePosition(float position, float target)
    {
        float previousTarget = targetPosition;
        targetPosition = Mathf.Clamp01(target);
        string result = GetPitchJudgment(position);
        targetPosition = previousTarget;
        return result;
    }

    private void HandlePressStarted()
    {
        if (hasJudged || hasCapturedPress || movingBar == null)
            return;

        hasCapturedPress = true;

        // 押した瞬間の音程位置を記録
        capturedPosition = movingBar.NormalizedPosition;

        movingBar.StopMovement();
        OnPressPositionCaptured?.Invoke(capturedPosition);
    }

    private void HandlePressCanceled()
    {
        if (!hasCapturedPress || hasJudged)
            return;

        hasCapturedPress = false;
        if (movingBar != null)
            movingBar.ResumeMovement();
        OnPressCaptureCanceled?.Invoke();
    }

    private void HandlePressReleased(float heldSeconds)
    {
        // Reject premature/incorrect release notifications while the key is down.
        if (IsInputHeld)
        {
            Debug.LogWarning("[Gameplay] Ignored release while input is physically held.");
            return;
        }

        if (hasJudged || !hasCapturedPress)
            return;

        if (float.IsNaN(heldSeconds) ||
            float.IsInfinity(heldSeconds) ||
            heldSeconds < 0f)
        {
            hasCapturedPress = false;
            return;
        }

        hasCapturedPress = false;
        hasJudged = true;

        float position = capturedPosition;

        string result = GetPitchJudgment(position);

        Debug.Log(
            $"Position={position:F3}, " +
            $"Target={targetPosition:F3}, " +
            $"Judgment={result}, " +
            $"Held={heldSeconds:F2}s"
        );

        // 次のステップで長押し採点に接続する
        OnJudgedWithPositionAndDuration?.Invoke(
            result,
            position,
            heldSeconds
        );

        // 既存のNoteId連携を維持
        OnJudgedWithPosition?.Invoke(result, position);

        // 既存の音符進行・UI連携を維持
        OnJudged?.Invoke(result);
    }

    private string GetPitchJudgment(float position)
    {
        float difference = Mathf.Abs(position - targetPosition);
        if (difference <= 0.02f) return "Perfect";
        if (difference <= 0.05f) return "Great";
        if (difference <= 0.10f) return "Good";
        if (difference <= 0.20f) return "Bad";
        return "Miss";
    }

    public void ResetJudgment(float nextTargetPosition)
    {
        targetPosition = Mathf.Clamp01(nextTargetPosition);

        hasJudged = false;
        hasCapturedPress = false;

        if (movingBar != null)
            movingBar.ResumeMovement();
    }
}
