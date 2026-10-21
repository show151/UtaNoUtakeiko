
using UnityEngine;
using TMPro;

public class JudgmentUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private JudgmentSystem judgmentSystem;
    [SerializeField] private TMP_Text judgmentText;

    [SerializeField] private MovingBar movingBar;
    [SerializeField] private SliderTicks sliderTicks;

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


    public void ClearJudgment()
    {
        judgmentText.text = "";
        sliderTicks.ClearHighlight();
    }
}
