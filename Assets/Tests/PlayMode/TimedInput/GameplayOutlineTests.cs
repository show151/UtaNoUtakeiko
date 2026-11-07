using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Uta.Tests
{
    public class GameplayOutlineTests
    {
        [UnityTest]
        public IEnumerator MovingBarOutlineFollowsAndSurroundsTheBar()
        {
            var go = new GameObject("Outline Test Bar");
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 10;
            go.AddComponent<MovingBarOutline>();
            yield return null;
            var border = go.transform.Find("Moving Bar Outline");
            Assert.IsNotNull(border);
            Assert.AreEqual(Color.black, border.GetComponent<SpriteRenderer>().color);
            Assert.Less(border.GetComponent<SpriteRenderer>().sortingOrder, renderer.sortingOrder);
            Assert.Greater(border.localScale.y, 1f);
            Object.Destroy(go);
            Object.Destroy(sprite);
        }

        [UnityTest]
        public IEnumerator JudgmentOutlineShowsPlainBlackTextAndFollowsItsLabel()
        {
            var go = new GameObject("Outline Test Notes");
            var display = go.AddComponent<NoteDisplay>();
            display.Initialize(new[] { 0.2f, 0.8f });
            display.ShowNoteJudgment(0, "Great", false, false);
            yield return null;
            var label = go.transform.Find("Note_1/NoteJudgment");
            Assert.IsNotNull(label);
            Assert.AreEqual(8, label.childCount);
            foreach (Transform child in label)
            {
                var text = child.GetComponent("TextMeshPro");
                Assert.IsNotNull(text);
                string value = (string)text.GetType().GetProperty("text").GetValue(text);
                StringAssert.Contains("Great", value);
                StringAssert.DoesNotContain("<color", value);
                Assert.Less(child.GetComponent<Renderer>().sortingOrder, label.GetComponent<Renderer>().sortingOrder);
            }
            display.SeekToNote(1);
            Assert.IsFalse(label.gameObject.activeInHierarchy);
            Object.Destroy(go);
        }
    }
}
