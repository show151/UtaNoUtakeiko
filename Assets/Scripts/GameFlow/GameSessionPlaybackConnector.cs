
using System;
using System.Collections;
using UnityEngine;

namespace Uta.GameFlow
{
    public class GameSessionPlaybackConnector : MonoBehaviour
    {
        [SerializeField]
        private GameSessionManager sessionManager;

        [SerializeField]
        private GameSessionPlayConnector playConnector;

        private AudioSource audioSource;
        private AudioClip generatedClip;
        private Coroutine playbackCoroutine;

        private void Awake()
        {
            if (playConnector == null)
            {
                playConnector = GetComponent<GameSessionPlayConnector>();
            }

            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            if (sessionManager != null)
                sessionManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (sessionManager != null)
                sessionManager.OnStateChanged -= HandleStateChanged;

            StopPlayback();
        }

        private void HandleStateChanged(
            GameSessionState previous,
            GameSessionState next)
        {
            if (previous == GameSessionState.Playback)
                StopPlayback();

            if (next == GameSessionState.Playback)
            {
                StopPlayback();
                playbackCoroutine = StartCoroutine(PlaybackRoutine());
            }
        }

        private IEnumerator PlaybackRoutine()
        {
            // 状態遷移処理が終わってから歌唱を生成する
            yield return null;

            if (!IsPlayback())
            {
                playbackCoroutine = null;
                yield break;
            }

            if (playConnector == null ||
                playConnector.CurrentSongChart == null ||
                playConnector.CurrentPlaybackPlan == null ||
                playConnector.CurrentScoreResult == null)
            {

                Debug.LogError(
                    "歌唱再生データ確認:\n" +
                    $"PlayConnector: {playConnector != null}\n" +
                    $"SongChart: {playConnector != null && playConnector.CurrentSongChart != null}\n" +
                    $"PlaybackPlan: {playConnector != null && playConnector.CurrentPlaybackPlan != null}\n" +
                    $"ScoreResult: {playConnector != null && playConnector.CurrentScoreResult != null}"
                );
                playbackCoroutine = null;
                sessionManager.RecoverToTitle();
                yield break;
            }

            double endTime = 0;
            bool playbackStarted = false;

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    generatedClip = VocalPlaybackComposer.Compose(
                        playConnector.CurrentSongChart,
                        playConnector.CurrentPlaybackPlan,
                        playConnector.CurrentScoreResult
                    );
                }
                finally
                {
                    stopwatch.Stop();

                    Debug.Log(
                        $"[歌唱パフォーマンス] 生成時間: " +
                        $"{stopwatch.Elapsed.TotalMilliseconds:F2} ms"
                    );
                }

                if (generatedClip == null)
                    throw new InvalidOperationException(
                        "歌唱音声を生成できませんでした。"
                    );

                audioSource.clip = generatedClip;

                double startTime = AudioSettings.dspTime + 0.15;
                endTime = startTime + generatedClip.length;

                audioSource.PlayScheduled(startTime);

                playbackStarted = true;

                Debug.Log(
                    $"歌唱再生開始: {generatedClip.length:F2}秒"
                );
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            // 例外発生時はタイトルへ復帰
            if (!playbackStarted)
            {
                playbackCoroutine = null;
                sessionManager.RecoverToTitle();
                yield break;
            }

            // try-catchの外側で再生終了を待つ
            while (IsPlayback() &&
                   AudioSettings.dspTime < endTime)
            {
                yield return null;
            }

            playbackCoroutine = null;

            if (IsPlayback())
            {
                Debug.Log("歌唱再生完了");

                if (!sessionManager.TryTransition(
                    GameSessionState.Result))
                {
                    sessionManager.RecoverToTitle();
                }
            }
        }

        private bool IsPlayback()
        {
            return sessionManager != null &&
                   sessionManager.CurrentState ==
                   GameSessionState.Playback;
        }

        private void StopPlayback()
        {
            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }

            if (generatedClip != null)
            {
                Destroy(generatedClip);
                generatedClip = null;
            }
        }
    }
}
