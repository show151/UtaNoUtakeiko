using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Uta.GameFlow;
using UtaNoUtakeiko.Gameplay;

namespace Uta.Tests
{
    public class SingingPresentationTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void JudgmentSelectsCorrectSingingExpression()
        {
            var obj = new GameObject("presenter");
            obj.SetActive(false);
            var presenter = obj.AddComponent<ScreenArtworkPresenter>();
            var texture = new Texture2D(1, 1);
            var happy = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            var normal = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            var worried = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            var type = typeof(ScreenArtworkPresenter);
            type.GetField("singingGoodCharacter", Private).SetValue(presenter, happy);
            type.GetField("singingNormalCharacter", Private).SetValue(presenter, normal);
            type.GetField("singingBadCharacter", Private).SetValue(presenter, worried);
            var method = type.GetMethod("SingingCharacterFor", Private);
            Assert.AreSame(happy, method.Invoke(presenter, new object[] { JudgmentType.Perfect }));
            Assert.AreSame(normal, method.Invoke(presenter, new object[] { JudgmentType.Great }));
            Assert.AreSame(normal, method.Invoke(presenter, new object[] { JudgmentType.Good }));
            Assert.AreSame(normal, method.Invoke(presenter, new object[] { null }));
            Assert.AreSame(worried, method.Invoke(presenter, new object[] { JudgmentType.Bad }));
            Assert.AreSame(worried, method.Invoke(presenter, new object[] { JudgmentType.Miss }));
            Object.DestroyImmediate(obj);
            Object.DestroyImmediate(happy);
            Object.DestroyImmediate(normal);
            Object.DestroyImmediate(worried);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void PitchDifferenceIncludesDirectionAndSemitone()
        {
            var method = typeof(ScreenArtworkPresenter).GetMethod("PitchDifferenceLabel", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.AreEqual("音程ぴったり", method.Invoke(null, new object[] { 0 }));
            Assert.AreEqual("音程差 1半音 高い ↑", method.Invoke(null, new object[] { 1 }));
            Assert.AreEqual("音程差 1半音 低い ↓", method.Invoke(null, new object[] { -1 }));
        }
    }
}
