
using UnityEngine;
using TMPro;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private TMP_Text connectionMessage;

    // 未接続メッセージを表示する
    public void ShowDisconnected()
    {
        connectionPanel.SetActive(true);

        connectionMessage.text =
              "Device is not connected.\nPlease connect the device.";
    }

    // 未接続メッセージを非表示にする
    public void HideDisconnected()
    {
        connectionPanel.SetActive(false);
    }
}
