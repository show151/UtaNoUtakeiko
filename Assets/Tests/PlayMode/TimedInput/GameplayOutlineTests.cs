using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Uta.Tests
{
    public class GameplayOutlineTests
    {
        [UnityTest]
        public IEnumerator MovingBarGetsSolidOutlineEvenIfSpriteIsAssignedLater()
        {
            var go = new GameObject("Outline Test Bar");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;
            go.transform.localScale = new Vector3(1.5f, 0.12f, 1f);
            go.AddComponent<MovingBar>();
            var border = go.transform.Find("Moving Bar Outline");
            Assert.IsNotNull(border, "Every MovingBar should create an outline.");
            Assert.IsFalse(border.GetComponent<SpriteRenderer>().enabled);
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            renderer.sprite = sprite;
            yield return null;
            var outline = border.GetComponent<SpriteRenderer>();
            Assert.IsTrue(outline.enabled);
            Assert.AreEqual(Color.black, outline.color);
            Assert.Less(outline.sortingOrder, renderer.sortingOrder);
            Assert.Greater(border.localScale.y, 1.5f);
            renderer.enabled = false;
            yield return null;
            Assert.IsFalse(outline.enabled);
            Object.Destroy(go);
            Object.Destroy(sprite);
        }

        [UnityTest]
        public IEnumerator JudgmentUsesOneTextMeshWithBlackSdfOutline()
        {
            var go = new GameObject("Outline Test Notes");
            var display = go.AddComponent<NoteDisplay>();
            display.Initialize(new[] { 0.2f, 0.8f });
            display.ShowNoteJudgment(0, "Great", false, false);
            yield return null;
            var label = go.transform.Find("Note_1/NoteJudgment");
            Assert.IsNotNull(label);
            Assert.IsNull(label.Find("Judgment Outline"), "Overlapping text must not be created.");
            var text = label.GetComponent("TextMeshPro");
            Assert.IsNotNull(text);
            string value = (string)text.GetType().GetProperty("text").GetValue(text);
            StringAssert.Contains("Great", value);
            StringAssert.Contains("HOLD", value);
            var material = (Material)text.GetType().GetProperty("fontMaterial").GetValue(text);
            Assert.IsTrue(material.IsKeywordEnabled("OUTLINE_ON"));
            Assert.AreEqual(Color.black, material.GetColor("_OutlineColor"));
            Assert.Greater(material.GetFloat("_OutlineWidth"), 0f);
            display.SeekToNote(1);
            Assert.IsFalse(label.gameObject.activeInHierarchy);
            Object.Destroy(go);
        }
    }
}
