
using System.Collections.Generic;
using NUnit.Framework;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class JudgmentSessionTests
    {
        private static PlayPlan CreatePlayPlan()
        {
            return new PlayPlan
            {
                SongId = "TEST_SONG",
                SectionIds = new List<string> { "A", "B" },
                Notes = new List<Note>
                {
                    new Note { NoteId = "A_N1" },
                    new Note { NoteId = "B_N1" }
                }
            };
        }

        [Test]
        public void DuplicateJudgment_IsRejected()
        {
            var session = new JudgmentSession(CreatePlayPlan());

            Assert.IsTrue(
                session.TryJudge("A_N1", 4, JudgmentType.Perfect));

            // 同じ音符は2回判定できない
            Assert.IsFalse(
                session.TryJudge("A_N1", 4, JudgmentType.Miss));

            Assert.AreEqual(1, session.JudgedNotes);
            Assert.AreEqual(
                JudgmentType.Perfect,
                session.GetResults()["A_N1"].Judgment);
        }

        [Test]
        public void FinalizeSession_RejectsMissingNotes()
        {
            var session = new JudgmentSession(CreatePlayPlan());

            Assert.IsTrue(
                session.TryJudge("A_N1", 4, JudgmentType.Great));

            // Bが未入力なら通常終了できない
            Assert.Throws<System.InvalidOperationException>(
                () => session.FinalizeSession());

            Assert.IsFalse(session.IsFinalized);
            Assert.AreEqual(1, session.JudgedNotes);

            // 残りの音符を入力すれば通常終了できる
            Assert.IsTrue(
                session.TryJudge("B_N1", 7, JudgmentType.Perfect));

            session.FinalizeSession();

            Assert.IsTrue(session.IsFinalized);
            Assert.AreEqual(2, session.JudgedNotes);

            var results = session.GetUnitResults();

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(InputState.Completed, results[0].InputState);
            Assert.AreEqual(InputState.Completed, results[1].InputState);
            Assert.AreEqual(4, results[0].CapturedPitchStep);
            Assert.AreEqual(7, results[1].CapturedPitchStep);
        }

        [Test]
        public void FinalizeSession_Twice_DoesNotChangeResults()
        {
            var session = new JudgmentSession(CreatePlayPlan());

            Assert.IsTrue(
                session.TryJudge("A_N1", 4, JudgmentType.Perfect));

            Assert.IsTrue(
                session.TryJudge("B_N1", 7, JudgmentType.Great));

            session.FinalizeSession();

            var before = session.GetUnitResults();

            // 通常終了は再実行しても結果が変わらない
            session.FinalizeSession();

            var after = session.GetUnitResults();

            Assert.AreEqual(2, session.JudgedNotes);
            Assert.AreEqual(before.Count, after.Count);

            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i].UnitId, after[i].UnitId);
                Assert.AreEqual(before[i].Judgment, after[i].Judgment);
                Assert.AreEqual(
                    before[i].CapturedPitchStep,
                    after[i].CapturedPitchStep);
            }

            // 確定後は追加判定できない
            Assert.IsFalse(
                session.TryJudge("A_N1", 5, JudgmentType.Miss));
        }

        [Test]
        public void UnknownNoteId_IsRejected()
        {
            var session = new JudgmentSession(CreatePlayPlan());

            Assert.IsFalse(
                session.TryJudge("A_REPEAT_N1", 4, JudgmentType.Perfect));

            Assert.AreEqual(0, session.JudgedNotes);
        }

        [Test]
        public void FinalizeTimedOut_PreservesPitchAndInputOrder()
        {
            var session = new JudgmentSession(CreatePlayPlan());

            // Bを先に判定しても結果はPlayPlan順になる
            Assert.IsTrue(
                session.TryJudge("B_N1", 7, JudgmentType.Great));

            var results = session.FinalizeTimedOut();

            Assert.AreEqual(2, results.Count);

            // Aは未入力のためTimedOut
            Assert.AreEqual("A_N1", results[0].UnitId);
            Assert.AreEqual(InputState.TimedOut, results[0].InputState);
            Assert.IsNull(results[0].CapturedPitchStep);
            Assert.IsNull(results[0].Judgment);
            Assert.AreEqual(0, results[0].BaseScore);

            // Bは入力済みで音程7が保存される
            Assert.AreEqual("B_N1", results[1].UnitId);
            Assert.AreEqual(InputState.Completed, results[1].InputState);
            Assert.AreEqual(7, results[1].CapturedPitchStep);
            Assert.AreEqual(
                JudgmentType.Great,
                results[1].Judgment);

            Assert.IsTrue(session.IsFinalized);
        }

        [Test]
        public void FinalizeTimedOut_RejectsFurtherJudgment()
        {
            var session = new JudgmentSession(CreatePlayPlan());

            Assert.IsTrue(
                session.TryJudge("A_N1", 4, JudgmentType.Perfect));
            session.FinalizeTimedOut();

            Assert.IsFalse(
                session.TryJudge("B_N1", 7, JudgmentType.Great));

            Assert.Throws<System.InvalidOperationException>(
                () => session.FinalizeTimedOut());
        }
    }
}
