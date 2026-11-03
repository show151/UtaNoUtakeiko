using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UtaNoUtakeiko.Data;
using UtaNoUtakeiko.Gameplay;

namespace Uta.Tests
{
    public class TimedInputPlayModeTests
    {
        private static TimedInputTrial MakeTrial()
        {
            return new TimedInputTrial(new List<InputUnit>
            {
                new InputUnit
                {
                    UnitId = "HOLD_1",
                    TargetPitchStep = 0,
                    IsHold = true,
                    StartSeconds = 0f,
                    EndSeconds = 0.1f
                },
                new InputUnit
                {
                    UnitId = "TAP_2",
                    TargetPitchStep = 2,
                    IsHold = false,
                    StartSeconds = 0.1f,
                    EndSeconds = 0.2f
                }
            });
        }

        [UnityTest]
        public IEnumerator NoInput_ExpiresAllUnits_ExactlyOnce()
        {
            var trial = MakeTrial();
            var results = new List<TimedInputTrialResult>();
            trial.OnResolved += results.Add;
            float started = Time.time;

            while (Time.time - started < 0.4f)
            {
                trial.Expire(Time.time - started);
                yield return null;
            }

            trial.Expire(0.4f);
            Assert.AreEqual(2, trial.ResolvedCount);
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("HOLD_1", results[0].UnitId);
            Assert.AreEqual("TAP_2", results[1].UnitId);
            Assert.IsTrue(results[0].TimedOut);
            Assert.IsTrue(results[1].TimedOut);
            Assert.AreEqual("Miss", results[0].PitchJudgment);
            Assert.AreEqual("Miss", results[1].PitchJudgment);

            trial.Expire(5f);
            Assert.AreEqual(2, results.Count, "Miss results must not duplicate.");
        }

        [UnityTest]
        public IEnumerator HeldPress_ResolvesOriginalUnit_NotNextUnit()
        {
            var trial = MakeTrial();
            var results = new List<TimedInputTrialResult>();
            trial.OnResolved += results.Add;

            Assert.AreEqual(0, trial.ActiveIndexAt(0.02f));
            Assert.AreEqual(1, trial.ActiveIndexAt(0.12f));

            // Release after the next unit's start, but within the old unit's grace.
            Assert.IsTrue(trial.TryRecordRelease(
                0, 0.16f, "Perfect", 0.4f, 0.12f, 0.05f));

            trial.Expire(0.34f);
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("HOLD_1", results[0].UnitId);
            Assert.IsFalse(results[0].TimedOut);
            Assert.AreEqual(true, results[0].HoldSucceeded);
            Assert.AreEqual("TAP_2", results[1].UnitId);
            Assert.IsTrue(results[1].TimedOut);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LateRelease_AfterAutoMiss_IsRejected()
        {
            var trial = MakeTrial();
            trial.Expire(0.23f);

            Assert.IsTrue(trial.IsResolved(0));
            Assert.IsFalse(trial.TryRecordRelease(
                0, 0.24f, "Great", 0.3f, 0.12f, 0.05f));
            Assert.AreEqual(1, trial.ResolvedCount);

            trial.Expire(0.33f);
            Assert.AreEqual(2, trial.ResolvedCount);
            yield return null;
        }
    }
}
