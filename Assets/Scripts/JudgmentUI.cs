
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
        judgmentText.text = "";
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

    // Pitch is captured at the press; HOLD remains pending until completion.
    public void ShowTimedCapturedPitch(string pitch, bool isHold)
    {
        if (!timedInputFeedback || judgmentText == null)
            return;
        judgmentText.text = isHold
            ? $"PITCH: {pitch}\nHOLD: ..."
            : $"PITCH: {pitch}";
        judgmentText.color = Color.white;
    }

    public void ShowTimedInputResult(string pitch, bool? holdSucceeded, bool timedOut)
    {
        if (!timedInputFeedback || judgmentText == null)
            return;
        string pitchResult = timedOut ? "MISS" : pitch;
        judgmentText.text = holdSucceeded.HasValue
            ? $"PITCH: {pitchResult}\nHOLD: {(holdSucceeded.Value ? "PERFECT" : "MISS")}"
            : $"PITCH: {pitchResult}";
        // Keep the shared text neutral: pitch and HOLD can have different results.
        judgmentText.color = Color.white;
    }

    public void ClearJudgment()
    {
        judgmentText.text = "";
        sliderTicks.ClearHighlight();
    }
}
