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

        glowImage = EnsureImage("DialogueFullGlow", Vector2.zero, panelSize + new Vector2(30f, 24f), new Color(0.2f, 0.85f, 1f, 0.13f));
        glowImage.sprite = fullDialogueSprite;
        glowImage.preserveAspect = true;
        glowImage.raycastTarget = false;
        glowImage.transform.SetAsFirstSibling();

        artworkImage = EnsureImage("DialogueFullArtwork", Vector2.zero, panelSize, Color.white);
        artworkImage.sprite = fullDialogueSprite;
        artworkImage.preserveAspect = true;
        artworkImage.raycastTarget = false;

        scanImage = EnsureImage("DialogueFullScan", Vector2.zero, new Vector2(84f, panelSize.y * 0.92f), new Color(0.58f, 0.95f, 1f, 0.11f));
        scanImage.raycastTarget = false;
        scanImage.transform.SetAsLastSibling();

        artworkImage.enabled = fullDialogueSprite != null;
        glowImage.enabled = fullDialogueSprite != null;
        scanImage.enabled = fullDialogueSprite != null;
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
