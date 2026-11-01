
using UnityEngine;

namespace Uta.GameFlow
{
    public class GameSessionPlayConnector : MonoBehaviour
    {
        [SerializeField] private GameSessionManager sessionManager;
        [SerializeField] private TestSessionAdapter testSessionAdapter;
        [SerializeField] private NoteProgression noteProgression;

        private void OnEnable()
        {
            if (sessionManager != null)
                sessionManager.OnStateChanged += HandleStateChanged;

            if (noteProgression != null)
                noteProgression.OnCompleted += HandlePlayCompleted;
        }

        private void OnDisable()
        {
            if (sessionManager != null)
                sessionManager.OnStateChanged -= HandleStateChanged;

            if (noteProgression != null)
            {
                noteProgression.OnCompleted -= HandlePlayCompleted;
                noteProgression.StopPlayback();
            }
        }

        private void HandleStateChanged(
            GameSessionState previous,
            GameSessionState next)

        {
            // 一時テスト用：Playingへの遷移時に復旧要求を発生させる
            //if (next == GameSessionState.Playing)
            //{
            //    sessionManager.RecoverToTitle();
            //    return;
            //}

            if (previous == GameSessionState.Playing &&
                next != GameSessionState.Playing)
            {
                noteProgression.StopPlayback();
            }

            if (next != GameSessionState.Playing)
                return;

            if (testSessionAdapter == null ||
                noteProgression == null ||
                !testSessionAdapter.Validate())
            {
                Debug.LogError("Play session initialization failed.");
                sessionManager.RecoverToTitle();
                return;
            }

            noteProgression.StartPlay(
                testSessionAdapter.GetTestNotePositions()
            );
        }

        private void HandlePlayCompleted()
        {
            if (sessionManager.CurrentState != GameSessionState.Playing)
                return;

            sessionManager.TryTransition(GameSessionState.Playback);
        }
    }
}
