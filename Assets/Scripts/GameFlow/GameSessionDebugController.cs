
using UnityEngine;
using UnityEngine.InputSystem;

namespace Uta.GameFlow
{
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

            // Enter: 次の状態へ進む
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                Advance();
            }

            // Escape: エラー経由でタイトルへ戻る
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                sessionManager.TryTransition(GameSessionState.Error);
                sessionManager.TryTransition(GameSessionState.Title);
            }

            // R: 現在の状態への重複遷移を試す
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                sessionManager.TryTransition(sessionManager.CurrentState);
            }

            // T: RecoverToTitle() を直接テスト
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                sessionManager.RecoverToTitle();
            }

            // Y: テスト用セッションデータを検証
            if (Keyboard.current.yKey.wasPressedThisFrame)
            {
                if (testSessionAdapter != null)
                {
                    testSessionAdapter.StartTestSession();
                }
            }
        }

        private void Advance()
        {
            GameSessionState nextState;

            switch (sessionManager.CurrentState)
            {
                case GameSessionState.Title:
                    nextState = GameSessionState.DifficultySelect;
                    break;

                case GameSessionState.DifficultySelect:
                    nextState = GameSessionState.SongSelect;
                    break;

                case GameSessionState.SongSelect:
                    nextState = GameSessionState.Countdown;
                    break;

                case GameSessionState.Countdown:
                    nextState = GameSessionState.Playing;
                    break;

                case GameSessionState.Result:
                    nextState = GameSessionState.Ranking;
                    break;

                case GameSessionState.Ranking:
                    nextState = GameSessionState.Announcement;
                    break;

                case GameSessionState.Announcement:
                    nextState = GameSessionState.Title;
                    break;

                // Playing・PlaybackではEnterで進めない
                default:
                    Debug.Log(

                        $"Manual advance disabled: {sessionManager.CurrentState}"
                    );
                    return;
            }

            sessionManager.TryTransition(nextState);
        }
    }
}
