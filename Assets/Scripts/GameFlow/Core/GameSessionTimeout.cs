
using UnityEngine;

namespace Uta.GameFlow
{
    public class GameSessionTimeout : MonoBehaviour
    {
        [SerializeField]
        private GameSessionManager sessionManager;

        // 仮の制限時間。正式な難易度別設定は後で接続する。
        [SerializeField, Min(1f)]
        private float testTimeLimit = 30f;

        private float remainingTime;
        private bool isCounting;
        private bool hardDifficulty;

        public float RemainingTime => remainingTime;

        private void OnEnable()
        {
            if (sessionManager != null)
                sessionManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (sessionManager != null)
                sessionManager.OnStateChanged -= HandleStateChanged;

            isCounting = false;
        }

        private void Update()
        {
            if (!isCounting || sessionManager == null)
                return;

            if (sessionManager.CurrentState != GameSessionState.Playing)
            {
                isCounting = false;
                return;
            }

            remainingTime -= Time.deltaTime;

            if (remainingTime > 0f)
                return;

            remainingTime = 0f;
            isCounting = false;

            Debug.LogWarning("Session timeout!");

            // 採点側に時間切れを通知する
            sessionManager.NotifySessionTimedOut();
        }

        private void HandleStateChanged(
            GameSessionState previous,
            GameSessionState next)
        {
            if (next == GameSessionState.Playing)
            {
                remainingTime = hardDifficulty ? 100f : testTimeLimit;
                isCounting = true;

                Debug.Log(
                    $"Session timer started: {remainingTime:F1}s"
                );
            }
            else
            {
                isCounting = false;
            }
        }


        public void SetHardDifficulty(bool hard) => hardDifficulty = hard;

        public void SetTestTimeLimit(float seconds)
        {
            testTimeLimit = Mathf.Max(0.01f, seconds);
        }

        public void SetSessionManager(GameSessionManager manager)
        {
            if (sessionManager != null)
            {
                sessionManager.OnStateChanged -= HandleStateChanged;
            }

            sessionManager = manager;

            if (isActiveAndEnabled && sessionManager != null)
            {
                sessionManager.OnStateChanged += HandleStateChanged;
            }
        }
    }
}
