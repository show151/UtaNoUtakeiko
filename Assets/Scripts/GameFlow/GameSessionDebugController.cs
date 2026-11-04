using UnityEngine;
using UnityEngine.InputSystem;

namespace Uta.GameFlow
{
    /// <summary>
    /// Keyboard-only recovery and test shortcuts. Screen navigation is owned by
    /// ScreenArtworkPresenter so Enter cannot skip a selection screen.
    /// </summary>
    public class GameSessionDebugController : MonoBehaviour
    {
        [SerializeField]
        private GameSessionManager sessionManager;

        [SerializeField]
        private TestSessionAdapter testSessionAdapter;

        private void Update()
        {
            if (sessionManager == null || Keyboard.current == null)
                return;

            // Keep the existing convenient test shortcut for the countdown.
            if (Keyboard.current.enterKey.wasPressedThisFrame &&
                sessionManager.CurrentState == GameSessionState.Countdown)
            {
                sessionManager.TryTransition(GameSessionState.Playing);
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                sessionManager.TryTransition(GameSessionState.Error);
                sessionManager.TryTransition(GameSessionState.Title);
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
                sessionManager.TryTransition(sessionManager.CurrentState);

            if (Keyboard.current.tKey.wasPressedThisFrame)
                sessionManager.RecoverToTitle();

            if (Keyboard.current.yKey.wasPressedThisFrame && testSessionAdapter != null)
                testSessionAdapter.StartTestSession();
        }
    }
}
