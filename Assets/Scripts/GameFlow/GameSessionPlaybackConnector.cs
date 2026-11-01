
using System.Collections;
using UnityEngine;

namespace Uta.GameFlow
{
    /// <summary>
    /// 正式な歌唱再生処理が完成するまでの仮アダプター。
    /// Playback状態で一定時間待ち、Resultへ進む。
    /// </summary>
    public class GameSessionPlaybackConnector : MonoBehaviour
    {
        [SerializeField]
        private GameSessionManager sessionManager;

        [SerializeField, Min(0.1f)]
        private float testPlaybackDuration = 3f;

        private Coroutine playbackCoroutine;

        private void OnEnable()
        {
            if (sessionManager != null)
                sessionManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (sessionManager != null)
                sessionManager.OnStateChanged -= HandleStateChanged;

            StopTestPlayback();
        }

        private void HandleStateChanged(
            GameSessionState previous,
            GameSessionState next)
        {
            if (previous == GameSessionState.Playback)
                StopTestPlayback();

            if (next == GameSessionState.Playback)
                StartTestPlayback();
        }

        private void StartTestPlayback()
        {
            StopTestPlayback();

            Debug.Log("TEMP playback started.");

            playbackCoroutine = StartCoroutine(PlaybackRoutine());
        }

        private IEnumerator PlaybackRoutine()
        {
            yield return new WaitForSeconds(testPlaybackDuration);

            playbackCoroutine = null;

            if (sessionManager == null ||
                sessionManager.CurrentState != GameSessionState.Playback)
                yield break;

            Debug.Log("TEMP playback completed.");

            sessionManager.TryTransition(GameSessionState.Result);
        }

        private void StopTestPlayback()
        {
            if (playbackCoroutine == null)
                return;

            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;

            Debug.Log("TEMP playback stopped.");
        }
    }
}
