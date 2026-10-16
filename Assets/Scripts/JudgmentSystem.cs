
using UnityEngine;
using UnityEngine.InputSystem;

public class JudgmentSystem : MonoBehaviour
{
    [SerializeField] private MovingBar movingBar;

    [Range(0f, 1f)]
    [SerializeField] private float targetPosition = 0.5f;

    private bool hasJudged = false;

    private void Update()
    {
        if (hasJudged) return;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Judge();
        }
    }

    private void Judge()
    {
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
    }

    public void ResetJudgment(float nextTargetPosition)
    {
        targetPosition = Mathf.Clamp01(nextTargetPosition);
        hasJudged = false;
        movingBar.ResumeMovement();
    }
}
