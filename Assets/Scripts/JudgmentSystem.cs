
using System;
using UnityEngine;

public class JudgmentSystem : MonoBehaviour
{
    public event Action<string> OnJudged;

    // Used only by the Issue #53 timed input trial to pin the press to its unit.
    public event Action<float> OnPressPositionCaptured;
    public bool HasCapturedPress => hasCapturedPress;

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
    }

    private void OnDisable()
    {
        if (inputManager != null)
        {
            inputManager.OnPressStarted -= HandlePressStarted;
            inputManager.OnPressReleased -= HandlePressReleased;
        }

        hasCapturedPress = false;
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

    private void HandlePressReleased(float heldSeconds)
    {
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

        float difference = Mathf.Abs(
            position - targetPosition
        );

        string result;

        if (difference <= 0.02f)
            result = "Perfect";
        else if (difference <= 0.05f)
            result = "Great";
        else if (difference <= 0.10f)
            result = "Good";
        else if (difference <= 0.20f)
            result = "Bad";
        else
            result = "Miss";

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

    public void ResetJudgment(float nextTargetPosition)
    {
        targetPosition = Mathf.Clamp01(nextTargetPosition);

        hasJudged = false;
        hasCapturedPress = false;

        if (movingBar != null)
            movingBar.ResumeMovement();
    }
}
