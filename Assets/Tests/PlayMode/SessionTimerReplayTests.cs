using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uta.GameFlow;

namespace Uta.Tests
{
    public class SessionTimerReplayTests
    {
        [UnityTest]
        public IEnumerator DifficultySetsTimerAndResetsForNextPlay()
        {
            var obj = new GameObject("Difficulty Timer Test");
            var manager = obj.AddComponent<GameSessionManager>();
            var timer = obj.AddComponent<GameSessionTimeout>();
            timer.SetSessionManager(manager);
            timer.SetTestTimeLimit(60f);
            yield return null;
            foreach (bool hard in new[] { true, false, true })
            {
                Assert.IsTrue(manager.TryTransition(GameSessionState.DifficultySelect));
                manager.SetHardDifficulty(hard);
                Assert.IsTrue(manager.TryTransition(GameSessionState.Countdown));
                Assert.IsTrue(manager.TryTransition(GameSessionState.Playing));
                Assert.AreEqual(hard ? 100f : 60f, timer.RemainingTime, 0.01f);
                Assert.AreEqual(hard ? 100f : 60f, manager.InitialTimeLimit.Value, 0.01f);
                Assert.IsTrue(manager.TryTransition(GameSessionState.GameFinished));
                manager.RecoverToTitle();
            }
            Object.Destroy(obj);
        }

        [UnityTest]
        public IEnumerator RemainingTime_TracksTimerAfterEarlyCompletionAndReplay()
        {
            var obj = new GameObject("ReplayTimerTest");
            var manager = obj.AddComponent<GameSessionManager>();
            var timer = obj.AddComponent<GameSessionTimeout>();
            timer.SetSessionManager(manager);
            timer.SetTestTimeLimit(5f);
            yield return null;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.IsTrue(manager.TryTransition(GameSessionState.DifficultySelect));
                Assert.IsTrue(manager.TryTransition(GameSessionState.Countdown));
                Assert.IsTrue(manager.TryTransition(GameSessionState.Playing));
                Assert.IsTrue(manager.RemainingTime.HasValue);
                float initial = manager.RemainingTime.Value;
                yield return new WaitForSeconds(0.15f);
                Assert.Less(manager.RemainingTime.Value, initial);
                Assert.Greater(manager.RemainingTime.Value, 0f);
                Assert.IsTrue(manager.TryTransition(GameSessionState.GameFinished));
                manager.RecoverToTitle();
            }

            Object.Destroy(obj);
        }
    }
}
