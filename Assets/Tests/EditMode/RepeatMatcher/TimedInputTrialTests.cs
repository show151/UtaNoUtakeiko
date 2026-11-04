using System.Collections.Generic;
using NUnit.Framework;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace UtaNoUtakeiko.Tests
{
    public class TimedInputTrialTests
    {
        private static TimedInputTrial CreateTrial()
        {
            return new TimedInputTrial(new List<InputUnit>
            {
                new InputUnit
                {
                    UnitId = "A",
                    TargetPitchStep = 0,
                    IsHold = true,
                    HoldDurationSeconds = 0.7f,
                    StartSeconds = 0f,
                    EndSeconds = 0.7f
                },
                new InputUnit
                {
                    UnitId = "B",
                    TargetPitchStep = 2,
                    IsHold = false,
                    StartSeconds = 0.7f,
                    EndSeconds = 1.4f
                }
            });
        }


        [Test]
        public void ProductionInput_DoesNotAdvanceOrExpireWithoutARelease()
        {
            TimedInputTrial trial = CreateTrial();

            // Production gameplay never invokes Expire: elapsed wall-clock time
            // alone must not create scores or advance to the next input.
            Assert.AreEqual(0, trial.ResolvedCount);
            Assert.IsFalse(trial.IsResolved(0));
            Assert.IsFalse(trial.IsResolved(1));

            Assert.IsTrue(trial.TryRecordPlayerRelease(
                0, "Perfect", 0.5f, 0.71f, 0.05f));
            Assert.IsTrue(trial.IsResolved(0));
            Assert.IsFalse(trial.IsResolved(1));
            Assert.AreEqual(1, trial.ResolvedCount);

            Assert.IsFalse(trial.TryRecordPlayerRelease(
                0, "Perfect", 0.5f, 0.71f, 0.05f));

            Assert.IsTrue(trial.TryRecordPlayerRelease(
                1, "Great", 0.4f, 0.13f, 0.05f));
            Assert.AreEqual(2, trial.ResolvedCount);
        }

        [Test]
        public void ProductionHold_CompletesAtRequiredDuration_ExactlyOnce()
        {
            var unit = new InputUnit
            {
                UnitId = "THREE-NOTE-HOLD",
                IsHold = true,
                StartSeconds = 0f,
                EndSeconds = 1f,
                HoldDurationSeconds = 3f,
                SourceNoteIds = new List<string> { "A", "B", "C" }
            };
            var trial = new TimedInputTrial(new List<InputUnit> { unit });
            var results = new List<TimedInputTrialResult>();
            trial.OnResolved += results.Add;

            Assert.IsFalse(trial.TryRecordHoldSuccess(0, "Perfect", 0.5f, 2.99f));
            Assert.IsTrue(trial.TryRecordHoldSuccess(0, "Great", 0.5f, 3f));
            Assert.IsFalse(trial.TryRecordHoldSuccess(0, "Perfect", 0.5f, 4f));
            Assert.AreEqual(1, trial.ResolvedCount);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Great", results[0].PitchJudgment);
            Assert.AreEqual(true, results[0].HoldSucceeded);
            Assert.AreEqual(3f, results[0].HeldSeconds, 0.001f);
        }

        [Test]
        public void ProductionInput_ShortHoldFails_AndLongHoldSucceeds()
        {
            var shortTrial = CreateTrial();
            var shortResults = new List<TimedInputTrialResult>();
            shortTrial.OnResolved += shortResults.Add;
            Assert.IsTrue(shortTrial.TryRecordPlayerRelease(
                0, "Perfect", 0.5f, 0.3f, 0.05f));
            Assert.AreEqual(false, shortResults[0].HoldSucceeded);
            Assert.IsFalse(shortResults[0].TimedOut);

            var longTrial = CreateTrial();
            var longResults = new List<TimedInputTrialResult>();
            longTrial.OnResolved += longResults.Add;
            Assert.IsTrue(longTrial.TryRecordPlayerRelease(
                0, "Perfect", 0.5f, 0.7f, 0.05f));
            Assert.AreEqual(true, longResults[0].HoldSucceeded);
            Assert.IsFalse(longResults[0].TimedOut);
        }

        [Test]
        public void ReleaseAfterNextUnitStart_StillResolvesPressedHold()
        {
            TimedInputTrial trial = CreateTrial();

            Assert.AreEqual(0, trial.ActiveIndexAt(0.1f));
            Assert.AreEqual(1, trial.ActiveIndexAt(0.71f));

            bool accepted = trial.TryRecordRelease(
                0, 0.74f, "Perfect", 0.5f, 0.74f, 0.05f);

            Assert.IsTrue(accepted);
            Assert.IsTrue(trial.IsResolved(0));
            Assert.IsFalse(trial.IsResolved(1));
            Assert.AreEqual(1, trial.ResolvedCount);
        }

        [Test]
        public void MissingInputAutomaticallyBecomesMiss_ExactlyOnce()
        {
            TimedInputTrial trial = CreateTrial();
            var results = new List<TimedInputTrialResult>();
            trial.OnResolved += results.Add;

            trial.Expire(0.81f);
            Assert.AreEqual(0, trial.ResolvedCount);

            trial.Expire(0.83f);
            trial.Expire(1.53f);
            trial.Expire(3f);

            Assert.AreEqual(2, trial.ResolvedCount);
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results[0].TimedOut);
            Assert.AreEqual("Miss", results[0].PitchJudgment);
            Assert.AreEqual(false, results[0].HoldSucceeded);
            Assert.IsTrue(results[1].TimedOut);
            Assert.IsNull(results[1].HoldSucceeded);
        }

        [Test]
        public void TooShortHoldIsMiss_AndDuplicateReleaseIsRejected()
        {
            TimedInputTrial trial = CreateTrial();
            var results = new List<TimedInputTrialResult>();
            trial.OnResolved += results.Add;

            Assert.IsTrue(trial.TryRecordRelease(
                0, 0.4f, "Great", 0.4f, 0.4f, 0.05f));

            Assert.AreEqual(false, results[0].HoldSucceeded);
            Assert.IsFalse(results[0].TimedOut);
            Assert.IsFalse(trial.TryRecordRelease(
                0, 0.5f, "Perfect", 0.5f, 0.5f, 0.05f));
            Assert.AreEqual(1, trial.ResolvedCount);
        }

        [Test]
        public void LateReleaseCannotOverwriteTimedOutResult()
        {
            TimedInputTrial trial = CreateTrial();
            trial.Expire(0.83f);

            Assert.IsFalse(trial.TryRecordRelease(
                0, 0.84f, "Perfect", 0.5f, 0.7f, 0.05f));
            Assert.AreEqual(1, trial.ResolvedCount);
        }

        [Test]
        public void TimelineDoesNotAcceptPressOutsideUnits()
        {
            TimedInputTrial trial = CreateTrial();

            Assert.AreEqual(-1, trial.ActiveIndexAt(-0.1f));
            Assert.AreEqual(-1, trial.ActiveIndexAt(1.4f));
            Assert.IsFalse(trial.IsResolved(0));
            Assert.IsFalse(trial.IsResolved(1));
        }
    }
}
