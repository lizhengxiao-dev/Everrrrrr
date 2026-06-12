using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class MicroNodeIntroAnimator : MonoBehaviour
{
    public Text titleText;
    public Text bodyText;

    private CanvasGroup canvasGroup;
    private Outline outline;
    private RectTransform scanLine;
    private float nextMessageTime;
    private int messageIndex;

    private static readonly string[] Messages =
    {
        "PRECISION SYSTEM // LAST CELLS DRIFTING",
        "ROBOT FINGERS // SIGNAL TOO WEAK",
        "PINCH EACH NODE // RESTORE FINE CONTROL"
    };

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        outline = GetComponent<Outline>();
        if (titleText == null)
        {
            titleText = transform.Find("Text_StoryTitle")?.GetComponent<Text>();
        }
        if (bodyText == null)
        {
            bodyText = transform.Find("Text_StoryBody")?.GetComponent<Text>();
        }

        EnsureScanLine();
    }

    private void OnEnable()
    {
        nextMessageTime = Time.unscaledTime;
        messageIndex = 0;
    }

    private void Update()
    {
        float pulse = 0.88f + Mathf.Sin(Time.unscaledTime * 3.8f) * 0.08f;
        canvasGroup.alpha = pulse;

        if (outline != null)
        {
            Color color = outline.effectColor;
            color.a = 0.52f + Mathf.Sin(Time.unscaledTime * 4.6f) * 0.18f;
            outline.effectColor = color;
        }

        if (scanLine != null)
        {
            float y = Mathf.Lerp(-86f, 86f, Mathf.Repeat(Time.unscaledTime * 0.22f, 1f));
            scanLine.anchoredPosition = new Vector2(0f, y);
        }

        if (Time.unscaledTime >= nextMessageTime)
        {
            nextMessageTime = Time.unscaledTime + 1.05f;
            if (bodyText != null)
            {
                bodyText.text = Messages[messageIndex % Messages.Length];
            }
            messageIndex++;
        }

        if (titleText != null)
        {
            titleText.color = Random.value < 0.025f
                ? new Color(0.65f, 1f, 1f, 1f)
                : new Color(1f, 0.36f, 0.5f, 1f);
        }
    }

    private void EnsureScanLine()
    {
        Transform existing = transform.Find("IntroScanLine");
        GameObject lineObject = existing != null
            ? existing.gameObject
            : new GameObject("IntroScanLine", typeof(RectTransform), typeof(Image));
        lineObject.transform.SetParent(transform, false);

        scanLine = lineObject.GetComponent<RectTransform>();
        scanLine.anchorMin = new Vector2(0.5f, 0.5f);
        scanLine.anchorMax = new Vector2(0.5f, 0.5f);
        scanLine.pivot = new Vector2(0.5f, 0.5f);
        scanLine.sizeDelta = new Vector2(860f, 2f);

        Image image = lineObject.GetComponent<Image>();
        image.color = new Color(0.15f, 0.95f, 1f, 0.24f);
        image.raycastTarget = false;
    }
}
