using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class MicroNodeEndDialogueAnimator : MonoBehaviour
{
    [Header("Full Dialogue Artwork")]
    public Sprite fullDialogueSprite;
    public string speaker = "ROBOT";
    [TextArea(2, 4)]
    public string message = "\"Thank you... I can feel my fingers responding again. Keep going - we're making progress!\"";
    public Vector2 panelSize = new Vector2(980f, 300f);
    public Vector2 panelOffset = new Vector2(0f, 8f);

    [Header("Effects")]
    public float popInDuration = 0.34f;
    public float popOutDuration = 0.22f;
    public float glowPulseSpeed = 4.4f;
    public float scanSpeed = 0.55f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Image artworkImage;
    private Image glowImage;
    private Image scanImage;
    private Coroutine showRoutine;
    private Sprite cleanedDialogueSprite;
    private Sprite cleanedSourceSprite;
    private Vector2 basePosition;
    private Vector3 baseScale;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        EnsureLayout();
        basePosition = rectTransform.anchoredPosition;
        baseScale = rectTransform.localScale;
    }

    private void OnEnable()
    {
        EnsureLayout();
        transform.SetAsLastSibling();

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }

        showRoutine = StartCoroutine(ShowRoutine());
    }

    private void Update()
    {
        if (glowImage != null)
        {
            float alpha = 0.12f + Mathf.Sin(Time.unscaledTime * glowPulseSpeed) * 0.04f;
            glowImage.color = new Color(0.2f, 0.85f, 1f, alpha);
        }

        if (scanImage != null)
        {
            float halfWidth = panelSize.x * 0.5f;
            float x = Mathf.Lerp(-halfWidth, halfWidth, Mathf.Repeat(Time.unscaledTime * scanSpeed, 1f));
            scanImage.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }
    }

    public void SetDialogue(string newSpeaker, string newMessage, Sprite ignoredPortrait)
    {
        EnsureLayout();

        if (isActiveAndEnabled)
        {
            transform.SetAsLastSibling();
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
            }

            showRoutine = StartCoroutine(ShowRoutine());
        }
    }

    public IEnumerator PlayHideOutRoutine()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        EnsureLayout();
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, popOutDuration);
        Vector2 startPosition = rectTransform.anchoredPosition;
        Vector3 startScale = rectTransform.localScale;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;
            canvasGroup.alpha = 1f - eased;
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, basePosition + new Vector2(0f, -36f), eased);
            rectTransform.localScale = Vector3.Lerp(startScale, new Vector3(baseScale.x * 0.96f, baseScale.y * 0.82f, baseScale.z), eased);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    private IEnumerator ShowRoutine()
    {
        EnsureLayout();
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, popInDuration);
        Vector2 startPosition = basePosition + new Vector2(0f, -46f);
        Vector3 startScale = new Vector3(baseScale.x * 0.92f, baseScale.y * 0.88f, baseScale.z);

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        rectTransform.anchoredPosition = startPosition;
        rectTransform.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float pop = 1f + Mathf.Sin(t * Mathf.PI) * 0.035f;
            canvasGroup.alpha = eased;
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, basePosition, eased);
            rectTransform.localScale = baseScale * Mathf.Lerp(0.92f, pop, eased);
            yield return null;
        }

        rectTransform.anchoredPosition = basePosition;
        rectTransform.localScale = baseScale;
        canvasGroup.alpha = 1f;
        showRoutine = null;
    }

    private void EnsureLayout()
    {
        rectTransform = rectTransform != null ? rectTransform : GetComponent<RectTransform>();
        canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = panelOffset;
        rectTransform.sizeDelta = panelSize;

        Canvas panelCanvas = GetComponent<Canvas>();
        if (panelCanvas == null)
        {
            panelCanvas = gameObject.AddComponent<Canvas>();
        }
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 5000;

        DisableOldRootChrome();
        DestroyOldGeneratedChildren();

        Sprite displaySprite = GetDisplaySprite();
        glowImage = EnsureImage("DialogueFullGlow", Vector2.zero, panelSize + new Vector2(30f, 24f), new Color(0.2f, 0.85f, 1f, 0.13f));
        glowImage.sprite = displaySprite;
        glowImage.preserveAspect = true;
        glowImage.raycastTarget = false;
        glowImage.transform.SetAsFirstSibling();

        artworkImage = EnsureImage("DialogueFullArtwork", Vector2.zero, panelSize, Color.white);
        artworkImage.sprite = displaySprite;
        artworkImage.preserveAspect = true;
        artworkImage.raycastTarget = false;

        scanImage = EnsureImage("DialogueFullScan", Vector2.zero, new Vector2(84f, panelSize.y * 0.92f), new Color(0.58f, 0.95f, 1f, 0.11f));
        scanImage.raycastTarget = false;
        scanImage.transform.SetAsLastSibling();

        artworkImage.enabled = displaySprite != null;
        glowImage.enabled = displaySprite != null;
        scanImage.enabled = displaySprite != null;
    }

    private Sprite GetDisplaySprite()
    {
        Sprite source = fullDialogueSprite;
        if (source == null)
        {
            Transform existingArtwork = transform.Find("DialogueFullArtwork");
            Image existingImage = existingArtwork != null ? existingArtwork.GetComponent<Image>() : null;
            source = existingImage != null ? existingImage.sprite : null;
        }

        if (source == null)
        {
            return null;
        }

        if (cleanedDialogueSprite != null && cleanedSourceSprite == source)
        {
            return cleanedDialogueSprite;
        }

        cleanedSourceSprite = source;
        cleanedDialogueSprite = CreateTransparentBackgroundSprite(source);
        return cleanedDialogueSprite != null ? cleanedDialogueSprite : source;
    }

    private static Sprite CreateTransparentBackgroundSprite(Sprite source)
    {
        Texture2D sourceTexture = source.texture;
        if (sourceTexture == null)
        {
            return null;
        }

        Rect spriteRect = source.rect;
        int width = Mathf.RoundToInt(spriteRect.width);
        int height = Mathf.RoundToInt(spriteRect.height);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        Texture2D readable = CopySpriteTexture(sourceTexture, spriteRect, width, height);
        if (readable == null)
        {
            return null;
        }

        Color32[] pixels = readable.GetPixels32();
        bool[] visited = new bool[pixels.Length];
        int[] queue = new int[pixels.Length];
        int head = 0;
        int tail = 0;

        for (int x = 0; x < width; x++)
        {
            EnqueueIfBackground(x, 0, width, pixels, visited, queue, ref tail);
            EnqueueIfBackground(x, height - 1, width, pixels, visited, queue, ref tail);
        }

        for (int y = 1; y < height - 1; y++)
        {
            EnqueueIfBackground(0, y, width, pixels, visited, queue, ref tail);
            EnqueueIfBackground(width - 1, y, width, pixels, visited, queue, ref tail);
        }

        while (head < tail)
        {
            int index = queue[head++];
            pixels[index].a = 0;
            int x = index % width;
            int y = index / width;

            if (x > 0)
            {
                EnqueueIfBackground(x - 1, y, width, pixels, visited, queue, ref tail);
            }
            if (x < width - 1)
            {
                EnqueueIfBackground(x + 1, y, width, pixels, visited, queue, ref tail);
            }
            if (y > 0)
            {
                EnqueueIfBackground(x, y - 1, width, pixels, visited, queue, ref tail);
            }
            if (y < height - 1)
            {
                EnqueueIfBackground(x, y + 1, width, pixels, visited, queue, ref tail);
            }
        }

        readable.SetPixels32(pixels);
        readable.Apply(false, false);
        readable.name = source.name + "_TransparentRuntime";
        Vector2 normalizedPivot = new Vector2(
            source.pivot.x / Mathf.Max(1f, source.rect.width),
            source.pivot.y / Mathf.Max(1f, source.rect.height)
        );
        return Sprite.Create(readable, new Rect(0f, 0f, width, height), normalizedPivot, source.pixelsPerUnit);
    }

    private static Texture2D CopySpriteTexture(Texture2D sourceTexture, Rect spriteRect, int width, int height)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
        Texture2D fullCopy = null;
        Texture2D cropped = null;

        try
        {
            Graphics.Blit(sourceTexture, temporary);
            RenderTexture.active = temporary;
            fullCopy = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
            fullCopy.ReadPixels(new Rect(0f, 0f, sourceTexture.width, sourceTexture.height), 0, 0);
            fullCopy.Apply(false, false);

            cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] sourcePixels = fullCopy.GetPixels(
                Mathf.RoundToInt(spriteRect.x),
                Mathf.RoundToInt(spriteRect.y),
                width,
                height
            );
            cropped.SetPixels(sourcePixels);
            cropped.Apply(false, false);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            if (fullCopy != null)
            {
                Destroy(fullCopy);
            }
        }

        return cropped;
    }

    private static void EnqueueIfBackground(
        int x,
        int y,
        int width,
        Color32[] pixels,
        bool[] visited,
        int[] queue,
        ref int tail)
    {
        int index = y * width + x;
        if (visited[index] || !IsCheckerBackground(pixels[index]))
        {
            return;
        }

        visited[index] = true;
        queue[tail++] = index;
    }

    private static bool IsCheckerBackground(Color32 color)
    {
        if (color.a < 8)
        {
            return true;
        }

        int max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        int min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        return max > 214 && max - min < 18;
    }

    private void DisableOldRootChrome()
    {
        Image oldImage = GetComponent<Image>();
        if (oldImage != null)
        {
            oldImage.enabled = false;
        }

        Outline oldOutline = GetComponent<Outline>();
        if (oldOutline != null)
        {
            oldOutline.enabled = false;
        }

        MicroNodeDialogueBubbleGraphic oldBubble = GetComponent<MicroNodeDialogueBubbleGraphic>();
        if (oldBubble != null)
        {
            oldBubble.enabled = false;
        }
    }

    private void DestroyOldGeneratedChildren()
    {
        string[] oldNames =
        {
            "DialogueRobotPortrait",
            "DialogueSpeakerText",
            "DialogueBodyText",
            "DialogueHintText",
            "DialogueDivider",
            "DialogueScanLine"
        };

        for (int i = 0; i < oldNames.Length; i++)
        {
            Transform child = transform.Find(oldNames[i]);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("DialogueWaveBar_"))
            {
                Destroy(child.gameObject);
            }
        }
    }

    private Image EnsureImage(string name, Vector2 position, Vector2 size, Color color)
    {
        Transform existing = transform.Find(name);
        GameObject imageObject = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(Image));

        imageObject.transform.SetParent(transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }
}

public class MicroNodeDialogueBubbleGraphic : MaskableGraphic
{
    public float edgeAlpha = 0.85f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
    }
}
