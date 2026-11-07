using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uta.GameFlow;

namespace Uta.Tests
{
    public class ClimaxAudioTests
    {
        private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        private static bool TryStart(ScreenArtworkPresenter presenter, float? remaining, float? duration) =>
            (bool)typeof(ScreenArtworkPresenter).GetMethod("TryStartClimax", Flags).Invoke(presenter, new object[] { remaining, duration });

        [UnityTest]
        public IEnumerator StartsAtThirtyPercentOnceAndResetsForReplay()
        {
            var go = new GameObject("Climax Audio Test");
            go.SetActive(false);
            var presenter = go.AddComponent<ScreenArtworkPresenter>();
            var bgm = go.AddComponent<AudioSource>();
            var effect = go.AddComponent<AudioSource>();
            var normal = Resources.Load<AudioClip>("GameAudio/play");
            var climax = Resources.Load<AudioClip>("GameAudio/play_clymax");
            var whistle = Resources.Load<AudioClip>("GameAudio/whistle");
            Assert.IsNotNull(normal);
            Assert.IsNotNull(climax);
            Assert.IsNotNull(whistle);
            Set(presenter, "bgmSource", bgm);
            Set(presenter, "effectSource", effect);
            Set(presenter, "playBgm", normal);
            Set(presenter, "warningBgm", climax);
            Set(presenter, "whistleSound", whistle);
            var reset = typeof(ScreenArtworkPresenter).GetMethod("ApplyAudioState", Flags);
            go.SetActive(true);

            reset.Invoke(presenter, new object[] { GameSessionState.Playing });
            Assert.AreSame(normal, bgm.clip);
            Assert.IsFalse(TryStart(presenter, 18.01f, 60f));
            Assert.IsTrue(TryStart(presenter, 18f, 60f));
            Assert.AreSame(climax, bgm.clip);
            Assert.IsTrue(effect.isPlaying, "The whistle should play when the BGM changes.");
            Assert.IsFalse(TryStart(presenter, 17f, 60f));
            Assert.IsFalse(TryStart(presenter, 0f, 60f));
            reset.Invoke(presenter, new object[] { GameSessionState.Playing });
            Assert.AreSame(normal, bgm.clip);
            Assert.IsFalse(TryStart(presenter, 30.01f, 100f));
            Assert.IsTrue(TryStart(presenter, 30f, 100f));
            Assert.AreSame(climax, bgm.clip);
            Assert.IsFalse(TryStart(presenter, 29f, 100f));
            Object.Destroy(go);
            yield return null;
        }
    }
}
