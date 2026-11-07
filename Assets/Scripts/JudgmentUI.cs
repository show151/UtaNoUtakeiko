
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

    // Gameplay status is communicated by note shapes and per-note judgment.
    public void ShowTimedInputPrompt(bool isHold) { }
    public void ShowStairPrompt(bool started) { }
    public void ShowTimedCapturedPitch(string pitch, bool isHold) { }
    public void ShowTimedInputResult(string pitch, bool? holdSucceeded, bool timedOut) { }

    public void ClearJudgment()
    {
        judgmentText.text = "";
        sliderTicks.ClearHighlight();
    }
}
