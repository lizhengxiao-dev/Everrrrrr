using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class MicroNodeRobotFeedback : MonoBehaviour
{
    public string idleStateName = "Male_Idle";
    public string precisionStateName = "Male_PrecisionActivate";
    public bool useGestureSynchronizedSprites = true;
    public Sprite[] gestureSprites;
    public HandTracker handTracker;
    public float pinchAdvanceSpeed = 28f;
    public float releaseAdvanceSpeed = 24f;
    public Vector3 fingertipGlowLocalPosition = new Vector3(1.55f, 0.7f, -0.08f);
    public int sortingOrder = 165;

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Transform glowRoot;
    private SpriteRenderer glowRenderer;
    private GameObject travelingBurst;
    private Coroutine feedbackRoutine;
    private float gesture01;
    private static Sprite glowSprite;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (handTracker == null)
        {
            handTracker = Object.FindFirstObjectByType<HandTracker>();
        }
        if (UseGestureSprites())
        {
            gesture01 = 0f;
            if (animator != null)
            {
                animator.enabled = false;
            }
            ApplyGestureFrame();
        }
        else
        {
            KeepRobotIdle();
        }
        EnsureGlow();
        SetGlowAlpha(0f);
    }

    private void Update()
    {
        if (!UseGestureSprites())
        {
            return;
        }

        bool isPinching = handTracker != null && handTracker.HasRecentMessage() && handTracker.IsPinching();
        float target = isPinching ? 1f : 0f;
        float speed = isPinching ? pinchAdvanceSpeed : releaseAdvanceSpeed;
        gesture01 = Mathf.MoveTowards(gesture01, target, Mathf.Max(1f, speed) * Time.deltaTime);
        ApplyGestureFrame();
    }

    public void PlayActivation(Vector3 capturedNodeWorldPosition)
    {
        EnsureGlow();

        Vector3 localNode = transform.InverseTransformPoint(capturedNodeWorldPosition);
        Vector3 glowPosition = fingertipGlowLocalPosition;
        glowPosition.x = Mathf.Abs(glowPosition.x) * (localNode.x < 0f ? -1f : 1f);
        glowRoot.localPosition = glowPosition;

        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }

        if (travelingBurst != null)
        {
            Destroy(travelingBurst);
        }

        feedbackRoutine = StartCoroutine(ActivationRoutine(capturedNodeWorldPosition));
    }

    private IEnumerator ActivationRoutine(Vector3 capturedNodeWorldPosition)
    {
        float precisionDuration = UseGestureSprites() ? 0f : PlayPrecisionState();

        travelingBurst = new GameObject("MicroNode_EnergyBurst");
        SpriteRenderer burstRenderer = travelingBurst.AddComponent<SpriteRenderer>();
        burstRenderer.sprite = GetGlowSprite();
        burstRenderer.color = new Color(0.45f, 1f, 1f, 0.95f);
        burstRenderer.sortingOrder = sortingOrder + 1;
        travelingBurst.transform.position = capturedNodeWorldPosition;
        travelingBurst.transform.localScale = Vector3.one * 0.8f;

        float elapsed = 0f;
        const float travelDuration = 0.24f;
        Vector3 burstStart = capturedNodeWorldPosition;

        while (elapsed < travelDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            travelingBurst.transform.position = Vector3.Lerp(burstStart, glowRoot.position, eased);
            travelingBurst.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 0.3f, t);
            yield return null;
        }

        Destroy(travelingBurst);
        travelingBurst = null;

        elapsed = 0f;
        const float flareDuration = 0.18f;

        while (elapsed < flareDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flareDuration);
            float alpha = Mathf.Sin(t * Mathf.PI);
            glowRoot.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.45f, t);
            SetGlowAlpha(alpha);
            yield return null;
        }

        SetGlowAlpha(0f);

        float visibleFeedbackDuration = travelDuration + flareDuration;
        if (precisionDuration > visibleFeedbackDuration)
        {
            yield return new WaitForSeconds(precisionDuration - visibleFeedbackDuration);
        }

        if (!UseGestureSprites())
        {
            KeepRobotIdle();
        }

        feedbackRoutine = null;
    }

    private float PlayPrecisionState()
    {
        if (animator == null || string.IsNullOrEmpty(precisionStateName) || !HasAnimatorState(animator, precisionStateName))
        {
            return 0f;
        }

        animator.speed = 1f;
        animator.CrossFade(precisionStateName, 0.04f, 0, 0f);
        return Mathf.Max(0.35f, GetAnimationClipLength(precisionStateName));
    }

    private void KeepRobotIdle()
    {
        if (UseGestureSprites())
        {
            ApplyGestureFrame();
            return;
        }

        if (animator == null || string.IsNullOrEmpty(idleStateName))
        {
            return;
        }

        if (!animator.enabled)
        {
            animator.enabled = true;
        }
        animator.speed = 1f;
        animator.Play(idleStateName, 0, 0f);
    }

    private bool UseGestureSprites()
    {
        return useGestureSynchronizedSprites
            && spriteRenderer != null
            && gestureSprites != null
            && gestureSprites.Length > 0;
    }

    private void ApplyGestureFrame()
    {
        if (!UseGestureSprites())
        {
            return;
        }

        int index = Mathf.Clamp(
            Mathf.RoundToInt(gesture01 * (gestureSprites.Length - 1)),
            0,
            gestureSprites.Length - 1
        );
        if (gestureSprites[index] != null)
        {
            spriteRenderer.sprite = gestureSprites[index];
        }
    }

    private float GetAnimationClipLength(string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(clipName))
        {
            return 0f;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name == clipName)
            {
                return clip.length;
            }
        }

        return 0f;
    }

    private static bool HasAnimatorState(Animator targetAnimator, string stateName)
    {
        return targetAnimator != null && !string.IsNullOrEmpty(stateName) && targetAnimator.HasState(0, Animator.StringToHash(stateName));
    }

    private void EnsureGlow()
    {
        if (glowRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("MicroNode_FingertipGlow");
        GameObject glowObject;

        if (existing != null)
        {
            glowObject = existing.gameObject;
        }
        else
        {
            glowObject = new GameObject("MicroNode_FingertipGlow");
            glowObject.transform.SetParent(transform, false);
        }

        glowRoot = glowObject.transform;
        glowRoot.localPosition = fingertipGlowLocalPosition;
        glowRoot.localScale = Vector3.one;

        glowRenderer = glowObject.GetComponent<SpriteRenderer>();
        if (glowRenderer == null)
        {
            glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        }

        glowRenderer.sprite = GetGlowSprite();
        glowRenderer.color = new Color(0f, 1f, 1f, 0f);
        glowRenderer.sortingOrder = sortingOrder;
    }

    private void SetGlowAlpha(float alpha)
    {
        if (glowRenderer == null)
        {
            return;
        }

        Color color = glowRenderer.color;
        color.a = Mathf.Clamp01(alpha);
        glowRenderer.color = color;
    }

    private static Sprite GetGlowSprite()
    {
        if (glowSprite != null)
        {
            return glowSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f);
                texture.SetPixel(x, y, new Color(0.7f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        glowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        glowSprite.name = "Runtime_FingertipGlow";
        return glowSprite;
    }
}
