using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UtaNoUtakeiko.Data;

namespace UtaNoUtakeiko.Tests
{
    public class TimedInputVisualTests
    {
        [Test]
        public void InitializeInputUnits_RendersHoldBarOnlyForGroupedHold()
        {
            var go = new GameObject("NoteDisplay_Test");
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(
                texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
            try
            {
                var display = go.AddComponent<NoteDisplay>();
                typeof(NoteDisplay)
                    .GetField("noteSprite", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(display, sprite);

                display.InitializeInputUnits(
                    new[] { 0.2f, 0.8f },
                    new List<InputUnit>
                    {
                        new InputUnit
                        {
                            UnitId = "H1", IsHold = true, TargetPitchStep = 2,
                            StartSeconds = 0f, EndSeconds = 0.6f
                        },
                        new InputUnit
                        {
                            UnitId = "T2", IsHold = false, TargetPitchStep = 8,
                            StartSeconds = 0.6f, EndSeconds = 0.9f
                        }
                    });

                var shaft = go.transform.Find("Note_1/NoteHead/HoldBody");
                var cap = go.transform.Find("Note_1/NoteHead/HoldEndCap");
                var fill = go.transform.Find("Note_1/NoteHead/HoldProgress");
                Assert.IsNotNull(shaft);
                Assert.IsNotNull(cap);
                Assert.IsNotNull(fill);
                Assert.IsNull(go.transform.Find("Note_2/NoteHead/HoldProgress"));
                Assert.IsNull(go.transform.Find("Note_2/NoteHead/HoldBody"));
                Assert.IsNull(go.transform.Find("Note_2/NoteHead/HoldEndCap"));

                // The shaft is a solid square, not a stretched copy of the
                // round note sprite, and it is visibly longer than tall.
                var shaftRenderer = shaft.GetComponent<SpriteRenderer>();
                Assert.AreNotSame(sprite, shaftRenderer.sprite);
                Assert.AreEqual(new Vector3(0.35f, 0.35f, 1f), go.transform.Find("Note_1").localScale);
                Assert.AreEqual(Vector3.one, go.transform.Find("Note_1/NoteHead").localScale);
                Assert.AreEqual(Vector3.one, cap.localScale);
                Assert.AreEqual(new Vector3(3f, 1f, 1f), shaft.localScale);
                Assert.AreEqual(shaft.localPosition.x * 2f, cap.localPosition.x, 0.001f);
                Assert.Greater(go.transform.Find("Note_2").localPosition.x,
                    go.transform.Find("Note_1").localPosition.x + cap.localPosition.x * 0.35f);

                var headSprite = go.transform.Find("Note_1/NoteHead").GetComponent<SpriteRenderer>().sprite;
                var capSprite = cap.GetComponent<SpriteRenderer>().sprite;
                Assert.AreNotSame(sprite, headSprite);
                Assert.AreNotSame(sprite, capSprite);
                foreach (Vector2 vertex in headSprite.vertices)
                    Assert.LessOrEqual(vertex.x, 0.001f);
                foreach (Vector2 vertex in capSprite.vertices)
                    Assert.GreaterOrEqual(vertex.x, -0.001f);
                Assert.AreSame(sprite, go.transform.Find("Note_2/NoteHead").GetComponent<SpriteRenderer>().sprite);

                var fillRenderer = fill.GetComponent<SpriteRenderer>();
                Assert.IsFalse(fillRenderer.enabled);
                display.ShowHoldProgress(0, 0.5f);
                var headRenderer = go.transform.Find("Note_1/NoteHead").GetComponent<SpriteRenderer>();
                Assert.Greater(headRenderer.color.g, 0.9f, "The first circle lights while held.");
                Assert.Less(cap.GetComponent<SpriteRenderer>().color.g, 0.9f, "The far circle waits for the fill.");
                Assert.IsTrue(fillRenderer.enabled);
                Assert.AreEqual(shaft.localScale.x * 0.5f, fill.localScale.x, 0.001f);
                Assert.AreEqual(fill.localScale.x * 0.5f, fill.localPosition.x, 0.001f);
                display.ResetInputProgress(0);
                Assert.IsFalse(fillRenderer.enabled);
                Assert.AreEqual(0f, fill.localScale.x, 0.001f);
                display.ShowInputResolution(0, true);
                Assert.AreEqual(shaft.localScale.x, fill.localScale.x, 0.001f);
                Assert.Greater(cap.GetComponent<SpriteRenderer>().color.g, 0.9f);

                // Skipping past a HOLD must hide its note and every child bar.
                float firstTargetX = go.transform.Find("Note_1").position.x;
                display.SeekToNote(1);
                Assert.AreEqual(firstTargetX, go.transform.Find("Note_2").position.x, 0.001f);
                Assert.IsFalse(go.transform.Find("Note_1/NoteHead")
                    .GetComponent<SpriteRenderer>().enabled);
                Assert.IsFalse(go.transform.Find("Note_1/NoteHead/HoldBody")
                    .GetComponent<SpriteRenderer>().enabled);
                Assert.IsFalse(go.transform.Find("Note_1/NoteHead/HoldEndCap")
                    .GetComponent<SpriteRenderer>().enabled);
                Assert.IsFalse(fillRenderer.enabled);

                display.ShowHoldProgress(0, 1f);
                Color completedColor = go.transform.Find("Note_1/NoteHead")
                    .GetComponent<SpriteRenderer>().color;
                Assert.Greater(completedColor.g, completedColor.r);

                display.ShowInputResolution(1, false);
                Color missedColor = go.transform.Find("Note_2/NoteHead")
                    .GetComponent<SpriteRenderer>().color;
                Assert.AreEqual(missedColor.r, missedColor.g, 0.05f);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
