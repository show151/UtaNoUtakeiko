
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Uta.GameFlow;

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
        ? pressedKeyboard != null && pressedKeyboard.added && pressedKeyboard.enterKey.isPressed
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

    // Reject unrealistically short key pulses (observed ~0.007-0.011s).
    [SerializeField, Min(0f)] private float minimumPressSeconds = 0.03f;

    [SerializeField] private GameSessionManager sessionManager;

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
    private Keyboard pressedKeyboard;
    private double pressStartedRealtime;
    private double lastKeyEventRealtime;
    [SerializeField] private bool logKeyboardDiagnostics = true;

    // ボタンを離したとき、実際の押下時間を通知する
    public event Action<float> OnPressReleased;

    // Report rejected key pulses so gameplay can unfreeze its moving bar.
    public event Action OnPressCanceled;

    // ボタンを押し始めた瞬間
    public event Action OnPressStarted;

    private void Awake()
    {
        lastInputAvailable = IsInputAvailable;
    }

    private static string KeyboardLabel(Keyboard keyboard)
    {
        if (keyboard == null)
            return "null";
        return $"{keyboard.deviceId}:{keyboard.displayName}({keyboard.layout})";
    }

    private void LogKeyEvent(string phase)
    {
        if (!logKeyboardDiagnostics)
            return;

        double realtime = Time.realtimeSinceStartupAsDouble;
        double sinceDown = phase == "DOWN" ? 0.0 : realtime - pressStartedRealtime;
        double sincePrevious = lastKeyEventRealtime > 0 ? realtime - lastKeyEventRealtime : 0.0;
        lastKeyEventRealtime = realtime;
        Debug.Log(
            $"[InputDiag] {phase} id={activePressId} " +
            $"keyboard={KeyboardLabel(pressedKeyboard)} " +
            $"current={KeyboardLabel(Keyboard.current)} " +
            $"focus={Application.isFocused} " +
            $"heldRealtime={sinceDown:F4}s deltaEvent={sincePrevious:F4}s " +
            $"unscaled={Time.unscaledTime:F4}s frame={Time.frameCount}");
    }

    private void CancelActivePress(string reason)
    {
        if (!isPressed)
            return;
        Debug.LogWarning(
            $"[InputDiag] CANCEL id={activePressId} reason={reason} " +
            $"keyboard={KeyboardLabel(pressedKeyboard)}");
        isPressed = false;
        isLongPress = false;
        activePressId = 0;
        pressedKeyboard = null;
        OnPressCanceled?.Invoke();
    }

    private bool CanAcceptInput()
    {
        if (sessionManager == null)
            return false;

        return sessionManager.CurrentState != GameSessionState.Playing;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            CancelActivePress("application lost focus");
    }

    private void OnDisable()
    {
        CancelActivePress("input manager disabled");
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

        // 以下、既存の処理

        if (!CanAcceptInput())
        {
            CancelActivePress("input disabled in current state");
            return;
        }

        if (isTestMode)
        {
            // Pin the original keyboard while a key cycle is active.
            // Keyboard.current may change when multiple keyboards (including
            // virtual devices) exist. Never read the release from another one.
            Keyboard keyboard = isPressed ? pressedKeyboard : Keyboard.current;
            if (keyboard != null && keyboard.added)
            {
                pressedThisFrame = keyboard.enterKey.wasPressedThisFrame;
                releasedThisFrame = keyboard.enterKey.wasReleasedThisFrame;
                isHolding = keyboard.enterKey.isPressed;

                if (isPressed && Keyboard.current != keyboard && logKeyboardDiagnostics)
                    Debug.LogWarning(
                        $"[InputDiag] CURRENT_CHANGED pressed={KeyboardLabel(keyboard)} " +
                        $"current={KeyboardLabel(Keyboard.current)} realtime={Time.realtimeSinceStartupAsDouble:F3}");
            }
            else if (isPressed)
            {
                // Device removal is an input cancellation, never a scored release.
                CancelActivePress("keyboard device unavailable");
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
            if (Time.unscaledTime - lastInputTime >= debounceTime)
            {
                isPressed = true;
                isLongPress = false;
                activePressId = ++pressSequenceId;
                pressedKeyboard = isTestMode ? Keyboard.current : null;
                pressStartTime = Time.unscaledTime;
                pressStartedRealtime = Time.realtimeSinceStartupAsDouble;

                lastInputTime = Time.unscaledTime;

                // Only a fresh physical down edge may start an input.
                Debug.Log($"[InputTrace] DOWN id={activePressId} frame={Time.frameCount}");
                LogKeyEvent("DOWN");
                OnPressStarted?.Invoke();
            }
        }

        // ボタンを押し続けている場合
        if (isPressed && !isLongPress && isHolding)
        {
            if (Time.unscaledTime - pressStartTime >= longPressThreshold)
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
                    Time.unscaledTime - pressStartTime
                );
                LogKeyEvent("UP");

                bool wasLongPress = isLongPress;
                int completedPressId = activePressId;

                // Clear the physical cycle BEFORE notifying listeners, so
                // no reentrant handler can consume the same release twice.
                isPressed = false;
                isLongPress = false;
                activePressId = 0;
                pressedKeyboard = null;

                Debug.Log(
                    $"[InputTrace] UP id={completedPressId} " +
                    $"held={heldSeconds:F3}s long={wasLongPress} frame={Time.frameCount}");

                if (heldSeconds < minimumPressSeconds)
                {
                    Debug.LogWarning(
                        $"[InputTrace] REJECT id={completedPressId} " +
                        $"pulse={heldSeconds:F3}s minimum={minimumPressSeconds:F3}s");
                    OnPressCanceled?.Invoke();
                }
                else
                {
                    if (!wasLongPress)
                        OnShortPress?.Invoke();

                    OnPressReleased?.Invoke(heldSeconds);
                }
            }

            isPressed = false;
            isLongPress = false;
        }
    }
}
