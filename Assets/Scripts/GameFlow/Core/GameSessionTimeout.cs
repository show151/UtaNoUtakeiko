
using UnityEngine;

namespace Uta.GameFlow
{
    public class GameSessionTimeout : MonoBehaviour
    {
        [SerializeField]
        private GameSessionManager sessionManager;

        // ‰¼‚Ì§ŒÀŽžŠÔB³Ž®‚È“ïˆÕ“x•ÊÝ’è‚ÍŒã‚ÅÚ‘±‚·‚éB
        [SerializeField, Min(1f)]
        private float testTimeLimit = 30f;

        private float remainingTime;
        private bool isCounting;

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

            sessionManager.RecoverToTitle();
        }

        private void HandleStateChanged(
            GameSessionState previous,
            GameSessionState next)
        {
            if (next == GameSessionState.Playing)
            {
                remainingTime = testTimeLimit;
                isCounting = true;

                Debug.Log(
                    $"Session timer started: {testTimeLimit:F1}s"
                );
            }
            else
            {
                isCounting = false;
            }
        }


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
