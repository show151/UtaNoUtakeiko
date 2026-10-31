
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    // 短押しされたとき
    public event Action OnShortPress;

    // 長押しされたとき
    public event Action OnLongPress;

    // 入力可能状態が変化したときに通知する
    public event Action<bool> OnInputAvailabilityChanged;

    // 入力可能かどうか
    public bool IsInputAvailable => isTestMode || isExternalDeviceConnected;

    // 前回の入力可能状態
    private bool lastInputAvailable;

    // テストモードかどうか
    // ON：Spaceキーで操作
    // OFF：外部デバイスで操作
    [SerializeField] private bool isTestMode = true;

    // 外部デバイスが接続されているか
    // 実機決定後に自動検出へ変更する
    [SerializeField] private bool isExternalDeviceConnected = false;

    // 長押しと判定する時間（秒）
    [SerializeField] private float longPressThreshold = 0.5f;

    // チャタリング対策の入力間隔（秒）
    [SerializeField] private float debounceTime = 0.05f;

    // 最後に入力を認識した時刻
    private float lastInputTime = -1f;

    // ボタンが押されているか
    private bool isPressed = false;

    // ボタンを押した時刻
    private float pressStartTime;

    // 長押し判定済みか
    private bool isLongPress = false;

    private void Awake()
    {
        lastInputAvailable = IsInputAvailable;
    }

    private void Update()
    {
        // 入力可能状態が変化したか確認
        if (IsInputAvailable != lastInputAvailable)
        {
            lastInputAvailable = IsInputAvailable;
            OnInputAvailabilityChanged?.Invoke(IsInputAvailable);
        }

        bool pressedThisFrame = false;
        bool releasedThisFrame = false;
        bool isHolding = false;

        // =================================
        // 入力取得部分
        // =================================

        if (isTestMode)
        {
            // テストモードならSpaceキーを使用
            if (Keyboard.current != null)
            {
                pressedThisFrame = Keyboard.current.spaceKey.wasPressedThisFrame;
                releasedThisFrame = Keyboard.current.spaceKey.wasReleasedThisFrame;
                isHolding = Keyboard.current.spaceKey.isPressed;
            }
            else
            {
                isPressed = false;
                isLongPress = false;
            }
        }
        else if (isExternalDeviceConnected)
        {
            // TODO: 外部デバイスの入力取得
        }
        else
        {
            // 本番モードでデバイス未接続なら入力を受け付けない
            isPressed = false;
            isLongPress = false;
        }

        // =================================
        // 共通処理
        // =================================

        // ボタンが押された瞬間
        if (pressedThisFrame)
        {
            if (Time.time - lastInputTime >= debounceTime)
            {
                isPressed = true;
                isLongPress = false;
                pressStartTime = Time.time;

                lastInputTime = Time.time;
            }
        }

        // ボタンを押し続けている場合
        if (isPressed && !isLongPress && isHolding)
        {
            if (Time.time - pressStartTime >= longPressThreshold)
            {
                isLongPress = true;
                Debug.Log("長押しを認識！");
                OnLongPress?.Invoke();
            }
        }

        // ボタンを離した瞬間
        if (releasedThisFrame)
        {
            // 長押しでなければ短押しとして通知
            if (isPressed && !isLongPress)
            {
                Debug.Log("短押しを認識！");
                OnShortPress?.Invoke();
            }

            isPressed = false;
            isLongPress = false;
        }
    }
}
