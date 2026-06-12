using UnityEngine;

public class MicroNodeTarget : MonoBehaviour
{
    private static Sprite nodeSprite;

    private MicroNodeActivationManager owner;
    private SpriteRenderer spriteRenderer;
    private Vector2 velocity;
    private float lifetime;
    private float remainingLifetime;
    private float pulseOffset;
    private Vector3 baseScale;
    private bool resolved;

    public float RemainingLife01 => lifetime <= 0f ? 0f : Mathf.Clamp01(remainingLifetime / lifetime);

    public void Initialize(
        MicroNodeActivationManager gameOwner,
        Vector2 driftVelocity,
        float nodeLifetime,
        Color color,
        float scaleMultiplier,
        int sortingOrder)
    {
        owner = gameOwner;
        velocity = driftVelocity;
        lifetime = Mathf.Max(0.2f, nodeLifetime);
        remainingLifetime = lifetime;
        pulseOffset = Random.Range(0f, Mathf.PI * 2f);
        baseScale = Vector3.one * Random.Range(0.72f, 0.92f) * Mathf.Max(0.5f, scaleMultiplier);

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetNodeSprite();
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;

        transform.localScale = baseScale;
    }

    public void Capture()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        owner.NotifyNodeCaptured(this);
        Destroy(gameObject);
    }

    private void Update()
    {
        if (resolved)
        {
            return;
        }

        remainingLifetime -= Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);

        float pulse = 1f + Mathf.Sin(Time.time * 6f + pulseOffset) * 0.07f;
        float lifeScale = Mathf.Lerp(0.55f, 1f, RemainingLife01);
        transform.localScale = baseScale * pulse * lifeScale;

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = Mathf.Lerp(0.16f, 1f, RemainingLife01);
            spriteRenderer.color = color;
        }

        if (remainingLifetime <= 0f)
        {
            resolved = true;
            owner.NotifyNodeExpired(this);
            Destroy(gameObject);
        }
    }

    private static Sprite GetNodeSprite()
    {
        if (nodeSprite != null)
        {
            return nodeSprite;
        }

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime_MicroNode";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.47f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedDistance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float outerGlow = Mathf.Clamp01(1f - normalizedDistance);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(normalizedDistance - 0.67f) * 18f);
                float core = Mathf.Clamp01(1f - normalizedDistance * 2.7f);
                float alpha = Mathf.Clamp01(outerGlow * 0.32f + ring * 0.92f + core);
                Color color = new Color(0.72f + core * 0.28f, 1f, 1f, alpha);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        nodeSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        nodeSprite.name = "Runtime_MicroNode_Sprite";
        return nodeSprite;
    }
}
