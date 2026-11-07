using TMPro;
using UnityEngine;

// Use the font's SDF shader rather than overlapping copies of the text.
[RequireComponent(typeof(TextMeshPro))]
public sealed class NoteJudgmentOutline : MonoBehaviour
{
    private void Awake()
    {
        var label = GetComponent<TextMeshPro>();
        // Keep the mobile shader's outline variant in player builds.
        Resources.Load<Material>("Fonts & Materials/LiberationSans SDF - Outline");
        var material = label.fontMaterial;
        if (material == null || !material.HasProperty("_OutlineWidth")) return;
        material.EnableKeyword("OUTLINE_ON");
        material.SetColor("_OutlineColor", Color.black);
        material.SetFloat("_OutlineWidth", 0.25f);
        label.UpdateMeshPadding();
        label.SetVerticesDirty();
    }
}
