
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

    // Physical key/pedal state; a scoring event must never advance while held.
    public bool IsPhysicallyPressed => isTestMode
        ? Keyboard.current != null && Keyboard.current.spaceKey.isPressed
        : isPressed;

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

    // One physical down/up cycle is exactly one action.
    // Press IDs make duplicate or repeated input events visible in Console.
    private int pressSequenceId;
    private int activePressId;

    // ボタンを離したとき、実際の押下時間を通知する
    public event Action<float> OnPressReleased;

    // ボタンを押し始めた瞬間
    public event Action OnPressStarted;

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
        if (pressedThisFrame && isHolding && !isPressed)
        {
            if (Time.time - lastInputTime >= debounceTime)
            {
                isPressed = true;
                isLongPress = false;
                activePressId = ++pressSequenceId;
                pressStartTime = Time.time;

                lastInputTime = Time.time;

                // Only a fresh physical down edge may start an input.
                Debug.Log($"[InputTrace] DOWN id={activePressId} frame={Time.frameCount}");
                OnPressStarted?.Invoke();
            }
        }

        // ボタンを押し続けている場合
        if (isPressed && !isLongPress && isHolding)
        {
            if (Time.time - pressStartTime >= longPressThreshold)
            {
                isLongPress = true;
                Debug.Log($"[InputTrace] HOLD id={activePressId} frame={Time.frameCount}");
                OnLongPress?.Invoke();
            }
        }

        // ボタンを離した瞬間
        if (releasedThisFrame && !isHolding)
        {
            if (isPressed)
            {
                float heldSeconds = Mathf.Max(
                    0f,
                    Time.time - pressStartTime
                );

                bool wasLongPress = isLongPress;
                int completedPressId = activePressId;

                // Clear the physical cycle BEFORE notifying listeners, so
                // no reentrant handler can consume the same release twice.
                isPressed = false;
                isLongPress = false;
                activePressId = 0;

                Debug.Log(
                    $"[InputTrace] UP id={completedPressId} " +
                    $"held={heldSeconds:F3}s long={wasLongPress} frame={Time.frameCount}");

                if (!wasLongPress)
                    OnShortPress?.Invoke();

                OnPressReleased?.Invoke(heldSeconds);
            }

            isPressed = false;
            isLongPress = false;
        }
    }
}
