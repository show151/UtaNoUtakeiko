
using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class JudgmentSystem : MonoBehaviour
{

    public event Action<string> OnJudged;

    [SerializeField] private MovingBar movingBar;
    [SerializeField] private InputManager inputManager;

    [Range(0f, 1f)]
    [SerializeField] private float targetPosition = 0.5f;

    private bool hasJudged = false;

    private void OnEnable()
    {
        if (inputManager != null)
            inputManager.OnShortPress += Judge;
    }

    private void OnDisable()
    {
        if (inputManager != null)
            inputManager.OnShortPress -= Judge;
    }

    private void Judge()
    {
        if (hasJudged) return;

        movingBar.StopMovement();

        float position = movingBar.NormalizedPosition;
        float difference = Mathf.Abs(position - targetPosition);

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

        hasJudged = true;

        Debug.Log(
            $"Position: {position:F3}, " +
            $"Target: {targetPosition:F3}, " +
            $"Difference: {difference:F3}, " +
            $"Judgment: {result}"
        );

        OnJudged?.Invoke(result);

    }

    public void ResetJudgment(float nextTargetPosition)
    {
        targetPosition = Mathf.Clamp01(nextTargetPosition);
        hasJudged = false;
        movingBar.ResumeMovement();
    }
}
