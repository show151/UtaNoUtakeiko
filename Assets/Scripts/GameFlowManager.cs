using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    [SerializeField] private InputManager inputManager;
    [SerializeField] private ConnectionUI connectionUI;

    private bool isPaused = false;

    public bool IsPaused => isPaused;

    private void OnEnable()
    {
        if (inputManager != null)
        {
            inputManager.OnInputAvailabilityChanged
                += HandleInputAvailabilityChanged;
        }
    }

    private void OnDisable()
    {
        if (inputManager != null)
        {
            inputManager.OnInputAvailabilityChanged
                -= HandleInputAvailabilityChanged;
        }
    }

    private void Start()
    {

        Debug.Log("GameFlowManager Start!");

        if (inputManager == null || connectionUI == null)
        {
            Debug.LogError("GameFlowManagerの参照が設定されていません。");
            return;
        }

        // 起動時の接続状態を確認
        HandleInputAvailabilityChanged(inputManager.IsInputAvailable);
    }

    // 入力可能状態が変化したときに呼ばれる
    private void HandleInputAvailabilityChanged(bool isAvailable)
    {
        Debug.Log($"入力可能状態が変更された：{isAvailable}");

        if (isAvailable)
        {
            ResumeGame();
            connectionUI.HideDisconnected();
        }
        else
        {
            PauseGame();
            connectionUI.ShowDisconnected();
        }
    }

    public void PauseGame()
    {
        if (isPaused) return;

        isPaused = true;
        Time.timeScale = 0f;

        Debug.Log("Game Paused");
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;

        Debug.Log("Game Resumed");
    }
}