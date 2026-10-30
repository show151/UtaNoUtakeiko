
using System.Collections.Generic;
using NUnit.Framework;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class ScoreCalculatorTests
    {
        // テスト専用配点。正式配点ではない
        private static ScorePolicy CreatePolicy()
        {
            return new ScorePolicy(
                perfect: 100,
                great: 80,
                good: 50,
                bad: 20,
                miss: 0,
                holdPerfect: 100,
                holdMiss: 0);
        }

        private static PlayPlan CreatePlayPlan()
        {
            return new PlayPlan
            {
                SongId = "TEST_SONG",
                SectionIds = new List<string> { "A" },
                Notes = new List<Note>
                {
                    new Note { NoteId = "N1" },
                    new Note { NoteId = "N2" },
                    new Note { NoteId = "N3" },
                    new Note { NoteId = "N4" }
                }
            };
        }

        private static UnitResult Completed(
            string id, JudgmentType judgment)
        {
            return new UnitResult
            {
                UnitId = id,
                InputState = InputState.Completed,
                CapturedPitchStep = 5,
                Judgment = judgment
            };
        }

        [Test]
        public void AllPerfect_ReturnsMaximumScore()
        {
            var results = new List<UnitResult>
            {
                Completed("N1", JudgmentType.Perfect),
                Completed("N2", JudgmentType.Perfect),
                Completed("N3", JudgmentType.Perfect),
                Completed("N4", JudgmentType.Perfect)
            };

            var score = ScoreCalculator.Calculate(
                CreatePlayPlan(),
                results,
                CreatePolicy(),
                Difficulty.Easy,
                false);

            Assert.AreEqual(1000000, score.Score);
            Assert.AreEqual(1000000, score.MaxScore);
            Assert.AreEqual(4, score.PerfectCount);
            Assert.AreEqual(0, score.MissCount);
            Assert.AreEqual(100f, score.AchievementRate);
        }

        [Test]
        public void MixedJudgments_CalculatesWeightedScore()
        {
            var results = new List<UnitResult>
            {
                Completed("N1", JudgmentType.Perfect),
                Completed("N2", JudgmentType.Great),
                Completed("N3", JudgmentType.Good),
                Completed("N4", JudgmentType.Miss)
            };

            var score = ScoreCalculator.Calculate(
                CreatePlayPlan(),
                results,
                CreatePolicy(),
                Difficulty.Normal,
                false);

            // (100 + 80 + 50 + 0) / 400 * 1,000,000
            Assert.AreEqual(575000, score.Score);
            Assert.AreEqual(1000000, score.MaxScore);

            Assert.AreEqual(1, score.PerfectCount);
            Assert.AreEqual(1, score.GreatCount);
            Assert.AreEqual(1, score.GoodCount);
            Assert.AreEqual(0, score.BadCount);
            Assert.AreEqual(1, score.MissCount);
        }

        [Test]
        public void HoldNote_CountsNormalAndHoldJudgments()
        {
            var plan = new PlayPlan
            {
                SongId = "TEST_SONG",
                Notes = new List<Note>
                {
                    new Note { NoteId = "H1", IsHold = true }
                }
            };

            var results = new List<UnitResult>
            {
                new UnitResult
                {
                    UnitId = "H1",
                    InputState = InputState.Completed,
                    CapturedPitchStep = 5,
                    Judgment = JudgmentType.Great,
                    HoldJudgment = HoldJudgmentType.Perfect
                }
            };

            var score = ScoreCalculator.Calculate(
                plan, results, CreatePolicy(),
                Difficulty.Easy, false);

            // 通常Great(80) + 保持Perfect(100) / 最大200
            Assert.AreEqual(900000, score.Score);
            Assert.AreEqual(1000000, score.MaxScore);

            Assert.AreEqual(1, score.GreatCount);
            Assert.AreEqual(1, score.PerfectCount);
            Assert.AreEqual(0, score.MissCount);
        }

        [Test]
        public void TimedOutNote_DoesNotCountAsMiss()
        {
            var results = new List<UnitResult>
            {
                Completed("N1", JudgmentType.Perfect),
                Completed("N2", JudgmentType.Great),
                new UnitResult
                {
                    UnitId = "N3",
                    InputState = InputState.TimedOut
                },
                new UnitResult
                {
                    UnitId = "N4",
                    InputState = InputState.TimedOut
                }
            };

            var score = ScoreCalculator.Calculate(
                CreatePlayPlan(),
                results,
                CreatePolicy(),
                Difficulty.Normal,
                true);

            // 最大点は4音分、得点は入力済み2音分のみ
            Assert.AreEqual(450000, score.Score);
            Assert.AreEqual(1000000, score.MaxScore);
            Assert.IsTrue(score.TimedOut);

            Assert.AreEqual(1, score.PerfectCount);
            Assert.AreEqual(1, score.GreatCount);
            Assert.AreEqual(0, score.MissCount);
            Assert.AreEqual(4, score.UnitResults.Count);

            Assert.AreEqual(
                InputState.TimedOut,
                score.UnitResults[2].InputState);
        }

        [Test]
        public void UnknownNoteId_IsRejectedEvenWhenCountsMatch()
        {
            var results = new List<UnitResult>
            {
                Completed("N1", JudgmentType.Perfect),
                Completed("N2", JudgmentType.Great),
                Completed("N3", JudgmentType.Good),

                // N4の代わりに、入力対象外の音符が混入
                Completed("REPEAT_N4", JudgmentType.Perfect)
            };

            Assert.Throws<System.ArgumentException>(() =>
                ScoreCalculator.Calculate(
                    CreatePlayPlan(),
                    results,
                    CreatePolicy(),
                    Difficulty.Easy,
                    false));
        }

        [Test]
        public void DuplicateUnitResult_IsRejected()
        {
            var results = new List<UnitResult>
            {
                Completed("N1", JudgmentType.Perfect),
                Completed("N1", JudgmentType.Perfect),
                Completed("N3", JudgmentType.Good),
                Completed("N4", JudgmentType.Bad)
            };

            Assert.Throws<System.ArgumentException>(() =>
                ScoreCalculator.Calculate(
                    CreatePlayPlan(),
                    results,
                    CreatePolicy(),
                    Difficulty.Easy,
                    false));
        }

        [Test]
        public void Calculate_DoesNotModifyOriginalUnitResults()
        {
            var results = new List<UnitResult>
            {
                Completed("N1", JudgmentType.Perfect),
                Completed("N2", JudgmentType.Great),
                Completed("N3", JudgmentType.Good),
                Completed("N4", JudgmentType.Miss)
            };

            var score = ScoreCalculator.Calculate(
                CreatePlayPlan(),
                results,
                CreatePolicy(),
                Difficulty.Easy,
                false);

            // 入力元は変更されない
            foreach (var unit in results)
            {
                Assert.AreEqual(0, unit.BaseScore);
                Assert.AreEqual(0, unit.HoldScore);
            }

            // 採点結果は別のインスタンス
            for (int i = 0; i < results.Count; i++)
            {
                Assert.AreNotSame(results[i], score.UnitResults[i]);
            }

            // 100万点換算後の各音符の得点
            Assert.AreEqual(250000, score.UnitResults[0].BaseScore);
            Assert.AreEqual(200000, score.UnitResults[1].BaseScore);
            Assert.AreEqual(125000, score.UnitResults[2].BaseScore);
            Assert.AreEqual(0, score.UnitResults[3].BaseScore);

            int total = 0;
            foreach (var unit in score.UnitResults)
            {
                total += unit.BaseScore + unit.HoldScore;
            }

            Assert.AreEqual(score.Score, total);
            Assert.AreEqual(575000, score.Score);
        }

        [Test]
        public void Calculate_HoldScoreIsStoredInCopiedResult()
        {
            var plan = new PlayPlan
            {
                SongId = "TEST_SONG",
                Notes = new List<Note>
                {
                    new Note { NoteId = "H1", IsHold = true }
                }
            };

            var original = new UnitResult
            {
                UnitId = "H1",
                InputState = InputState.Completed,
                CapturedPitchStep = 5,
                Judgment = JudgmentType.Great,
                HoldJudgment = HoldJudgmentType.Perfect
            };

            var score = ScoreCalculator.Calculate(
                plan,
                new List<UnitResult> { original },
                CreatePolicy(),
                Difficulty.Easy,
                false);

            // 元の入力結果は変更されない
            Assert.AreEqual(0, original.BaseScore);
            Assert.AreEqual(0, original.HoldScore);

            // 通常Great 80/200、保持Perfect 100/200
            Assert.AreNotSame(original, score.UnitResults[0]);
            Assert.AreEqual(400000, score.UnitResults[0].BaseScore);
            Assert.AreEqual(500000, score.UnitResults[0].HoldScore);

            Assert.AreEqual(
                score.Score,
                score.UnitResults[0].BaseScore +
                score.UnitResults[0].HoldScore);

            Assert.AreEqual(900000, score.Score);
        }

        [Test]
        public void DuplicateNoteIdInPlayPlan_IsRejected()
        {
            var plan = new PlayPlan
            {
                SongId = "TEST_SONG",
                Notes = new List<Note>
                {
                    new Note { NoteId = "N1" },
                    new Note { NoteId = "N1" }
                }
            };

            var results = new List<UnitResult>
            {
                Completed("N1", JudgmentType.Perfect),
                Completed("N2", JudgmentType.Great)
            };

            var exception = Assert.Throws<System.ArgumentException>(() =>
                ScoreCalculator.Calculate(
                    plan,
                    results,
                    CreatePolicy(),
                    Difficulty.Easy,
                    false));

            StringAssert.Contains(
                "NoteIdが重複",
                exception.Message);

            StringAssert.Contains(
                "N1",
                exception.Message);
        }

        [Test]
        public void InvalidJudgmentValue_IsRejected()
        {
            var results = new List<UnitResult>
            {
                Completed("N1", (JudgmentType)999),
                Completed("N2", JudgmentType.Great),
                Completed("N3", JudgmentType.Good),
                Completed("N4", JudgmentType.Bad)
            };

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                ScoreCalculator.Calculate(
                    CreatePlayPlan(),
                    results,
                    CreatePolicy(),
                    Difficulty.Easy,
                    false));
        }
    }
}
