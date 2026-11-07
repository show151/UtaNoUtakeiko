using System.Collections;
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
        public void Judgment_StaysWithTheNoteAndHidesAfterAdvancing()
        {
            var go = new GameObject("NoteJudgment_Test");
            try
            {
                var display = go.AddComponent<NoteDisplay>();
                display.Initialize(new[] { 0.2f, 0.8f });
                display.ShowNoteJudgment(0, "Great", false, false);
                var label = go.transform.Find("Note_1/NoteJudgment");
                Assert.IsNotNull(label);
                var text = label.GetComponent("TextMeshPro");
                Assert.IsNotNull(text);
                string content = (string)text.GetType().GetProperty("text").GetValue(text);
                StringAssert.Contains("<color=#72EEFF>Great</color>", content);
                StringAssert.Contains("HOLD <color=#FF90A1>Miss</color>", content);
                Assert.Greater(label.position.y, go.transform.Find("Note_1").position.y);
                display.SeekToNote(1);
                Assert.IsFalse(label.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void StairNotes_AreConnectedAndLinksDisappearWhenPassed()
        {
            var go = new GameObject("StairVisual_Test");
            try
            {
                var display = go.AddComponent<NoteDisplay>();
                display.InitializeInputUnits(new[] { 0.8f, 0.5f, 0.2f, 0.4f },
                    new List<InputUnit> {
                        new InputUnit { StairGroupId = "A" },
                        new InputUnit { StairGroupId = "A" },
                        new InputUnit { StairGroupId = "A" },
                        new InputUnit() });
                var horizontal = go.transform.Find("Note_1/StairHorizontal");
                var vertical = go.transform.Find("Note_1/StairVertical");
                Assert.IsNotNull(horizontal);
                Assert.IsNotNull(vertical);
                var tapColor = go.transform.Find("Note_4/NoteHead").GetComponent<SpriteRenderer>().color;
                Assert.AreEqual(tapColor, go.transform.Find("Note_1/NoteHead").GetComponent<SpriteRenderer>().color);
                Assert.AreEqual(tapColor, horizontal.GetComponent<SpriteRenderer>().color);
                Assert.AreEqual(tapColor, vertical.GetComponent<SpriteRenderer>().color);
                Assert.IsNull(go.transform.Find("Note_3/StairHorizontal"));
                Assert.IsNull(go.transform.Find("Note_4/StairHorizontal"));
                display.SeekToNote(1);
                Assert.IsFalse(horizontal.GetComponent<SpriteRenderer>().enabled);
                Assert.IsFalse(vertical.GetComponent<SpriteRenderer>().enabled);
                Assert.IsTrue(go.transform.Find("Note_2/StairHorizontal").GetComponent<SpriteRenderer>().enabled);
            }
            finally { Object.DestroyImmediate(go); }
        }

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
                Assert.Greater(shaftRenderer.sortingOrder, headRenderer.sortingOrder);
                Assert.Greater(shaftRenderer.sortingOrder, cap.GetComponent<SpriteRenderer>().sortingOrder);
                Assert.Greater(fillRenderer.sortingOrder, shaftRenderer.sortingOrder);
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
                Assert.AreEqual(headRenderer.color, shaftRenderer.color);
                Assert.AreEqual(headRenderer.color, fillRenderer.color);
                Assert.AreEqual(headRenderer.color, cap.GetComponent<SpriteRenderer>().color);

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

        [TestCase(false)]
        [TestCase(true)]
        public void CompletedHold_ScrollsWhileHeld_AndRearmsOnlyAfterRelease(bool releaseDuringSlide)
        {
            var go = new GameObject("Advance_Test");
            var textGo = new GameObject("JudgmentText_Test", typeof(RectTransform));
            textGo.SetActive(false);
            try
            {
                var display = go.AddComponent<NoteDisplay>();
                var input = go.AddComponent<InputManager>();
                var judgment = go.AddComponent<JudgmentSystem>();
                var ui = go.AddComponent<JudgmentUI>();
                var ticks = go.AddComponent<SliderTicks>();
                var progression = go.AddComponent<NoteProgression>();
                var positions = new[] { 0.2f, 0.8f };
                display.Initialize(positions);
                SetPrivate(display, "scrollDuration", 0f);
                SetPrivate(input, "isTestMode", false);
                SetPrivate(input, "isPressed", true);
                SetPrivate(judgment, "inputManager", input);

                var textField = typeof(JudgmentUI).GetField(
                    "judgmentText", BindingFlags.Instance | BindingFlags.NonPublic);
                var textType = textField.FieldType.Assembly.GetType("TMPro.TextMeshProUGUI");
                Assert.IsNotNull(textType);
                textField.SetValue(ui, textGo.AddComponent(textType));
                SetPrivate(ui, "sliderTicks", ticks);

                SetPrivate(progression, "noteDisplay", display);
                SetPrivate(progression, "judgmentSystem", judgment);
                SetPrivate(progression, "judgmentUI", ui);
                SetPrivate(progression, "notePositions", positions);
                SetPrivate(progression, "currentGameplayUnits", new List<InputUnit>
                {
                    new InputUnit { UnitId = "H1", IsHold = true },
                    new InputUnit { UnitId = "T2", IsHold = false }
                });
                SetPrivate(progression, "isGameplayInput", true);
                SetPrivate(progression, "isAdvancing", true);
                SetPrivate(progression, "holdScrollDuration", 0f);

                var method = typeof(NoteProgression).GetMethod(
                    "AdvanceGameplay", BindingFlags.Instance | BindingFlags.NonPublic);
                var advance = (IEnumerator)method.Invoke(progression, new object[] { 1, true });

                Assert.IsTrue(advance.MoveNext(), "Hold success remains visible briefly.");
                Assert.IsInstanceOf<WaitForSecondsRealtime>(advance.Current);
                Assert.IsTrue((bool)GetPrivate(progression, "isAdvancing"));
                Assert.IsTrue(advance.MoveNext(), "Scroll starts before release.");
                var slide = advance.Current as IEnumerator;
                Assert.IsNotNull(slide);
                float before = display.transform.position.x;
                if (releaseDuringSlide)
                    SetPrivate(input, "isPressed", false);
                Assert.IsFalse(slide.MoveNext());
                Assert.Less(display.transform.position.x, before);

                if (!releaseDuringSlide)
                {
                    Assert.IsTrue(advance.MoveNext(), "Still held after scroll: keep waiting.");
                    Assert.IsNull(advance.Current);
                    Assert.IsTrue((bool)GetPrivate(progression, "isAdvancing"));
                    Assert.AreEqual(0, GetPrivate(progression, "currentNoteIndex"));
                    SetPrivate(input, "isPressed", false);
                }

                Assert.IsFalse(advance.MoveNext());
                Assert.AreEqual(1, GetPrivate(progression, "currentNoteIndex"));
                Assert.IsFalse((bool)GetPrivate(progression, "isAdvancing"));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(textGo);
            }
        }

        private static void SetPrivate(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static object GetPrivate(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }
    }
}
