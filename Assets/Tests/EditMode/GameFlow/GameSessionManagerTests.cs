
using NUnit.Framework;
using UnityEngine;
using Uta.GameFlow;

namespace Uta.Tests
{
    public class GameSessionManagerTests
    {
        private GameObject gameObject;
        private GameSessionManager manager;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("SessionTest");
            manager = gameObject.AddComponent<GameSessionManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void InitialState_IsTitle()
        {
            Assert.AreEqual(
                GameSessionState.Title,
                manager.CurrentState
            );
        }

        [Test]
        public void ValidTransition_Succeeds()
        {
            bool result = manager.TryTransition(
                GameSessionState.DifficultySelect
            );

            Assert.IsTrue(result);
            Assert.AreEqual(
                GameSessionState.DifficultySelect,
                manager.CurrentState
            );
        }

        [Test]
        public void InvalidTransition_IsRejected()
        {
            bool result = manager.TryTransition(
                GameSessionState.Playing
            );

            Assert.IsFalse(result);
            Assert.AreEqual(
                GameSessionState.Title,
                manager.CurrentState
            );
        }

        [Test]
        public void DuplicateTransition_IsRejected()
        {
            bool result = manager.TryTransition(
                GameSessionState.Title
            );

            Assert.IsFalse(result);
        }

        [Test]
        public void Recovery_ReturnsToTitle()
        {
            manager.TryTransition(
                GameSessionState.DifficultySelect
            );

            manager.RecoverToTitle();

            Assert.AreEqual(
                GameSessionState.Title,
                manager.CurrentState
            );
        }

        [Test]
        public void RecoveryDuringTransition_IsQueued()
        {
            manager.OnStateChanged += (previous, next) =>
            {
                if (next == GameSessionState.DifficultySelect)
                    manager.RecoverToTitle();
            };

            manager.TryTransition(
                GameSessionState.DifficultySelect
            );

            Assert.AreEqual(
                GameSessionState.Title,
                manager.CurrentState
            );
        }

        [Test]
        public void AfterRecovery_CanStartAgain()
        {
            manager.TryTransition(
                GameSessionState.DifficultySelect
            );

            manager.RecoverToTitle();

            bool result = manager.TryTransition(
                GameSessionState.DifficultySelect
            );

            Assert.IsTrue(result);
        }

        [Test]
        public void ReentrantTransition_IsRejected()
        {
            bool nestedResult = true;

            manager.OnStateChanged += (previous, next) =>
            {
                if (next == GameSessionState.DifficultySelect)
                {
                    nestedResult = manager.TryTransition(
                        GameSessionState.SongSelect
                    );
                }
            };

            bool result = manager.TryTransition(
                GameSessionState.DifficultySelect
            );

            Assert.IsTrue(result);
            Assert.IsFalse(nestedResult);
            Assert.AreEqual(
                GameSessionState.DifficultySelect,
                manager.CurrentState
            );
        }

        [Test]
        public void DuplicateCompletionTransition_IsRejected()
        {
            manager.TryTransition(GameSessionState.DifficultySelect);
            manager.TryTransition(GameSessionState.SongSelect);
            manager.TryTransition(GameSessionState.Countdown);
            manager.TryTransition(GameSessionState.Playing);

            bool first = manager.TryTransition(
                GameSessionState.Playback
            );

            bool second = manager.TryTransition(
                GameSessionState.Playback
            );

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.AreEqual(
                GameSessionState.Playback,
                manager.CurrentState
            );
        }

        [Test]
        public void StateChangedEvent_FiresOnce()
        {
            int eventCount = 0;

            manager.OnStateChanged += (previous, next) =>
            {
                eventCount++;
            };

            manager.TryTransition(GameSessionState.DifficultySelect);
            manager.TryTransition(GameSessionState.DifficultySelect);

            Assert.AreEqual(1, eventCount);
        }
    }
}
