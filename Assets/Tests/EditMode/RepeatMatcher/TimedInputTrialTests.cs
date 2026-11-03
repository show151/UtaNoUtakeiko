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
        public void ReleaseAfterNextUnitStart_StillResolvesPressedHold()
        {
            TimedInputTrial trial = CreateTrial();

            Assert.AreEqual(0, trial.ActiveIndexAt(0.1f));
            Assert.AreEqual(1, trial.ActiveIndexAt(0.71f));

            bool accepted = trial.TryRecordRelease(
                0, 0.74f, "Perfect", 0.74f, 0.05f);

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
                0, 0.4f, "Great", 0.4f, 0.05f));

            Assert.AreEqual(false, results[0].HoldSucceeded);
            Assert.IsFalse(results[0].TimedOut);
            Assert.IsFalse(trial.TryRecordRelease(
                0, 0.5f, "Perfect", 0.5f, 0.05f));
            Assert.AreEqual(1, trial.ResolvedCount);
        }

        [Test]
        public void LateReleaseCannotOverwriteTimedOutResult()
        {
            TimedInputTrial trial = CreateTrial();
            trial.Expire(0.83f);

            Assert.IsFalse(trial.TryRecordRelease(
                0, 0.84f, "Perfect", 0.7f, 0.05f));
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
