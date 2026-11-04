
using UnityEngine;
using TMPro;

public class JudgmentUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private JudgmentSystem judgmentSystem;
    [SerializeField] private TMP_Text judgmentText;

    [SerializeField] private MovingBar movingBar;
    [SerializeField] private SliderTicks sliderTicks;

    private bool timedInputFeedback;

    private void OnEnable()
    {
        if (judgmentSystem != null)
        {
            judgmentSystem.OnJudged += ShowJudgment;
        }
    }

    private void OnDisable()
    {
        if (judgmentSystem != null)
        {
            judgmentSystem.OnJudged -= ShowJudgment;
        }
    }

    private void Start()
    {
        judgmentText.text = "";
    }

    private void ShowJudgment(string result)
    {
        if (timedInputFeedback)
            return;
        judgmentText.text = result;

        switch (result)
        {
            case "Perfect":
                judgmentText.color = Color.yellow;
                break;

            case "Great":
                judgmentText.color = Color.green;
                break;

            case "Good":
                judgmentText.color = Color.cyan;
                break;

            case "Bad":
                judgmentText.color = new Color(1f, 0.5f, 0f);
                break;

            default:
                judgmentText.color = Color.gray;
                break;
        }

        sliderTicks.HighlightNearestTick(
            movingBar.NormalizedPosition
        );
    }


    // In the timed route, each input can be TAP or HOLD without changing
    // the existing legacy JudgmentSystem event contract.
    public void SetTimedInputFeedback(bool enabled)
    {
        timedInputFeedback = enabled;
        ClearJudgment();
    }

    public void ShowTimedInputPrompt(bool isHold)
    {
        if (!timedInputFeedback || judgmentText == null)
            return;
        judgmentText.text = isHold ? "HOLD" : "TAP";
        judgmentText.color = isHold ? new Color(1f, 0.7f, 0.24f) : Color.white;
    }

    public void ShowTimedHoldProgress(float fraction)
    {
        if (!timedInputFeedback || judgmentText == null)
            return;
        judgmentText.text = $"HOLD {Mathf.RoundToInt(Mathf.Clamp01(fraction) * 100f)}%";
        judgmentText.color = new Color(1f, 0.7f, 0.24f);
    }

    public void ShowTimedInputResult(string pitch, bool? holdSucceeded, bool timedOut)
    {
        if (!timedInputFeedback || judgmentText == null)
            return;
        judgmentText.text = timedOut ? "Miss" :
            holdSucceeded.HasValue
                ? $"{pitch} / HOLD {(holdSucceeded.Value ? "OK" : "MISS")}" : pitch;
        judgmentText.color = timedOut || holdSucceeded == false
            ? new Color(1f, 0.45f, 0.38f)
            : Color.green;
    }

    public void ClearJudgment()
    {
        judgmentText.text = "";
        sliderTicks.ClearHighlight();
    }
}
