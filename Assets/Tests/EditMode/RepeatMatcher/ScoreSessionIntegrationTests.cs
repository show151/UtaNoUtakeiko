
using System.Collections.Generic;
using NUnit.Framework;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class ScoreSessionIntegrationTests
    {
        [Test]
        public void JudgmentToScore_NormalCompletion()
        {
            var plan = new PlayPlan
            {
                SongId = "TEST_SONG",
                SectionIds = new List<string> { "A", "B" },
                Notes = new List<Note>
                {
                    new Note { NoteId = "A_N1" },
                    new Note { NoteId = "B_N1" }
                }
            };

            var policy = new ScorePolicy(
                100, 80, 50, 20, 0, 100, 0);

            // 1. セッション開始
            var session = new JudgmentSession(plan);

            // 2. 入力順序が前後してもNoteIdで管理
            Assert.IsTrue(
                session.TryJudge(
                    "B_N1", 7, JudgmentType.Great));

            Assert.IsTrue(
                session.TryJudge(
                    "A_N1", 4, JudgmentType.Perfect));

            // 3. 重複判定を拒否
            Assert.IsFalse(
                session.TryJudge(
                    "A_N1", 4, JudgmentType.Perfect));

            // 4. 通常終了
            session.FinalizeSession();

            // 5. スコア計算
            var score = ScoreCalculator.Calculate(
                plan,
                session.GetUnitResults(),
                policy,
                Difficulty.Easy,
                false);

            // 最大200に対して獲得180
            Assert.AreEqual(900000, score.Score);
            Assert.AreEqual(1000000, score.MaxScore);

            Assert.AreEqual(1, score.PerfectCount);
            Assert.AreEqual(1, score.GreatCount);
            Assert.AreEqual(0, score.MissCount);

            // 入力順序ではなくPlayPlan順に保存
            Assert.AreEqual(
                "A_N1", score.UnitResults[0].UnitId);
            Assert.AreEqual(
                "B_N1", score.UnitResults[1].UnitId);

            // 内訳の合計が最終スコアと一致
            Assert.AreEqual(
                score.Score,
                score.UnitResults[0].BaseScore +
                score.UnitResults[1].BaseScore);
        }

        [Test]
        public void JudgmentToScore_TimeoutCompletion()
        {
            var plan = new PlayPlan
            {
                SongId = "TEST_SONG",
                SectionIds = new List<string> { "A", "B" },
                Notes = new List<Note>
                {
                    new Note { NoteId = "A_N1" },
                    new Note { NoteId = "B_N1" }
                }
            };

            var policy = new ScorePolicy(
                100, 80, 50, 20, 0, 100, 0);

            var session = new JudgmentSession(plan);

            // Aのみ入力
            Assert.IsTrue(
                session.TryJudge(
                    "A_N1", 4, JudgmentType.Perfect));

            // B未入力で時間切れ
            var unitResults = session.FinalizeTimedOut();

            var score = ScoreCalculator.Calculate(
                plan,
                unitResults,
                policy,
                Difficulty.Normal,
                true);

            Assert.AreEqual(500000, score.Score);
            Assert.IsTrue(score.TimedOut);
            Assert.AreEqual(1, score.PerfectCount);
            Assert.AreEqual(0, score.MissCount);

            Assert.AreEqual(
                InputState.TimedOut,
                score.UnitResults[1].InputState);

            Assert.IsNull(
                score.UnitResults[1].Judgment);

            Assert.AreEqual(
                0, score.UnitResults[1].BaseScore);
        }
    }
}
