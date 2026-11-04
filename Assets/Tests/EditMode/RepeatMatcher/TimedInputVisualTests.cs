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
                texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f));
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

                var shaft = go.transform.Find("Note_1/HoldBody");
                var cap = go.transform.Find("Note_1/HoldEndCap");
                Assert.IsNotNull(shaft);
                Assert.IsNotNull(cap);
                Assert.IsNull(go.transform.Find("Note_2/HoldBody"));
                Assert.IsNull(go.transform.Find("Note_2/HoldEndCap"));

                // The shaft is a solid square, not a stretched copy of the
                // round note sprite, and it is visibly longer than tall.
                var shaftRenderer = shaft.GetComponent<SpriteRenderer>();
                Assert.AreNotSame(sprite, shaftRenderer.sprite);
                Assert.Greater(shaft.lossyScale.x, shaft.lossyScale.y * 3f);

                // Skipping past a HOLD must hide BOTH its note and child bar.
                display.SeekToNote(1);
                Assert.IsFalse(go.transform.Find("Note_1")
                    .GetComponent<SpriteRenderer>().enabled);
                Assert.IsFalse(go.transform.Find("Note_1/HoldBody")
                    .GetComponent<SpriteRenderer>().enabled);
                Assert.IsFalse(go.transform.Find("Note_1/HoldEndCap")
                    .GetComponent<SpriteRenderer>().enabled);

                display.ShowHoldProgress(0, 1f);
                Color completedColor = go.transform.Find("Note_1")
                    .GetComponent<SpriteRenderer>().color;
                Assert.Greater(completedColor.g, completedColor.r);

                display.ShowInputResolution(1, false);
                Color missedColor = go.transform.Find("Note_2")
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
