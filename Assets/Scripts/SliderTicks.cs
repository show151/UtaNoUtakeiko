using UnityEngine;
using System.Collections;

public class SliderTicks : MonoBehaviour
{
    [Header("Sprite")]
    [SerializeField] private Sprite tickSprite;
    [SerializeField] private Sprite dividerSprite;

    [Header("Layout")]
    [SerializeField, Min(2)] private int tickCount = 10;
    [SerializeField] private float minY = -3f;
    [SerializeField] private float maxY = 3f;
    [SerializeField] private float tickX = -5.3f;
    [SerializeField] private float tickWidth = 1.2f;

    [Header("Dividers")]
    [SerializeField] private float dividerHeight = 0.025f;
    [SerializeField]
    private Color dividerColor =
        new Color(0.1f, 0.15f, 0.25f, 1f);

    [Header("Soft Glow")] 
    private Sprite softGlowSprite;
    [SerializeField] private float glowDuration = 0.15f;
    [SerializeField] private float glowMaxAlpha = 0.65f;
    [SerializeField] private float glowSizeMultiplier = 1.5f;

    private Coroutine glowCoroutine;

    private SpriteRenderer[] tickRenderers;
    private SpriteRenderer[] glowRenderers;

    private void Start()
    {
        if (tickSprite == null || dividerSprite == null)
        {
            Debug.LogError("Spriteが未設定です。", this);
            return;
        }
        softGlowSprite = CreateSoftGlowSprite();

        tickRenderers = new SpriteRenderer[tickCount];
        glowRenderers = new SpriteRenderer[tickCount];

        // 10個のブロックで可動範囲全体を覆う
        float totalHeight = maxY - minY;
        float blockHeight = totalHeight / tickCount;

        Vector2 tickSize = tickSprite.bounds.size;
        Vector2 dividerSize = dividerSprite.bounds.size;

        for (int i = 0; i < tickCount; i++)
        {
            // ブロックの中心位置
            float y = minY + blockHeight * (i + 0.5f);

            // 色のグラデーション
            float t = (float)i / (tickCount - 1);
            Color color = GetGradientColor(t);

            // 発光用Sprite
            GameObject glow = new GameObject($"Glow_{i}");
            glow.transform.SetParent(transform);
            glow.transform.position = new Vector3(tickX, y, 0f);

            SpriteRenderer glowRenderer =
                glow.AddComponent<SpriteRenderer>();

            glowRenderer.sprite = softGlowSprite;

            Vector2 glowSize = glowRenderer.sprite.bounds.size;

            glow.transform.localScale = new Vector3(
                tickWidth * glowSizeMultiplier / glowSize.x,
                blockHeight * glowSizeMultiplier / glowSize.y,
                1f
            );

            glowRenderer.sortingOrder = 1;
            glowRenderer.color = new Color(
                color.r, color.g, color.b, 0f
            );
            glowRenderer.enabled = false;

            glowRenderers[i] = glowRenderer;

            // 通常のブロック
            GameObject tick = new GameObject($"Tick_{i}");
            tick.transform.SetParent(transform);
            tick.transform.position =
                new Vector3(tickX, y, 0f);

            tick.transform.localScale = new Vector3(
                tickWidth / tickSize.x,
                blockHeight / tickSize.y,
                1f
            );

            SpriteRenderer renderer =
                tick.AddComponent<SpriteRenderer>();

            renderer.sprite = tickSprite;
            renderer.color = color;
            renderer.sortingOrder = 2;

            tickRenderers[i] = renderer;
        }

        // ブロック間の分割線
        for (int i = 1; i < tickCount; i++)
        {
            float y = minY + blockHeight * i;

            GameObject divider =
                new GameObject($"Divider_{i}");

            divider.transform.SetParent(transform);
            divider.transform.position =
                new Vector3(tickX, y, 0f);

            divider.transform.localScale = new Vector3(
                tickWidth / dividerSize.x,
                dividerHeight / dividerSize.y,
                1f
            );

            SpriteRenderer renderer =
                divider.AddComponent<SpriteRenderer>();

            renderer.sprite = dividerSprite;
            renderer.color = dividerColor;
            renderer.sortingOrder = 3;
        }
    }

    private Color GetGradientColor(float t)
    {
        // 下（低音）から上（高音）の順
        Color[] colors =
        {
        new Color32(69, 158, 241, 255),  // Blue
        new Color32(57, 199, 206, 255),  // Cyan
        new Color32(78, 212, 163, 255),  // Mint
        new Color32(135, 219, 121, 255), // Green
        new Color32(198, 229, 104, 255), // Lime
        new Color32(244, 220, 88, 255),  // Yellow
        new Color32(255, 196, 81, 255),  // Gold
        new Color32(255, 157, 72, 255),  // Orange
        new Color32(255, 117, 80, 255),  // Coral
        new Color32(255, 82, 101, 255)   // Red
    };

        float scaled = Mathf.Clamp01(t) * (colors.Length - 1);

        int index = Mathf.Min(
            Mathf.FloorToInt(scaled),
            colors.Length - 2
        );

        return Color.Lerp(
            colors[index],
            colors[index + 1],
            scaled - index
        );
    }

    public void HighlightNearestTick(float normalizedPosition)
    {
        if (tickRenderers == null) return;

        ClearHighlight();

        int index = Mathf.Min(
            Mathf.FloorToInt(
                Mathf.Clamp01(normalizedPosition) * tickCount
            ),
            tickCount - 1
        );

        glowCoroutine = StartCoroutine(
            AnimateGlow(index)
        );
    }

    private IEnumerator AnimateGlow(int index)
    {
        SpriteRenderer tick = tickRenderers[index];
        SpriteRenderer glow = glowRenderers[index];

        Color baseColor = GetGradientColor(
            (float)index / (tickCount - 1)
        );

        glow.enabled = true;

        float elapsed = 0f;

        while (elapsed < glowDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / Mathf.Max(glowDuration, 0.001f)
            );

            // ゆっくり立ち上がる発光
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            tick.color = Color.Lerp(
                baseColor,
                Color.white,
                eased * 0.5f
            );

            glow.color = new Color(
                baseColor.r,
                baseColor.g,
                baseColor.b,
                eased * glowMaxAlpha
            );

            yield return null;
        }

        glowCoroutine = null;
    }

    private Sprite CreateSoftGlowSprite()
    {
        const int size = 128;

        Texture2D texture = new Texture2D(
            size, size, TextureFormat.RGBA32, false
        );

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - size / 2f) / (size / 2f);
                float ny = (y - size / 2f) / (size / 2f);

                // 中央からの距離
                float distance = Mathf.Sqrt(
                    nx * nx + ny * ny
                );

                // 中央が明るく、外側ほど透明
                float alpha = Mathf.Exp(
                    -distance * distance * 5f
                );

                // 画像の端では完全に透明
                alpha *= Mathf.Clamp01(
                    (1f - distance) * 4f
                );

                pixels[y * size + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    public void ClearHighlight()
    {
        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
            glowCoroutine = null;
        }

        if (tickRenderers == null) return;

        for (int i = 0; i < tickCount; i++)
        {
            float t = (float)i / (tickCount - 1);

            tickRenderers[i].color = GetGradientColor(t);
            glowRenderers[i].enabled = false;
        }
    }
}
