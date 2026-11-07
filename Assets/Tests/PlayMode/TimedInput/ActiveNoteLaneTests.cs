using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uta.GameFlow;

namespace Uta.Tests
{
    public class ActiveNoteLaneTests
    {
        [UnityTest]
        public IEnumerator LaneStaysAtInputPositionAndOnlyAppearsWhilePlaying()
        {
            var cameraObject = new GameObject("Lane Test Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.GetComponent<Camera>().orthographic = true;
            var notesObject = new GameObject("Lane Test Notes");
            notesObject.transform.position = new Vector3(4f, 0f, 0f);
            var notes = notesObject.AddComponent<NoteDisplay>();
            notes.Initialize(new[] { 0.2f, 0.8f });
            var sessionObject = new GameObject("Lane Test Session");
            sessionObject.SetActive(false);
            var manager = sessionObject.AddComponent<GameSessionManager>();
            var component = sessionObject.AddComponent<ActiveNoteLane>();
            typeof(ActiveNoteLane).GetField("sessionManager", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(component, manager);
            typeof(ActiveNoteLane).GetField("noteDisplay", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(component, notes);
            sessionObject.SetActive(true);
            var lane = sessionObject.transform.Find("Active Note Lane");
            Assert.IsNotNull(lane);
            Assert.IsFalse(lane.gameObject.activeSelf);
            Assert.IsTrue(manager.TryTransition(GameSessionState.DifficultySelect));
            Assert.IsTrue(manager.TryTransition(GameSessionState.Countdown));
            Assert.IsTrue(manager.TryTransition(GameSessionState.Playing));
            yield return null;
            Assert.IsTrue(lane.gameObject.activeSelf);
            Assert.AreEqual(notes.InputLaneWorldX, lane.position.x, 0.001f);
            var x = lane.position.x;
            var renderer = lane.GetComponent<SpriteRenderer>();
            Assert.AreEqual(1, renderer.sortingOrder);
            Assert.AreEqual(0.28f, renderer.color.a, 0.001f);
            notes.SeekToNote(1);
            yield return null;
            Assert.AreEqual(x, lane.position.x, 0.001f);
            Assert.IsTrue(manager.TryTransition(GameSessionState.GameFinished));
            Assert.IsFalse(lane.gameObject.activeSelf);
            Object.Destroy(sessionObject);
            Object.Destroy(notesObject);
            Object.Destroy(cameraObject);
        }
    }
}
