using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class EverMotionFeedbackToast
{
    private static Coroutine activeRoutine;

    public static void Show(MonoBehaviour owner, string message, Color accent)
    {
        if (owner == null)
        {
            return;
        }

        if (activeRoutine != null)
        {
            owner.StopCoroutine(activeRoutine);
        }

        activeRoutine = owner.StartCoroutine(ShowRoutine(message, accent));
    }

    private static IEnumerator ShowRoutine(string message, Color accent)
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            activeRoutine = null;
            yield break;
        }

        GameObject root = new GameObject("EverMotion_FeedbackToast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -132f);
        rect.sizeDelta = new Vector2(820f, 172f);
        rect.SetAsLastSibling();

        Image image = root.GetComponent<Image>();
        Sprite toastSprite = Resources.Load<Sprite>("MicroNode/NodeRestoredToast");
        image.sprite = toastSprite;
        image.preserveAspect = true;
        image.color = toastSprite != null ? Color.white : new Color(0.01f, 0.06f, 0.1f, 0.84f);
        image.raycastTarget = false;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Text label = CreateLabel(root.transform, message, accent);

        float elapsed = 0f;
        const float duration = 0.86f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float inOut = Mathf.SmoothStep(0f, 1f, Mathf.Min(t * 4f, (1f - t) * 4f));
            float pop = Mathf.Sin(t * Mathf.PI);

            group.alpha = inOut;
            root.transform.localScale = Vector3.one * (1f + pop * 0.075f);
            label.color = Color.Lerp(Color.white, accent, 0.42f + pop * 0.36f);
            yield return null;
        }

        Object.Destroy(root);
        activeRoutine = null;
    }

    private static Text CreateLabel(Transform parent, string message, Color accent)
    {
        GameObject obj = new GameObject("Text_FeedbackToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(72f, 38f);
        rect.offsetMax = new Vector2(-72f, -38f);

        Text label = obj.GetComponent<Text>();
        label.text = message;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (label.font == null)
        {
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        label.fontSize = 39;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = accent;
        label.raycastTarget = false;

        Outline outline = obj.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0.2f, 0.3f, 0.72f);
        outline.effectDistance = new Vector2(1f, -1f);

        return label;
    }
}
