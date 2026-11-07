using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uta.GameFlow;

namespace Uta.Tests
{
    public class DifficultyBarSpeedTests
    {
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [UnityTest]
        public IEnumerator SelectingHardIncreasesSpeedAndEasyRestoresIt()
        {
            var barObject = new GameObject("Test Moving Bar");
            var bar = barObject.AddComponent<MovingBar>();
            bar.StopMovement();
            var judgmentObject = new GameObject("Test Judgment");
            judgmentObject.SetActive(false);
            var judgment = judgmentObject.AddComponent<JudgmentSystem>();
            Set(judgment, "movingBar", bar);
            judgmentObject.SetActive(true);
            var progressObject = new GameObject("Test Progression");
            progressObject.SetActive(false);
            var progress = progressObject.AddComponent<NoteProgression>();
            Set(progress, "judgmentSystem", judgment);
            progressObject.SetActive(true);
            var connectorObject = new GameObject("Test Connector");
            connectorObject.SetActive(false);
            var connector = connectorObject.AddComponent<GameSessionPlayConnector>();
            Set(connector, "noteProgression", progress);
            connectorObject.SetActive(true);
            var field = typeof(MovingBar).GetField("difficultySpeedMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
            connector.SelectDifficulty(2);
            Assert.AreEqual(1.15f, (float)field.GetValue(bar), 0.001f);
            connector.SelectDifficulty(0);
            Assert.AreEqual(1f, (float)field.GetValue(bar), 0.001f);
            Object.Destroy(connectorObject);
            Object.Destroy(progressObject);
            Object.Destroy(judgmentObject);
            Object.Destroy(barObject);
            yield return null;
        }
    }
}
