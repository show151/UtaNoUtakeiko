using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uta.GameFlow;

namespace Uta.Tests
{
    public class AnnouncementReturnTests
    {
        private static GameSessionManager CreateAnnouncement(out GameObject obj)
        {
            obj = new GameObject("AnnouncementReturnTest");
            obj.SetActive(false);
            var manager = obj.AddComponent<GameSessionManager>();
            foreach (var state in new[] { GameSessionState.DifficultySelect, GameSessionState.Countdown,
                GameSessionState.Playing, GameSessionState.GameFinished, GameSessionState.Playback,
                GameSessionState.Result, GameSessionState.Ranking, GameSessionState.Announcement })
                Assert.IsTrue(manager.TryTransition(state));
            var presenter = obj.AddComponent<ScreenArtworkPresenter>();
            typeof(ScreenArtworkPresenter).GetField("announcementReturnSeconds", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(presenter, 0.2f);
            obj.SetActive(true);
            return manager;
        }

        [UnityTest]
        public IEnumerator Announcement_ReturnsToTitleWithoutInputEvenWhenPaused()
        {
            var manager = CreateAnnouncement(out var obj);
            float originalScale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                Assert.AreEqual(GameSessionState.Announcement, manager.CurrentState);
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.AreEqual(GameSessionState.Title, manager.CurrentState);
            }
            finally { Time.timeScale = originalScale; Object.Destroy(obj); }
        }

        [UnityTest]
        public IEnumerator ManualReturn_CancelsPendingAutomaticReturn()
        {
            var manager = CreateAnnouncement(out var obj);
            try
            {
                yield return null;
                var presenter = obj.GetComponent<ScreenArtworkPresenter>();
                typeof(ScreenArtworkPresenter).GetMethod("Confirm", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(presenter, null);
                Assert.AreEqual(GameSessionState.Title, manager.CurrentState);
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.AreEqual(GameSessionState.Title, manager.CurrentState);
            }
            finally { Object.Destroy(obj); }
        }
    }
}
