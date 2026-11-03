
using System;
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

        private int currentBpm;

        // 後続の採点・歌唱処理で使う
        public PlayPlan CurrentPlayPlan { get; private set; }
        public PlaybackPlan CurrentPlaybackPlan { get; private set; }

        public JudgmentSession CurrentJudgmentSession
        {
            get;
            private set;
        }

        public ScoreResult CurrentScoreResult { get; private set; }

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
                noteProgression.OnCompleted += HandlePlayCompleted;
                noteProgression.OnNoteJudgedWithDuration += HandleNoteJudged;
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
                noteProgression.OnCompleted -= HandlePlayCompleted;
                noteProgression.OnNoteJudgedWithDuration -= HandleNoteJudged;
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

            string[] noteIds =
                new string[CurrentPlayPlan.Notes.Count];

            for (int i = 0; i < noteIds.Length; i++)
            {
                noteIds[i] = CurrentPlayPlan.Notes[i].NoteId;
            }

            noteProgression.StartPlay(notePositions, noteIds);
        }

        private bool TryPreparePlay(out float[] notePositions)
        {
            notePositions = null;
            CurrentPlayPlan = null;
            CurrentPlaybackPlan = null;
            CurrentJudgmentSession = null;

            currentBpm = 0;

            CurrentScoreResult = null;

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

            currentBpm = chart.Bpm;

            return true;
        }

        private void HandlePlayCompleted()
        {
            FinalizeScoreAndAdvance(timedOut: false);
        }

        private void HandleSessionTimedOut()
        {
            Debug.LogWarning(
                "時間切れのため、未入力音符を確定します。"
            );

            FinalizeScoreAndAdvance(timedOut: true);
        }

        private void FinalizeScoreAndAdvance(bool timedOut)
        {
            if (sessionManager == null ||
                sessionManager.CurrentState != GameSessionState.Playing)
            {
                return;
            }

            // スコアは1セッションにつき1回だけ確定
            if (CurrentScoreResult != null)
                return;

            if (CurrentJudgmentSession == null ||
                CurrentPlayPlan == null)
            {
                Debug.LogError("採点に必要なデータがありません。");
                sessionManager.RecoverToTitle();
                return;
            }

            try
            {
                if (timedOut)
                {
                    // 未入力音符をTimedOutとして確定
                    CurrentJudgmentSession.FinalizeTimedOut();
                }
                else
                {
                    // 全音符の入力完了を確認して確定
                    CurrentJudgmentSession.FinalizeSession();
                }

                var unitResults =
                    CurrentJudgmentSession.GetUnitResults();

                // 既存Step 11と同じ暫定配点
                var scorePolicy = new ScorePolicy(
                    perfect: 100,
                    great: 80,
                    good: 60,
                    bad: 30,
                    miss: 0,
                    holdPerfect: 100,
                    holdMiss: 0
                );

                CurrentScoreResult = ScoreCalculator.Calculate(
                    CurrentPlayPlan,
                    unitResults,
                    scorePolicy,
                    Difficulty.Easy,
                    timedOut
                );

                var score = CurrentScoreResult;

                Debug.Log(
                    "===== スコア確定 =====\n" +
                    $"時間切れ: {score.TimedOut}\n" +
                    $"スコア: {score.Score:N0} / {score.MaxScore:N0}\n" +
                    $"達成率: {score.AchievementRate:F2}%\n" +
                    $"Perfect: {score.PerfectCount}\n" +
                    $"Great: {score.GreatCount}\n" +
                    $"Good: {score.GoodCount}\n" +
                    $"Bad: {score.BadCount}\n" +
                    $"Miss: {score.MissCount}\n" +
                    $"入力済み音符数: " +
                    $"{CurrentJudgmentSession.JudgedNotes}/" +
                    $"{CurrentJudgmentSession.TotalNotes}"
                );

                // 通常終了・時間切れの両方でPlaybackへ
                if (!sessionManager.TryTransition(
                    GameSessionState.Playback))
                {
                    Debug.LogError(
                        "Playbackへの遷移に失敗しました。"
                    );

                    CurrentScoreResult = null;
                    sessionManager.RecoverToTitle();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("スコア確定に失敗しました。");
                Debug.LogException(ex);

                CurrentScoreResult = null;
                sessionManager.RecoverToTitle();
            }
        }

        private void HandleNoteJudged(
            string noteId,
            string result,
            float position,
            float heldSeconds)
        {
            if (sessionManager == null ||
                sessionManager.CurrentState != GameSessionState.Playing ||
                CurrentJudgmentSession == null ||
                CurrentPlayPlan == null ||
                currentBpm <= 0)
            {
                return;
            }

            if (!Enum.TryParse<JudgmentType>(
                result, out var judgment) ||
                !Enum.IsDefined(typeof(JudgmentType), judgment))
            {
                Debug.LogError($"不明な判定: {result}");
                return;
            }

            if (float.IsNaN(position) ||
                float.IsInfinity(position) ||
                position < 0f ||
                position > 1f ||
                float.IsNaN(heldSeconds) ||
                float.IsInfinity(heldSeconds) ||
                heldSeconds < 0f)
            {
                Debug.LogError("入力データが不正です。");
                return;
            }

            // PlayPlanから対象音符を取得
            Note targetNote = null;

            foreach (Note note in CurrentPlayPlan.Notes)
            {
                if (note.NoteId == noteId)
                {
                    targetNote = note;
                    break;
                }
            }

            if (targetNote == null)
            {
                Debug.LogError($"音符が見つかりません: {noteId}");
                return;
            }

            int capturedPitchStep = Mathf.RoundToInt(
                Mathf.Lerp(minPitchStep, maxPitchStep, position)
            );

            // 通常の5段階判定を一度だけ登録
            if (!CurrentJudgmentSession.TryJudge(
                noteId,
                capturedPitchStep,
                judgment))
            {
                Debug.LogError($"通常判定の登録失敗: {noteId}");
                return;
            }

            // 長押し音符だけ保持判定を追加
            if (targetNote.IsHold)
            {
                float requiredSeconds =
                    targetNote.DurationBeats * 60f / currentBpm;

                HoldJudgmentType holdJudgment =
                    heldSeconds + holdToleranceSeconds >= requiredSeconds
                        ? HoldJudgmentType.Perfect
                        : HoldJudgmentType.Miss;

                if (!CurrentJudgmentSession.TryJudgeHold(
                    noteId, holdJudgment))
                {
                    Debug.LogError($"保持判定の登録失敗: {noteId}");
                    return;
                }

                Debug.Log(
                    $"保持判定: {noteId}, " +
                    $"必要={requiredSeconds:F2}s, " +
                    $"実際={heldSeconds:F2}s, " +
                    $"結果={holdJudgment}"
                );
            }

            Debug.Log(
                $"判定登録成功: {noteId}, " +
                $"Pitch={capturedPitchStep}, " +
                $"Judgment={judgment}, " +
                $"Count={CurrentJudgmentSession.JudgedNotes}/" +
                $"{CurrentJudgmentSession.TotalNotes}"
            );
        }
    }
}
