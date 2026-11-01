
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

        [Test]
        public void JudgmentToScore_HoldNoteCompletion()
        {
            var plan = new PlayPlan
            {
                SongId = "TEST_SONG",
                Notes = new List<Note>
                {
                    new Note
                    {
                        NoteId = "H1",
                        IsHold = true
                    }
                }
            };

            var policy = new ScorePolicy(
                100, 80, 50, 20, 0, 100, 0);

            var session = new JudgmentSession(plan);

            // 1. 通常判定を登録
            Assert.IsTrue(
                session.TryJudge(
                    "H1", 5, JudgmentType.Great));

            // 2. 保持判定が未登録なら通常終了できない
            Assert.Throws<System.InvalidOperationException>(
                () => session.FinalizeSession());

            Assert.IsFalse(session.IsFinalized);

            // 3. 保持判定を登録
            Assert.IsTrue(
                session.TryJudgeHold(
                    "H1", HoldJudgmentType.Perfect));

            // 4. 二重登録は拒否
            Assert.IsFalse(
                session.TryJudgeHold(
                    "H1", HoldJudgmentType.Miss));

            // 5. 通常終了
            session.FinalizeSession();

            Assert.IsTrue(session.IsFinalized);

            // 6. スコア計算
            var score = ScoreCalculator.Calculate(
                plan,
                session.GetUnitResults(),
                policy,
                Difficulty.Easy,
                false);

            // 通常Great(80) + 保持Perfect(100)
            // 最大200 → 900,000点
            Assert.AreEqual(900000, score.Score);
            Assert.AreEqual(1000000, score.MaxScore);

            Assert.AreEqual(1, score.GreatCount);
            Assert.AreEqual(1, score.PerfectCount);
            Assert.AreEqual(0, score.MissCount);

            // 採点内訳
            Assert.AreEqual(1, score.UnitResults.Count);
            Assert.AreEqual(
                JudgmentType.Great,
                score.UnitResults[0].Judgment);
            Assert.AreEqual(
                HoldJudgmentType.Perfect,
                score.UnitResults[0].HoldJudgment);

            Assert.AreEqual(
                400000, score.UnitResults[0].BaseScore);
            Assert.AreEqual(
                500000, score.UnitResults[0].HoldScore);
        }

        [Test]
        public void JudgmentToScore_HoldTimeout()
        {
            var plan = new PlayPlan
            {
                SongId = "TEST_SONG",
                Notes = new List<Note>
                {
                    new Note { NoteId = "H1", IsHold = true },
                    new Note { NoteId = "H2", IsHold = true }
                }
            };

            var policy = new ScorePolicy(
                100, 80, 50, 20, 0, 100, 0);

            var session = new JudgmentSession(plan);

            // H1は通常判定のみ完了
            Assert.IsTrue(
                session.TryJudge(
                    "H1", 5, JudgmentType.Great));

            // H2は未入力のまま時間切れ
            var units = session.FinalizeTimedOut();

            var score = ScoreCalculator.Calculate(
                plan,
                units,
                policy,
                Difficulty.Normal,
                true);

            // 4判定分の最大重み400のうちGreatの80だけ獲得
            Assert.AreEqual(200000, score.Score);
            Assert.AreEqual(1, score.GreatCount);
            Assert.AreEqual(1, score.MissCount);

            Assert.AreEqual(InputState.Completed,
                score.UnitResults[0].InputState);
            Assert.AreEqual(HoldJudgmentType.Miss,
                score.UnitResults[0].HoldJudgment);

            Assert.AreEqual(InputState.TimedOut,
                score.UnitResults[1].InputState);
            Assert.IsNull(score.UnitResults[1].Judgment);
            Assert.IsNull(score.UnitResults[1].HoldJudgment);
        }

        [Test]
        public void RepeatedPlayback_DoesNotIncreaseScoreOrJudgmentCounts()
        {
            // A→Bだけが入力対象
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

            Assert.AreEqual(2, session.TotalNotes);

            Assert.IsTrue(
                session.TryJudge("A_N1", 4, JudgmentType.Perfect));

            Assert.IsTrue(
                session.TryJudge("B_N1", 7, JudgmentType.Great));

            // Aの再生時に再判定しようとしても拒否される
            Assert.IsFalse(
                session.TryJudge("A_N1", 4, JudgmentType.Perfect));

            session.FinalizeSession();

            var firstScore = ScoreCalculator.Calculate(
                plan,
                session.GetUnitResults(),
                policy,
                Difficulty.Easy,
                false);

            // 歌唱再生がA→B→Aになっても、
            // 採点対象のPlayPlanは変わらない
            var secondScore = ScoreCalculator.Calculate(
                plan,
                session.GetUnitResults(),
                policy,
                Difficulty.Easy,
                false);

            Assert.AreEqual(900000, firstScore.Score);
            Assert.AreEqual(firstScore.Score, secondScore.Score);
            Assert.AreEqual(firstScore.MaxScore, secondScore.MaxScore);

            Assert.AreEqual(2, firstScore.UnitResults.Count);
            Assert.AreEqual(1, firstScore.PerfectCount);
            Assert.AreEqual(1, firstScore.GreatCount);

            Assert.AreEqual(
                firstScore.PerfectCount,
                secondScore.PerfectCount);

            Assert.AreEqual(
                firstScore.GreatCount,
                secondScore.GreatCount);
        }

        [Test]
        public void RepeatedPlaybackPlan_DoesNotDoubleScore()
        {
            // A → B → A の3区間
            var a1 = new Section
            {
                SongId = "TEST_SONG",
                SectionId = "A1",
                StartMeasure = 1,
                EndMeasure = 1,
                Notes = new List<Note>
                {
                    new Note
                    {
                        NoteId = "A1_N1",
                        MeasureNumber = 1,
                        TargetPitchStep = 5,
                        DurationBeats = 1f
                    }
                }
                    };

                    var b = new Section
                    {
                        SongId = "TEST_SONG",
                        SectionId = "B",
                        StartMeasure = 2,
                        EndMeasure = 2,
                        Notes = new List<Note>
                {
                    new Note
                    {
                        NoteId = "B_N1",
                        MeasureNumber = 2,
                        TargetPitchStep = 7,
                        DurationBeats = 1f
                    }
                }
                    };

                    var a2 = new Section
                    {
                        SongId = "TEST_SONG",
                        SectionId = "A2",
                        StartMeasure = 3,
                        EndMeasure = 3,
                        Notes = new List<Note>
                {
                    new Note
                    {
                        NoteId = "A2_N1",
                        MeasureNumber = 3,
                        TargetPitchStep = 5,
                        DurationBeats = 1f
                    }
                }
            };

            // 繰り返し区間を検出
            Assert.IsTrue(
                RepeatMatcher.TryMatch(
                    a1, a2, out var mapping, out string matchError),
                matchError);

            // 入力用と歌唱再生用の譜面を生成
            Assert.IsTrue(
                PlanBuilder.TryBuild(
                    new List<Section> { a1, b, a2 },
                    new List<RepeatMapping> { mapping },
                    out var playPlan,
                    out var playbackPlan,
                    out string error),
                error);

            Assert.AreEqual(2, playPlan.Notes.Count);
            Assert.AreEqual(3, playbackPlan.Sections.Count);
            Assert.AreEqual(
                "A1_N1",
                playbackPlan.Sections[2].SourceNoteIds[0]);

            // 入力はAとBの2音だけ
            var session = new JudgmentSession(playPlan);

            Assert.IsTrue(session.TryJudge(
                "A1_N1", 5, JudgmentType.Perfect));

            Assert.IsTrue(session.TryJudge(
                "B_N1", 7, JudgmentType.Great));

            // A2は入力対象外
            Assert.IsFalse(session.TryJudge(
                "A2_N1", 5, JudgmentType.Perfect));

            session.FinalizeSession();

            var policy = new ScorePolicy(
                100, 80, 50, 20, 0, 100, 0);

            var score = ScoreCalculator.Calculate(
                playPlan,
                session.GetUnitResults(),
                policy,
                Difficulty.Easy,
                false);

            // A1+Bだけでスコアを確定
            Assert.AreEqual(900000, score.Score);
            Assert.AreEqual(1000000, score.MaxScore);

            Assert.AreEqual(2, score.UnitResults.Count);
            Assert.AreEqual(1, score.PerfectCount);
            Assert.AreEqual(1, score.GreatCount);
            Assert.AreEqual(0, score.MissCount);
        }

        [Test]
        public void ChangingPlaybackRepeats_DoesNotChangeScore()
        {
            Section MakeSection(string id, int measure, int pitch)
            {
                return new Section
                {
                    SongId = "TEST_SONG",
                    SectionId = id,
                    StartMeasure = measure,
                    EndMeasure = measure,
                    Notes = new List<Note>
                    {
                        new Note
                        {
                            NoteId = id + "_N1",
                            MeasureNumber = measure,
                            TargetPitchStep = pitch,
                            DurationBeats = 1f
                        }
                    }
                };
            }

            var a1 = MakeSection("A1", 1, 5);
            var b = MakeSection("B", 2, 7);
            var a2 = MakeSection("A2", 3, 5);

            // パターン1：繰り返しなし
            Assert.IsTrue(PlanBuilder.TryBuild(
                new List<Section> { a1, b },
                new List<RepeatMapping>(),
                out var playPlan1,
                out var playbackPlan1,
                out string error1), error1);

            // パターン2：A2をA1の繰り返しとして扱う
            Assert.IsTrue(RepeatMatcher.TryMatch(
                a1, a2, out var mapping, out string matchError),
                matchError);

            Assert.IsTrue(PlanBuilder.TryBuild(
                new List<Section> { a1, b, a2 },
                new List<RepeatMapping> { mapping },
                out var playPlan2,
                out var playbackPlan2,
                out string error2), error2);

            Assert.AreEqual(2, playbackPlan1.Sections.Count);
            Assert.AreEqual(3, playbackPlan2.Sections.Count);

            Assert.AreEqual(2, playPlan1.Notes.Count);
            Assert.AreEqual(2, playPlan2.Notes.Count);

            // どちらも同じNoteIdが入力対象
            CollectionAssert.AreEqual(
                new[] { "A1_N1", "B_N1" },
                playPlan1.Notes.ConvertAll(n => n.NoteId));

            CollectionAssert.AreEqual(
                new[] { "A1_N1", "B_N1" },
                playPlan2.Notes.ConvertAll(n => n.NoteId));

            var policy = new ScorePolicy(
                100, 80, 50, 20, 0, 100, 0);

            ScoreResult CalculateScore(PlayPlan plan)
            {
                var session = new JudgmentSession(plan);

                Assert.IsTrue(session.TryJudge(
                    "A1_N1", 5, JudgmentType.Perfect));

                Assert.IsTrue(session.TryJudge(
                    "B_N1", 7, JudgmentType.Great));

                session.FinalizeSession();

                return ScoreCalculator.Calculate(
                    plan,
                    session.GetUnitResults(),
                    policy,
                    Difficulty.Easy,
                    false);
            }

            var score1 = CalculateScore(playPlan1);
            var score2 = CalculateScore(playPlan2);

            Assert.AreEqual(900000, score1.Score);
            Assert.AreEqual(score1.Score, score2.Score);
            Assert.AreEqual(score1.MaxScore, score2.MaxScore);
            Assert.AreEqual(score1.PerfectCount, score2.PerfectCount);
            Assert.AreEqual(score1.GreatCount, score2.GreatCount);
            Assert.AreEqual(score1.UnitResults.Count, score2.UnitResults.Count);
        }
    }
}
