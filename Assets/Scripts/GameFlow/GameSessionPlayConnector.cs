
using System;
using System.Collections.Generic;
using UnityEngine;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace Uta.GameFlow
{
    public class GameSessionPlayConnector : MonoBehaviour
    {
        [SerializeField]
        private GameSessionManager sessionManager;

        [SerializeField]
        private NoteProgression noteProgression;

        // Unity Inspectorから正式譜面JSONを設定する
        [SerializeField]
        private TextAsset songChartJson;

        // C4 = 0、A4 = 9
        // 画面位置への変換範囲（暫定）
        [SerializeField]
        private int minPitchStep = 0;

        [SerializeField]
        private int maxPitchStep = 9;

        [SerializeField, Range(0f, 0.2f)]
        private float holdToleranceSeconds = 0.05f;

        // 後続の採点・歌唱処理で使う
        public PlayPlan CurrentPlayPlan { get; private set; }
        public PlaybackPlan CurrentPlaybackPlan { get; private set; }

        public JudgmentSession CurrentJudgmentSession
        {
            get;
            private set;
        }

        public ScoreResult CurrentScoreResult { get; private set; }
        public ScoreResult CurrentVocalScoreResult { get; private set; }
        public List<InputUnit> CurrentInputUnits { get; private set; }

        public SongChart CurrentSongChart { get; private set; }

        private void OnEnable()
        {
            if (sessionManager != null)
            {
                sessionManager.OnStateChanged += HandleStateChanged;
                sessionManager.OnSessionTimedOut += HandleSessionTimedOut;
            }
        
            if (noteProgression != null)
            {
                noteProgression.OnGameplayCompleted += HandleGameplayCompleted;
            }
        }
        
        private void OnDisable()
        {
            if (sessionManager != null)
            {
                sessionManager.OnStateChanged -= HandleStateChanged;
                sessionManager.OnSessionTimedOut -= HandleSessionTimedOut;
            }
        
            if (noteProgression != null)
            {
                noteProgression.OnGameplayCompleted -= HandleGameplayCompleted;
                noteProgression.StopPlayback();
            }
        }

        private void HandleStateChanged(
            GameSessionState previous,
            GameSessionState next)
        {
            if (previous == GameSessionState.Playing &&
                next != GameSessionState.Playing)
            {
                if (noteProgression != null)
                    noteProgression.StopPlayback();
            }

            if (next != GameSessionState.Playing)
                return;

            if (noteProgression == null ||
                !TryPreparePlay(out float[] notePositions))
            {
                Debug.LogError(
                    "正式譜面の初期化に失敗しました。"
                );

                sessionManager?.RecoverToTitle();
                return;
            }

            // Production Playing always uses scheduled, player-driven
            // InputUnits (TAP/HOLD). No preview or legacy note-by-note fallback.
            if (!TryStartGameplay())
            {
                Debug.LogError("本番ゲームプレイの開始に失敗しました。");
                sessionManager?.RecoverToTitle();
            }
        }

        private bool TryPreparePlay(out float[] notePositions)
        {
            notePositions = null;
            CurrentPlayPlan = null;
            CurrentPlaybackPlan = null;
            CurrentJudgmentSession = null;

            CurrentScoreResult = null;
            CurrentVocalScoreResult = null;
            CurrentInputUnits = null;

            CurrentSongChart = null;

            if (songChartJson == null)
            {
                Debug.LogError("Song Chart JSONが設定されていません。");
                return false;
            }

            SongChart chart;

            try
            {
                chart = JsonUtility.FromJson<SongChart>(
                    songChartJson.text
                );
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }

            if (chart == null ||
                chart.Bpm <= 0 ||
                chart.Sections == null ||
                chart.RepeatMappings == null)
            {
                Debug.LogError("譜面データが不正です。");
                return false;
            }

            // ① 先にPlayPlanとPlaybackPlanを生成
            if (!PlanBuilder.TryBuild(
                chart.Sections,
                chart.RepeatMappings,
                out PlayPlan playPlan,
                out PlaybackPlan playbackPlan,
                out string error))
            {
                Debug.LogError("PlayPlan生成失敗: " + error);
                return false;
            }

            // ② 音符と表示範囲を検証
            if (playPlan.Notes == null ||
                playPlan.Notes.Count == 0 ||
                maxPitchStep <= minPitchStep)
            {
                Debug.LogError("入力音符または音程変換範囲が不正です。");
                return false;
            }

            // ③ 音程を画面位置に変換
            notePositions = new float[playPlan.Notes.Count];

            for (int i = 0; i < playPlan.Notes.Count; i++)
            {
                Note note = playPlan.Notes[i];

                if (note.TargetPitchStep < minPitchStep ||
                    note.TargetPitchStep > maxPitchStep)
                {
                    Debug.LogError(
                        $"音程が表示範囲外: {note.NoteId}"
                    );
                    return false;
                }

                notePositions[i] = Mathf.InverseLerp(
                    minPitchStep,
                    maxPitchStep,
                    note.TargetPitchStep
                );
            }

            // ④ 生成したプランを保持
            CurrentPlayPlan = playPlan;
            CurrentPlaybackPlan = playbackPlan;

            // ⑤ 生成後にJudgmentSessionを初期化
            try
            {
                CurrentJudgmentSession = new JudgmentSession(playPlan);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);

                CurrentPlayPlan = null;
                CurrentPlaybackPlan = null;
                CurrentJudgmentSession = null;
                notePositions = null;

                return false;
            }

            CurrentSongChart = chart;

            Debug.Log(
                $"正式譜面読込成功: {chart.SongId} / " +
                $"{chart.DifficultyId} / BPM={chart.Bpm}"
            );

            Debug.Log(
                $"入力音符数: {playPlan.Notes.Count}, " +
                $"再生区間数: {playbackPlan.Sections.Count}"
            );

            return true;
        }

        // The regular Playing state always uses the grouped InputUnit model.
        // A hold is determined by contiguous source notes, never Note.IsHold.
        private bool TryStartGameplay()
        {
            if (CurrentSongChart == null || CurrentPlayPlan == null)
            {
                Debug.LogError("ゲームプレイ用の譜面がありません。");
                return false;
            }

            if (!InputUnitBuilder.TryBuild(
                CurrentSongChart, CurrentPlayPlan,
                out var units, out string error))
            {
                Debug.LogError("InputUnit生成失敗: " + error);
                return false;
            }

            var positions = new float[units.Count];
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].TargetPitchStep < minPitchStep ||
                    units[i].TargetPitchStep > maxPitchStep)
                {
                    Debug.LogError(
                        "入力区間の音程が表示範囲外: " + units[i].UnitId);
                    return false;
                }

                positions[i] = Mathf.InverseLerp(
                    minPitchStep, maxPitchStep,
                    units[i].TargetPitchStep);
            }

            Debug.Log(
                $"本番ゲームプレイ: 入力区間={units.Count}, " +
                $"長押し={units.FindAll(u => u.IsHold).Count}, " +
                $"時間={units[units.Count - 1].EndSeconds:F2}s");

            CurrentInputUnits = units;
            return noteProgression.StartGameplayInput(
                units, positions, holdToleranceSeconds);
        }

        private void HandleGameplayCompleted()
        {
            if (sessionManager == null ||
                sessionManager.CurrentState != GameSessionState.Playing ||
                CurrentScoreResult != null)
                return;

            try
            {
                var results = noteProgression.LastGameplayResults;
                if (CurrentInputUnits == null || results == null)
                    throw new InvalidOperationException("ゲームプレイの判定結果がありません。");

                var policy = new ScorePolicy(
                    perfect: 100, great: 80, good: 60,
                    bad: 30, miss: 0, holdPerfect: 100, holdMiss: 0);

                Difficulty difficulty = Difficulty.Easy;
                if (CurrentSongChart != null &&
                    !string.IsNullOrWhiteSpace(CurrentSongChart.DifficultyId))
                    Enum.TryParse(CurrentSongChart.DifficultyId, true, out difficulty);

                CurrentScoreResult = GroupedInputScoreAdapter.Calculate(
                    CurrentPlayPlan, CurrentInputUnits, results,
                    policy, difficulty, minPitchStep, maxPitchStep);

                CurrentVocalScoreResult =
                    GroupedInputScoreAdapter.ExpandForVocalPlayback(
                        CurrentScoreResult, CurrentInputUnits);

                Debug.Log(
                    $"本番プレイ採点完了: {CurrentScoreResult.Score:N0}/" +
                    $"{CurrentScoreResult.MaxScore:N0}, " +
                    $"入力区間={CurrentScoreResult.UnitResults.Count}, " +
                    $"歌唱音符={CurrentVocalScoreResult.UnitResults.Count}");

                if (!sessionManager.TryTransition(GameSessionState.GameFinished))
                    sessionManager.RecoverToTitle();
            }
            catch (Exception ex)
            {
                Debug.LogError("本番プレイの採点・歌唱準備に失敗しました。");
                Debug.LogException(ex);
                CurrentScoreResult = null;
                CurrentVocalScoreResult = null;
                sessionManager.RecoverToTitle();
            }
        }

        private void HandleSessionTimedOut()
        {
            if (sessionManager == null ||
                sessionManager.CurrentState != GameSessionState.Playing)
                return;

            // Finalize unresolved units as timed-out Misses, then use the same
            // scoring and Playback transition as normal gameplay completion.
            if (noteProgression != null &&
                noteProgression.CompleteGameplayAsTimedOut())
            {
                Debug.LogWarning(
                    "全体制限時間を超えたため、未判定入力をMissとして確定しFinish演出へ移ります。");
                return;
            }

            Debug.LogError(
                "制限時間切れ時にゲームプレイを確定できませんでした。タイトルに戻ります。");
            sessionManager.RecoverToTitle();
        }
    }
}
