using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

// Render an opaque black border even when the active font material has no SDF outline.
[RequireComponent(typeof(TextMeshPro))]
public sealed class NoteJudgmentOutline : MonoBehaviour
{
    private TextMeshPro source;
    private readonly TextMeshPro[] borders = new TextMeshPro[8];
    private string previousText;

    private void Awake()
    {
        source = GetComponent<TextMeshPro>();
        int index = 0;
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            if (x == 0 && y == 0) continue;
            var go = new GameObject("Judgment Outline", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x * 0.045f, y * 0.045f, 0f);
            var text = go.AddComponent<TextMeshPro>();
            text.font = source.font;
            text.fontSize = source.fontSize;
            text.alignment = source.alignment;
            text.rectTransform.sizeDelta = source.rectTransform.sizeDelta;
            text.color = Color.black;
            text.richText = false;
            text.GetComponent<MeshRenderer>().sortingLayerID = source.GetComponent<MeshRenderer>().sortingLayerID;
            text.GetComponent<MeshRenderer>().sortingOrder = source.GetComponent<MeshRenderer>().sortingOrder - 1;
            borders[index++] = text;
        }
    }

    private void LateUpdate()
    {
        if (source.text == previousText) return;
        previousText = source.text;
        string plain = Regex.Replace(previousText ?? "", "</?color(?:=[^>]*)?>", "", RegexOptions.IgnoreCase);
        foreach (var border in borders) border.text = plain;
    }
}
