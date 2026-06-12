using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class MicroNodeEndDialogueAnimator : MonoBehaviour
{
    public Sprite robotPortraitSprite;
    public string speaker = "ROBOT";
    [TextArea(2, 4)]
    public string message = "\"Thank you... I can feel my fingers responding again. Keep going - we're making progress!\"";
    public float typewriterCharactersPerSecond = 34f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private MicroNodeDialogueBubbleGraphic bubbleGraphic;
    private Image portraitImage;
    private Text speakerText;
    private Text bodyText;
    private Text hintText;
    private Image scanLine;
    private Image[] waveformBars;
    private Coroutine showRoutine;
    private Vector2 basePosition;
    private Vector3 baseScale;
    private string pendingSpeaker;
    private string pendingMessage;

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
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }
        showRoutine = StartCoroutine(ShowRoutine());
    }

    private void Update()
    {
        if (bubbleGraphic != null)
        {
            float glow = 0.65f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.25f;
            bubbleGraphic.edgeAlpha = glow;
            bubbleGraphic.SetVerticesDirty();
        }

        if (scanLine != null)
        {
            RectTransform scanRect = scanLine.rectTransform;
            float x = Mathf.Lerp(-440f, 440f, Mathf.Repeat(Time.unscaledTime * 0.32f, 1f));
            scanRect.anchoredPosition = new Vector2(x, scanRect.anchoredPosition.y);
        }

        if (waveformBars == null)
        {
            return;
        }

        for (int i = 0; i < waveformBars.Length; i++)
        {
            if (waveformBars[i] == null)
            {
                continue;
            }

            float wave = 0.45f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7.5f + i * 0.55f)) * 0.85f;
            waveformBars[i].rectTransform.sizeDelta = new Vector2(5f, 12f + wave * 32f);
            waveformBars[i].color = new Color(0.32f, 0.93f, 1f, 0.55f + wave * 0.25f);
        }
    }

    public void SetDialogue(string newSpeaker, string newMessage, Sprite newPortrait)
    {
        pendingSpeaker = string.IsNullOrWhiteSpace(newSpeaker) ? speaker : newSpeaker;
        pendingMessage = string.IsNullOrWhiteSpace(newMessage) ? message : newMessage;
        if (newPortrait != null)
        {
            robotPortraitSprite = newPortrait;
        }

        speaker = pendingSpeaker;
        message = pendingMessage;
        ApplyStaticText();

        if (isActiveAndEnabled)
        {
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
        const float outDuration = 0.22f;
        Vector2 startPosition = rectTransform.anchoredPosition;
        Vector3 startScale = rectTransform.localScale;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        while (elapsed < outDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / outDuration);
            float eased = t * t;
            canvasGroup.alpha = 1f - eased;
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, basePosition + new Vector2(0f, -38f), eased);
            rectTransform.localScale = Vector3.Lerp(startScale, new Vector3(baseScale.x * 0.94f, baseScale.y * 0.78f, baseScale.z), eased);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    private IEnumerator ShowRoutine()
    {
        ApplyStaticText();
        if (bodyText != null)
        {
            bodyText.text = string.Empty;
        }

        float elapsed = 0f;
        const float inDuration = 0.36f;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        rectTransform.anchoredPosition = basePosition + new Vector2(0f, -48f);
        rectTransform.localScale = new Vector3(baseScale.x * 0.92f, baseScale.y * 0.88f, baseScale.z);

        while (elapsed < inDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / inDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            canvasGroup.alpha = eased;
            rectTransform.anchoredPosition = Vector2.Lerp(basePosition + new Vector2(0f, -48f), basePosition, eased);
            float pop = 1f + Mathf.Sin(t * Mathf.PI) * 0.035f;
            rectTransform.localScale = baseScale * Mathf.Lerp(0.92f, pop, eased);
            yield return null;
        }

        rectTransform.anchoredPosition = basePosition;
        rectTransform.localScale = baseScale;
        canvasGroup.alpha = 1f;

        yield return TypeMessage();
        showRoutine = null;
    }

    private IEnumerator TypeMessage()
    {
        if (bodyText == null)
        {
            yield break;
        }

        string text = message;
        float delay = 1f / Mathf.Max(1f, typewriterCharactersPerSecond);
        for (int i = 0; i <= text.Length; i++)
        {
            bodyText.text = text.Substring(0, i);
            yield return new WaitForSecondsRealtime(delay);
        }
    }

    private void ApplyStaticText()
    {
        if (speakerText != null)
        {
            speakerText.text = speaker;
        }
        if (hintText != null)
        {
            hintText.text = "PRESS R TO RESTART";
        }
        if (portraitImage != null)
        {
            portraitImage.sprite = robotPortraitSprite;
            portraitImage.enabled = robotPortraitSprite != null;
        }
    }

    private void EnsureLayout()
    {
        rectTransform = rectTransform != null ? rectTransform : GetComponent<RectTransform>();
        canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(0f, -58f);
        rectTransform.sizeDelta = new Vector2(1040f, 390f);

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

        bubbleGraphic = GetComponent<MicroNodeDialogueBubbleGraphic>();
        if (bubbleGraphic == null)
        {
            bubbleGraphic = gameObject.AddComponent<MicroNodeDialogueBubbleGraphic>();
        }

        portraitImage = EnsureImage("DialogueRobotPortrait", new Vector2(-400f, 0f), new Vector2(310f, 310f), new Color(1f, 1f, 1f, 1f));
        portraitImage.preserveAspect = true;

        speakerText = EnsureText("DialogueSpeakerText", speaker, new Vector2(-178f, 112f), new Vector2(210f, 62f), 44, TextAnchor.MiddleLeft, FontStyle.Bold);
        bodyText = EnsureText("DialogueBodyText", message, new Vector2(150f, -4f), new Vector2(660f, 190f), 38, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
        hintText = EnsureText("DialogueHintText", "PRESS R TO RESTART", new Vector2(334f, -160f), new Vector2(330f, 34f), 17, TextAnchor.MiddleRight, FontStyle.Bold);

        EnsureDivider();
        EnsureScanLine();
        EnsureWaveform();
        ApplyStaticText();
    }

    private Image EnsureImage(string name, Vector2 position, Vector2 size, Color color)
    {
        Transform existing = transform.Find(name);
        GameObject imageObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text EnsureText(string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, FontStyle style)
    {
        Transform existing = transform.Find(name);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
        textObject.transform.SetParent(transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = new Color32(118, 235, 255, 255);

        Shadow shadow = textObject.GetComponent<Shadow>();
        shadow.effectColor = new Color32(0, 165, 255, 180);
        shadow.effectDistance = new Vector2(0f, -2f);
        return text;
    }

    private void EnsureDivider()
    {
        Image divider = EnsureImage("DialogueDivider", new Vector2(176f, 72f), new Vector2(684f, 2f), new Color32(36, 176, 255, 96));
        divider.type = Image.Type.Simple;
    }

    private void EnsureScanLine()
    {
        scanLine = EnsureImage("DialogueScanLine", new Vector2(-440f, -118f), new Vector2(120f, 3f), new Color32(92, 238, 255, 120));
    }

    private void EnsureWaveform()
    {
        const int count = 11;
        if (waveformBars == null || waveformBars.Length != count)
        {
            waveformBars = new Image[count];
        }

        for (int i = 0; i < count; i++)
        {
            Image bar = EnsureImage("DialogueWaveBar_" + i.ToString("00"), new Vector2(-12f + i * 10f, 113f), new Vector2(5f, 18f), new Color32(88, 231, 255, 210));
            waveformBars[i] = bar;
        }
    }
}

public class MicroNodeDialogueBubbleGraphic : MaskableGraphic
{
    public float edgeAlpha = 0.85f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float x0 = r.xMin + 185f;
        float x1 = r.xMax - 18f;
        float y0 = r.yMin + 22f;
        float y1 = r.yMax - 28f;
        float notchX = x0 - 70f;
        float notchY = Mathf.Lerp(y0, y1, 0.66f);
        float cut = 34f;

        Vector2[] points =
        {
            new Vector2(x0 + cut, y1),
            new Vector2(x1 - cut, y1),
            new Vector2(x1, y1 - cut),
            new Vector2(x1, y0 + cut),
            new Vector2(x1 - cut, y0),
            new Vector2(x0 + cut, y0),
            new Vector2(x0, y0 + cut),
            new Vector2(notchX, notchY),
            new Vector2(x0, y1 - cut)
        };

        Color32 fill = new Color32(3, 17, 39, 238);
        Color32 edge = new Color32(54, 206, 255, (byte)Mathf.RoundToInt(edgeAlpha * 255f));
        AddPolygon(vh, points, fill);
        AddLine(vh, points, 5f, edge);
    }

    private static void AddPolygon(VertexHelper vh, Vector2[] points, Color32 color)
    {
        int start = vh.currentVertCount;
        for (int i = 0; i < points.Length; i++)
        {
            vh.AddVert(points[i], color, Vector2.zero);
        }
        for (int i = 1; i < points.Length - 1; i++)
        {
            vh.AddTriangle(start, start + i, start + i + 1);
        }
    }

    private static void AddLine(VertexHelper vh, Vector2[] points, float width, Color32 color)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Length];
            Vector2 dir = (b - a).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x) * width * 0.5f;
            int start = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
