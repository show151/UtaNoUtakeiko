
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uta.GameFlow;

namespace Uta.Tests
{
    public class GameSessionTimeoutTests
    {
        [UnityTest]
        public IEnumerator Session_CanStartAndRecover_InPlayMode()
        {
            GameObject obj = new GameObject("SessionTest");
            GameSessionManager manager =
                obj.AddComponent<GameSessionManager>();

            yield return null;

            Assert.AreEqual(
                GameSessionState.Title,
                manager.CurrentState
            );

            Assert.IsTrue(manager.TryTransition(
                GameSessionState.DifficultySelect
            ));

            manager.RecoverToTitle();

            Assert.AreEqual(
                GameSessionState.Title,
                manager.CurrentState
            );

            Object.Destroy(obj);
        }

        [UnityTest]
        public IEnumerator Timeout_ReturnsToTitle()
        {
            GameObject obj = new GameObject("TimeoutTest");
            GameSessionManager manager =
                obj.AddComponent<GameSessionManager>();

            GameSessionTimeout timeout =
                obj.AddComponent<GameSessionTimeout>();

            // SerializeFieldへの参照は後でセットするため、
            // 今回はテスト前に専用メソッドを用意する。
            timeout.SetSessionManager(manager);
            timeout.SetTestTimeLimit(0.1f);

            yield return null;

            manager.TryTransition(GameSessionState.DifficultySelect);
            manager.TryTransition(GameSessionState.SongSelect);
            manager.TryTransition(GameSessionState.Countdown);
            manager.TryTransition(GameSessionState.Playing);

            yield return new WaitForSeconds(0.25f);

            Assert.AreEqual(GameSessionState.Title, manager.CurrentState);

            Object.Destroy(obj);
        }

        [UnityTest]
        public IEnumerator Timeout_DoesNotRunOutsidePlaying()
        {
            GameObject obj = new GameObject("TimeoutTest");
            GameSessionManager manager =
                obj.AddComponent<GameSessionManager>();

            GameSessionTimeout timeout =
                obj.AddComponent<GameSessionTimeout>();

            timeout.SetSessionManager(manager);
            timeout.SetTestTimeLimit(0.1f);

            yield return null;

            manager.TryTransition(GameSessionState.DifficultySelect);
            manager.TryTransition(GameSessionState.SongSelect);

            // Playingに入らず、制限時間以上待つ
            yield return new WaitForSeconds(0.25f);

            Assert.AreEqual(
                GameSessionState.SongSelect,
                manager.CurrentState
            );

            Object.Destroy(obj);
        }

        [UnityTest]
        public IEnumerator AfterTimeout_CanStartNewSession()
        {
            GameObject obj = new GameObject("TimeoutTest");
            GameSessionManager manager =
                obj.AddComponent<GameSessionManager>();

            GameSessionTimeout timeout =
                obj.AddComponent<GameSessionTimeout>();

            timeout.SetSessionManager(manager);
            timeout.SetTestTimeLimit(0.1f);

            yield return null;

            manager.TryTransition(GameSessionState.DifficultySelect);
            manager.TryTransition(GameSessionState.SongSelect);
            manager.TryTransition(GameSessionState.Countdown);
            manager.TryTransition(GameSessionState.Playing);

            yield return new WaitForSeconds(0.25f);

            Assert.AreEqual(
                GameSessionState.Title,
                manager.CurrentState
            );

            bool restarted = manager.TryTransition(
                GameSessionState.DifficultySelect
            );

            Assert.IsTrue(restarted);
            Assert.AreEqual(
                GameSessionState.DifficultySelect,
                manager.CurrentState
            );

            Object.Destroy(obj);
        }
    }
}
