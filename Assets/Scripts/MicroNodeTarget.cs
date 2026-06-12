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

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime_MicroNode";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pixel = new Vector2(x, y);
                Vector2 delta = pixel - center;
                float normalizedDistance = delta.magnitude / radius;
                float angle = Mathf.Atan2(delta.y, delta.x);
                float spoke = Mathf.Max(
                    Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(angle * 4f)) * 18f),
                    Mathf.Clamp01(1f - Mathf.Abs(Mathf.Cos(angle * 4f)) * 18f)
                );
                float outerGlow = Mathf.Pow(Mathf.Clamp01(1f - normalizedDistance), 1.45f);
                float outerRing = Mathf.Clamp01(1f - Mathf.Abs(normalizedDistance - 0.78f) * 26f);
                float innerRing = Mathf.Clamp01(1f - Mathf.Abs(normalizedDistance - 0.43f) * 22f);
                float core = Mathf.Pow(Mathf.Clamp01(1f - normalizedDistance * 3.1f), 1.4f);
                float tickMask = normalizedDistance > 0.52f && normalizedDistance < 0.9f ? spoke * 0.55f : 0f;
                float alpha = Mathf.Clamp01(outerGlow * 0.28f + outerRing + innerRing * 0.72f + core + tickMask);
                Color color = new Color(0.58f + core * 0.42f, 0.9f + core * 0.1f, 1f, alpha);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        nodeSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        nodeSprite.name = "Runtime_MicroNode_Sprite";
        return nodeSprite;
    }
}
