using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uta.GameFlow;

namespace Uta.Tests
{
    public class CountdownResetTests
    {
        private static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }

        [UnityTest]
        public IEnumerator EnteringCountdownClearsOldNotesAndResetsMovingBar()
        {
            var notesObject = new GameObject("Old Notes");
            var notes = notesObject.AddComponent<NoteDisplay>();
            notes.Initialize(new[] { 0.2f, 0.8f });
            notes.ShowNoteJudgment(0, "Great", null, false);
            var oldNote = notesObject.transform.Find("Note_1").gameObject;
            notes.SeekToNote(1);

            var barObject = new GameObject("Moving Bar");
            barObject.transform.position = new Vector3(-5.31f, 1, 0);
            var bar = barObject.AddComponent<MovingBar>();
            barObject.transform.position = new Vector3(-5.31f, -2, 0);
            var judgmentObject = new GameObject("Judgment");
            judgmentObject.SetActive(false);
            var judgment = judgmentObject.AddComponent<JudgmentSystem>();
            Set(judgment, "movingBar", bar);
            judgmentObject.SetActive(true);

            var progressionObject = new GameObject("Progression");
            progressionObject.SetActive(false);
            var progression = progressionObject.AddComponent<NoteProgression>();
            Set(progression, "noteDisplay", notes);
            Set(progression, "judgmentSystem", judgment);
            progressionObject.SetActive(true);

            var sessionObject = new GameObject("Session");
            sessionObject.SetActive(false);
            var manager = sessionObject.AddComponent<GameSessionManager>();
            var connector = sessionObject.AddComponent<GameSessionPlayConnector>();
            Set(connector, "sessionManager", manager);
            Set(connector, "noteProgression", progression);
            sessionObject.SetActive(true);
            Assert.IsTrue(manager.TryTransition(GameSessionState.DifficultySelect));
            Assert.IsTrue(manager.TryTransition(GameSessionState.Countdown));
            Assert.IsFalse(oldNote.activeSelf);
            Assert.AreEqual(1f, barObject.transform.position.y, 0.001f);
            Assert.AreEqual(-5.31f, barObject.transform.position.x, 0.001f);
            Assert.AreEqual(0f, notesObject.transform.position.x, 0.001f);
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(1f, barObject.transform.position.y, 0.001f, "Bar stays at its start during countdown.");
            judgment.ResetJudgment(0.5f);
            yield return new WaitForSeconds(0.1f);
            Assert.Greater(barObject.transform.position.y, 1f, "Bar resumes when gameplay starts.");
            Object.Destroy(sessionObject);
            Object.Destroy(progressionObject);
            Object.Destroy(judgmentObject);
            Object.Destroy(barObject);
            Object.Destroy(notesObject);
        }
    }
}
