using UnityEngine;

// A solid rectangular border remains visible regardless of sprite transparency.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class MovingBarOutline : MonoBehaviour
{
    private static Sprite solidSprite;
    private SpriteRenderer source;
    private SpriteRenderer border;

    private void Awake()
    {
        source = GetComponent<SpriteRenderer>();
        if (solidSprite == null)
            solidSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1);
        var go = new GameObject("Moving Bar Outline");
        go.transform.SetParent(transform, false);
        border = go.AddComponent<SpriteRenderer>();
        border.sprite = solidSprite;
        border.color = Color.black;
        UpdateOutline();
    }

    private void LateUpdate() => UpdateOutline();

    private void UpdateOutline()
    {
        if (source == null || border == null) return;
        border.enabled = source.enabled && source.sprite != null;
        if (!border.enabled) return;
        border.sortingLayerID = source.sortingLayerID;
        border.sortingOrder = source.sortingOrder - 1;
        var bounds = source.sprite.bounds;
        var scale = transform.lossyScale;
        const float thickness = 0.04f;
        border.transform.localPosition = bounds.center;
        border.transform.localScale = new Vector3(
            bounds.size.x + 2f * thickness / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            bounds.size.y + 2f * thickness / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1f);
    }
}
