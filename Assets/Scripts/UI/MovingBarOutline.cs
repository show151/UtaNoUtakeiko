using UnityEngine;

// A second, slightly larger sprite follows the moving white bar.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class MovingBarOutline : MonoBehaviour
{
    private void Awake()
    {
        var source = GetComponent<SpriteRenderer>();
        if (source.sprite == null) return;
        var border = new GameObject("Moving Bar Outline");
        border.transform.SetParent(transform, false);
        border.transform.localScale = new Vector3(1.04f, 1.65f, 1f);
        var renderer = border.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.color = Color.black;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder - 1;
    }
}
