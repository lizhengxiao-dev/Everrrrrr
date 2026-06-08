using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clear, code-generated energy shields for the push game.
/// Shows a visible crescent/hex shard shield while DefenseDirection is active.
/// 1 = left push, 2 = right push, 3 = powered up/top.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class PushShieldVfx : MonoBehaviour
{
    [Header("Animator")]
    public string defenseDirectionParameter = "DefenseDirection";

    [Header("Placement")]
    public Vector3 leftLocalPosition = new Vector3(-3.85f, 0.55f, -0.05f);
    public Vector3 rightLocalPosition = new Vector3(3.85f, 0.55f, -0.05f);
    public Vector3 topLocalPosition = new Vector3(0f, 2.35f, -0.05f);
    public Vector3 sideScale = new Vector3(1.65f, 2.25f, 1f);
    public Vector3 topScale = new Vector3(1.55f, 1.35f, 1f);

    [Header("Look")]
    public Color shieldColor = new Color(0f, 1f, 1f, 0.78f);
    public Color hotColor = new Color(0.92f, 1f, 1f, 1f);
    public int sortingOrder = 140;
    public float fadeSpeed = 14f;
    public bool showEditorPreview = true;
    public bool forceVisibleForDebug;
    [Range(1, 3)]
    public int editorPreviewDirection = 3;

    [Header("Powered Up Shield")]
    public Sprite poweredUpShieldSprite;
    public Vector3 poweredShieldLocalPosition = new Vector3(0f, 0.48f, -0.06f);
    public Vector3 poweredShieldLocalScale = new Vector3(2.15f, 3.25f, 1f);
    public Color poweredShieldColor = new Color(0.35f, 1f, 1f, 0.42f);
    public Color poweredShieldEdgeColor = new Color(0.88f, 1f, 1f, 0.82f);
    public float poweredPulseSpeed = 4.2f;
    public float poweredPulseAmount = 0.035f;

    private Animator animator;
    private Transform root;
    private Transform poweredRoot;
    private SpriteRenderer[] renderers;
    private LineRenderer[] lineRenderers;
    private SpriteRenderer poweredRenderer;
    private LineRenderer[] poweredLineRenderers;
    private PolygonCollider2D shieldCollider;
    private PolygonCollider2D poweredCollider;
    private Sprite hexSprite;
    private Sprite fallbackPoweredSprite;
    private Material lineMaterial;
    private int currentDirection;
    private float alpha;
    private float poweredAlpha;
    private Coroutine impactRoutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        BuildShield();
        SetAlpha(0f);
        SetPoweredAlpha(0f);
        SetCollider(false);
        SetPoweredCollider(false);
    }

    public void RebuildNow()
    {
        BuildShield();
        SetAlpha(0f);
        SetPoweredAlpha(0f);
        SetCollider(false);
        SetPoweredCollider(false);

        if (!Application.isPlaying && showEditorPreview)
        {
            ShowEditorPreview();
        }
    }

    private void OnEnable()
    {
        animator = GetComponent<Animator>();

        if (root == null || poweredRoot == null)
        {
            BuildShield();
        }

        if (!Application.isPlaying && showEditorPreview)
        {
            ShowEditorPreview();
        }
    }

    private void Update()
    {
        if (root == null || poweredRoot == null)
        {
            BuildShield();
        }

        if (!Application.isPlaying)
        {
            if (showEditorPreview)
            {
                ShowEditorPreview();
            }
            else
            {
                SetAlpha(0f);
                SetPoweredAlpha(0f);
            }

            SetCollider(false);
            SetPoweredCollider(false);
            return;
        }

        int direction = forceVisibleForDebug ? 1 : animator.GetInteger(defenseDirectionParameter);
        bool sideVisible = direction == 1 || direction == 2;
        bool poweredVisible = direction == 3;

        if (direction != currentDirection)
        {
            currentDirection = direction;
            ApplyPose(direction);
        }

        float targetAlpha = sideVisible ? 1f : 0f;
        alpha = Mathf.MoveTowards(alpha, targetAlpha, fadeSpeed * Time.deltaTime);
        SetAlpha(alpha);

        float targetPoweredAlpha = poweredVisible ? 1f : 0f;
        poweredAlpha = Mathf.MoveTowards(poweredAlpha, targetPoweredAlpha, fadeSpeed * Time.deltaTime);
        SetPoweredAlpha(poweredAlpha);

        SetCollider(sideVisible && alpha > 0.45f);
        SetPoweredCollider(poweredVisible && poweredAlpha > 0.35f);
    }

    public void PlayImpact(Vector2 worldPosition)
    {
        if (impactRoutine != null)
        {
            StopCoroutine(impactRoutine);
        }

        impactRoutine = StartCoroutine(ImpactRoutine());
    }

    private void ShowEditorPreview()
    {
        currentDirection = Mathf.Clamp(editorPreviewDirection, 1, 3);
        ApplyPose(currentDirection);

        if (currentDirection == 3)
        {
            SetAlpha(0f);
            SetPoweredAlpha(1f);
            return;
        }

        SetAlpha(0.85f);
        SetPoweredAlpha(0f);
    }

    private void BuildShield()
    {
        Transform oldRoot = transform.Find("PushShield_VFX");
        if (oldRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(oldRoot.gameObject);
            }
            else
            {
                DestroyImmediate(oldRoot.gameObject);
            }
        }

        Transform oldPoweredRoot = transform.Find("PoweredUpShield_VFX");
        if (oldPoweredRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(oldPoweredRoot.gameObject);
            }
            else
            {
                DestroyImmediate(oldPoweredRoot.gameObject);
            }
        }

        GameObject rootObject = new GameObject("PushShield_VFX");
        rootObject.transform.SetParent(transform, false);
        root = rootObject.transform;
        root.localPosition = leftLocalPosition;
        root.localScale = sideScale;

        shieldCollider = rootObject.AddComponent<PolygonCollider2D>();
        shieldCollider.isTrigger = true;
        shieldCollider.pathCount = 1;
        shieldCollider.SetPath(0, CreateShieldPath());
        TrySetTag(rootObject, "Shield");

        renderers = new SpriteRenderer[0];

        List<LineRenderer> lines = new List<LineRenderer>();
        lines.Add(CreateArcLine("Arc_Glow", -0.46f, 1.06f, 0.14f, sortingOrder + 18, 0.28f));
        lines.Add(CreateArcLine("Arc_EdgeHot", -0.46f, 1.02f, 0.07f, sortingOrder + 24, 1.1f));
        lines.Add(CreateArcLine("Arc_Outer", -0.28f, 0.9f, 0.04f, sortingOrder + 25, 0.86f));
        lines.Add(CreateArcLine("Arc_Inner", -0.02f, 0.68f, 0.022f, sortingOrder + 26, 0.55f));

        AddHoneycombCanopy(lines);
        AddUmbrellaRibs(lines);
        lineRenderers = lines.ToArray();

        BuildPoweredShield();
    }

    private void BuildPoweredShield()
    {
        GameObject poweredObject = new GameObject("PoweredUpShield_VFX");
        poweredObject.transform.SetParent(transform, false);
        poweredRoot = poweredObject.transform;
        poweredRoot.localPosition = poweredShieldLocalPosition;
        poweredRoot.localScale = Vector3.one;

        GameObject spriteObject = new GameObject("PoweredUpShield_Sprite");
        spriteObject.transform.SetParent(poweredRoot, false);
        poweredRenderer = spriteObject.AddComponent<SpriteRenderer>();
        poweredRenderer.sprite = poweredUpShieldSprite != null ? poweredUpShieldSprite : GetFallbackPoweredSprite();
        poweredRenderer.color = poweredShieldColor;
        poweredRenderer.sortingOrder = sortingOrder + 8;
        FitPoweredSpriteToRobot();

        poweredCollider = poweredObject.AddComponent<PolygonCollider2D>();
        poweredCollider.isTrigger = true;
        poweredCollider.pathCount = 1;
        poweredCollider.SetPath(0, CreatePoweredShieldPath());
        TrySetTag(poweredObject, "Shield");

        float halfWidth = poweredShieldLocalScale.x * 0.5f;
        float halfHeight = poweredShieldLocalScale.y * 0.5f;

        poweredLineRenderers = new LineRenderer[8];
        poweredLineRenderers[0] = CreatePoweredRing("Powered_RingOuter", halfWidth, halfHeight, 0.028f, sortingOrder + 58, 1f);
        poweredLineRenderers[1] = CreatePoweredRing("Powered_RingMiddle", halfWidth * 0.82f, halfHeight * 0.82f, 0.014f, sortingOrder + 59, 0.58f);
        poweredLineRenderers[2] = CreatePoweredRing("Powered_RingInner", halfWidth * 0.66f, halfHeight * 0.66f, 0.01f, sortingOrder + 60, 0.38f);
        poweredLineRenderers[3] = CreatePoweredVerticalArc("Powered_LeftVeil", -halfWidth * 0.52f, sortingOrder + 61, 0.36f);
        poweredLineRenderers[4] = CreatePoweredVerticalArc("Powered_RightVeil", halfWidth * 0.52f, sortingOrder + 62, 0.36f);
        poweredLineRenderers[5] = CreatePoweredRing("Powered_VisibleDebugOuter", halfWidth * 1.08f, halfHeight * 1.08f, 0.045f, sortingOrder + 80, 1.15f);
        poweredLineRenderers[6] = CreatePoweredRing("Powered_VisibleDebugInner", halfWidth * 0.92f, halfHeight * 0.92f, 0.018f, sortingOrder + 81, 0.72f);
        poweredLineRenderers[7] = CreatePoweredRing("Powered_VisibleDebugCore", halfWidth * 0.48f, halfHeight * 0.48f, 0.01f, sortingOrder + 82, 0.42f);
    }

    private SpriteRenderer CreatePart(string partName, Sprite sprite, Vector3 localPosition, Vector3 localScale, float zRotation, int order)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(root, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = shieldColor;
        renderer.sortingOrder = order;
        return renderer;
    }

    private LineRenderer CreateArcLine(string partName, float xOffset, float height, float width, int order, float brightness)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(root, false);
        part.transform.localPosition = Vector3.zero;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = Vector3.one;

        LineRenderer line = part.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = 128;
        line.widthMultiplier = width;
        line.numCapVertices = 5;
        line.numCornerVertices = 5;
        line.alignment = LineAlignment.View;
        line.material = GetLineMaterial();
        line.sortingOrder = order;

        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            float angle = Mathf.Lerp(-78f, 78f, t) * Mathf.Deg2Rad;
            float x = xOffset - Mathf.Cos(angle) * 0.44f;
            float y = Mathf.Sin(angle) * height;
            line.SetPosition(i, new Vector3(x, y, -0.02f));
        }

        ApplyArcGradient(line, brightness);
        return line;
    }

    private void AddHoneycombCanopy(List<LineRenderer> lines)
    {
        int index = 0;

        for (int row = -5; row <= 5; row++)
        {
            float y = row * 0.155f;
            float absY = Mathf.Abs(y);
            float rowOffset = row % 2 == 0 ? 0f : 0.092f;

            for (int col = 0; col < 6; col++)
            {
                float x = -0.52f + col * 0.175f + rowOffset;
                float innerLimit = 0.28f - absY * 0.17f;
                float outerLimit = -0.68f + absY * 0.08f;

                if (x < outerLimit || x > innerLimit)
                {
                    continue;
                }

                float centerBias = 1f - Mathf.Clamp01(absY / 0.86f);
                float brightness = Mathf.Lerp(0.48f, 0.86f, centerBias);
                float radius = Mathf.Lerp(0.064f, 0.082f, centerBias);

                lines.Add(CreateHexLine(
                    "HexCell_" + index.ToString("00"),
                    new Vector3(x, y, -0.03f),
                    radius,
                    sortingOrder + 32 + index,
                    brightness
                ));
                index++;
            }
        }
    }

    private void AddUmbrellaRibs(List<LineRenderer> lines)
    {
        lines.Add(CreateEnergyRib("Rib_TopEdge", -0.76f, 0.76f, 0.16f, 0.48f, sortingOrder + 72, 0.64f));
        lines.Add(CreateEnergyRib("Rib_TopMid", -0.7f, 0.5f, 0.22f, 0.32f, sortingOrder + 73, 0.55f));
        lines.Add(CreateEnergyRib("Rib_UpperCenter", -0.66f, 0.26f, 0.26f, 0.16f, sortingOrder + 74, 0.48f));
        lines.Add(CreateEnergyRib("Rib_Horizon", -0.74f, 0f, 0.3f, 0f, sortingOrder + 75, 0.46f));
        lines.Add(CreateEnergyRib("Rib_LowerCenter", -0.66f, -0.26f, 0.26f, -0.16f, sortingOrder + 76, 0.48f));
        lines.Add(CreateEnergyRib("Rib_BottomMid", -0.7f, -0.5f, 0.22f, -0.32f, sortingOrder + 77, 0.55f));
        lines.Add(CreateEnergyRib("Rib_BottomEdge", -0.76f, -0.76f, 0.16f, -0.48f, sortingOrder + 78, 0.64f));
    }

    private LineRenderer CreateEnergyRib(
        string partName,
        float startX,
        float startY,
        float endX,
        float endY,
        int order,
        float brightness)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(root, false);
        part.transform.localPosition = Vector3.zero;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = Vector3.one;

        LineRenderer line = part.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = 24;
        line.widthMultiplier = 0.012f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.material = GetLineMaterial();
        line.sortingOrder = order;

        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            float curve = Mathf.Sin(t * Mathf.PI) * 0.035f * Mathf.Sign(startY - endY);
            float x = Mathf.Lerp(startX, endX, t);
            float y = Mathf.Lerp(startY, endY, t) + curve;
            line.SetPosition(i, new Vector3(x, y, -0.03f));
        }

        Color color = shieldColor;
        color.a *= brightness;
        line.startColor = color;
        line.endColor = color;
        return line;
    }

    private LineRenderer CreateHexLine(string partName, Vector3 localPosition, float radius, int order, float brightness = 0.65f)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(root, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = Vector3.one;

        LineRenderer line = part.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 6;
        line.widthMultiplier = 0.009f;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.material = GetLineMaterial();
        line.sortingOrder = order;

        for (int i = 0; i < 6; i++)
        {
            float angle = (60f * i + 30f) * Mathf.Deg2Rad;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.03f));
        }

        Color outlineColor = hotColor;
        outlineColor.a *= brightness;
        line.startColor = outlineColor;
        line.endColor = outlineColor;
        return line;
    }

    private LineRenderer CreatePoweredRing(string partName, float radiusX, float radiusY, float width, int order, float brightness)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(poweredRoot, false);
        part.transform.localPosition = Vector3.zero;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = Vector3.one;

        LineRenderer line = part.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 160;
        line.widthMultiplier = width;
        line.numCapVertices = 5;
        line.numCornerVertices = 5;
        line.alignment = LineAlignment.View;
        line.material = GetLineMaterial();
        line.sortingOrder = order;

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i / (float)line.positionCount * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, -0.04f));
        }

        ApplyPoweredGradient(line, brightness, 1f);
        return line;
    }

    private LineRenderer CreatePoweredVerticalArc(string partName, float xOffset, int order, float brightness)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(poweredRoot, false);
        part.transform.localPosition = Vector3.zero;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = Vector3.one;

        LineRenderer line = part.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = 96;
        line.widthMultiplier = 0.009f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.material = GetLineMaterial();
        line.sortingOrder = order;

        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            float y = Mathf.Lerp(-poweredShieldLocalScale.y * 0.46f, poweredShieldLocalScale.y * 0.46f, t);
            float x = xOffset * Mathf.Sin(t * Mathf.PI);
            line.SetPosition(i, new Vector3(x, y, -0.05f));
        }

        ApplyPoweredGradient(line, brightness, 1f);
        return line;
    }

    private Material GetLineMaterial()
    {
        if (lineMaterial != null)
        {
            return lineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Diffuse");
        }

        lineMaterial = new Material(shader);
        lineMaterial.name = "RuntimePushShieldLine";
        return lineMaterial;
    }

    private void ApplyPose(int direction)
    {
        if (direction == 1)
        {
            root.localPosition = leftLocalPosition;
            root.localScale = sideScale;
            root.localRotation = Quaternion.Euler(0f, 0f, 0f);
            return;
        }

        if (direction == 2)
        {
            root.localPosition = rightLocalPosition;
            root.localScale = new Vector3(-sideScale.x, sideScale.y, sideScale.z);
            root.localRotation = Quaternion.Euler(0f, 0f, 0f);
            return;
        }

        if (direction == 3)
        {
            poweredRoot.localPosition = poweredShieldLocalPosition;
            poweredRoot.localScale = Vector3.one;
            poweredRoot.localRotation = Quaternion.identity;
        }
    }

    private IEnumerator ImpactRoutine()
    {
        Transform activeRoot = currentDirection == 3 && poweredRoot != null ? poweredRoot : root;
        Vector3 basePosition = activeRoot.localPosition;
        float elapsed = 0f;
        float duration = 0.22f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float fade = 1f - t;
            Vector2 jitter = Random.insideUnitCircle * 0.09f * fade;
            activeRoot.localPosition = basePosition + new Vector3(jitter.x, jitter.y, 0f);

            if (currentDirection == 3)
            {
                SetPoweredAlpha(Mathf.Lerp(1f, poweredAlpha, t), Color.Lerp(hotColor, poweredShieldColor, t));
            }
            else
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].color = Color.Lerp(hotColor, shieldColor, t) * new Color(1f, 1f, 1f, alpha);
                }

                SetLineAlpha(Mathf.Lerp(1f, alpha, t), Color.Lerp(hotColor, shieldColor, t));
            }

            yield return null;
        }

        activeRoot.localPosition = basePosition;
        impactRoutine = null;
    }

    private void SetAlpha(float value)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            Color color = i == 0 ? shieldColor : Color.Lerp(shieldColor, hotColor, 0.35f);
            color.a *= value;
            renderers[i].color = color;
            renderers[i].enabled = value > 0.01f;
        }

        SetLineAlpha(value, shieldColor);
    }

    private void SetPoweredAlpha(float value)
    {
        SetPoweredAlpha(value, poweredShieldColor);
    }

    private void SetPoweredAlpha(float value, Color baseColor)
    {
        if (poweredRoot == null)
        {
            return;
        }

        float pulse = Application.isPlaying
            ? 1f + Mathf.Sin(Time.time * poweredPulseSpeed) * poweredPulseAmount
            : 1f;
        poweredRoot.localScale = new Vector3(pulse, pulse, 1f);

        if (poweredRenderer != null)
        {
            Color color = baseColor;
            color.a = poweredShieldColor.a * value;
            poweredRenderer.color = color;
            poweredRenderer.enabled = value > 0.01f;
        }

        if (poweredLineRenderers == null)
        {
            return;
        }

        for (int i = 0; i < poweredLineRenderers.Length; i++)
        {
            if (poweredLineRenderers[i] == null)
            {
                continue;
            }

            float shimmer = Application.isPlaying ? 1f + Mathf.Sin(Time.time * 5.5f + i * 0.8f) * 0.1f : 1f;
            ApplyPoweredGradient(poweredLineRenderers[i], i == 0 ? 1f : 0.55f, value * shimmer);
            poweredLineRenderers[i].enabled = value > 0.01f;
        }
    }

    private void SetLineAlpha(float value, Color baseColor)
    {
        if (lineRenderers == null)
        {
            return;
        }

        for (int i = 0; i < lineRenderers.Length; i++)
        {
            if (lineRenderers[i] == null)
            {
                continue;
            }

            string lineName = lineRenderers[i].gameObject.name;
            float brightness = GetLineBrightness(lineName);
            float shimmer = Application.isPlaying ? 1f + Mathf.Sin(Time.time * 7.5f + i * 0.43f) * 0.08f : 1f;

            if (lineName.StartsWith("Arc_"))
            {
                ApplyArcGradient(lineRenderers[i], brightness * shimmer, value);
            }
            else if (lineName.StartsWith("Rib_"))
            {
                ApplySoftLineGradient(lineRenderers[i], brightness * shimmer, value);
            }
            else
            {
                Color lineColor = Color.Lerp(baseColor, hotColor, 0.25f);
                lineColor.a = Mathf.Clamp01(baseColor.a * value * brightness * shimmer);
                lineRenderers[i].startColor = lineColor;
                lineRenderers[i].endColor = lineColor;
            }

            lineRenderers[i].enabled = value > 0.01f;
        }
    }

    private float GetLineBrightness(string lineName)
    {
        if (lineName.Contains("Glow"))
        {
            return 0.28f;
        }

        if (lineName.Contains("EdgeHot"))
        {
            return 1.1f;
        }

        if (lineName.Contains("Outer"))
        {
            return 0.86f;
        }

        if (lineName.Contains("Inner"))
        {
            return 0.55f;
        }

        if (lineName.StartsWith("Rib_"))
        {
            return 0.48f;
        }

        return 0.68f;
    }

    private void ApplyArcGradient(LineRenderer line, float brightness, float alphaMultiplier = 1f)
    {
        Gradient gradient = new Gradient();
        Color outer = hotColor;
        Color middle = shieldColor;
        Color inner = shieldColor;
        outer.a = Mathf.Clamp01(hotColor.a * brightness * alphaMultiplier);
        middle.a = Mathf.Clamp01(shieldColor.a * brightness * 0.85f * alphaMultiplier);
        inner.a = Mathf.Clamp01(shieldColor.a * brightness * 0.28f * alphaMultiplier);

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(middle, 0f),
                new GradientColorKey(outer, 0.5f),
                new GradientColorKey(inner, 1f),
            },
            new[]
            {
                new GradientAlphaKey(middle.a, 0f),
                new GradientAlphaKey(outer.a, 0.5f),
                new GradientAlphaKey(inner.a, 1f),
            }
        );
        line.colorGradient = gradient;
    }

    private void ApplySoftLineGradient(LineRenderer line, float brightness, float alphaMultiplier)
    {
        Gradient gradient = new Gradient();
        Color edge = shieldColor;
        Color middle = hotColor;
        edge.a = Mathf.Clamp01(shieldColor.a * brightness * 0.25f * alphaMultiplier);
        middle.a = Mathf.Clamp01(hotColor.a * brightness * 0.72f * alphaMultiplier);

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(edge, 0f),
                new GradientColorKey(middle, 0.5f),
                new GradientColorKey(edge, 1f),
            },
            new[]
            {
                new GradientAlphaKey(edge.a, 0f),
                new GradientAlphaKey(middle.a, 0.5f),
                new GradientAlphaKey(edge.a, 1f),
            }
        );
        line.colorGradient = gradient;
    }

    private void ApplyPoweredGradient(LineRenderer line, float brightness, float alphaMultiplier)
    {
        Gradient gradient = new Gradient();
        Color edge = poweredShieldEdgeColor;
        Color center = hotColor;
        edge.a = Mathf.Clamp01(poweredShieldEdgeColor.a * brightness * alphaMultiplier);
        center.a = Mathf.Clamp01(hotColor.a * brightness * 0.42f * alphaMultiplier);

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(edge, 0f),
                new GradientColorKey(center, 0.5f),
                new GradientColorKey(edge, 1f),
            },
            new[]
            {
                new GradientAlphaKey(edge.a, 0f),
                new GradientAlphaKey(center.a, 0.5f),
                new GradientAlphaKey(edge.a, 1f),
            }
        );
        line.colorGradient = gradient;
    }

    private void SetCollider(bool enabled)
    {
        if (shieldCollider != null)
        {
            shieldCollider.enabled = enabled;
        }
    }

    private void SetPoweredCollider(bool enabled)
    {
        if (poweredCollider != null)
        {
            poweredCollider.enabled = enabled;
        }
    }

    private void FitPoweredSpriteToRobot()
    {
        if (poweredRenderer == null || poweredRenderer.sprite == null)
        {
            return;
        }

        Vector2 spriteSize = poweredRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            poweredRenderer.transform.localScale = Vector3.one;
            return;
        }

        poweredRenderer.transform.localScale = new Vector3(
            poweredShieldLocalScale.x / spriteSize.x,
            poweredShieldLocalScale.y / spriteSize.y,
            1f
        );
    }

    private static Vector2[] CreateShieldPath()
    {
        return new[]
        {
            new Vector2(-0.52f, 0.76f),
            new Vector2(0.46f, 0.55f),
            new Vector2(0.62f, 0f),
            new Vector2(0.46f, -0.55f),
            new Vector2(-0.52f, -0.76f),
            new Vector2(-0.22f, -0.38f),
            new Vector2(-0.12f, 0f),
            new Vector2(-0.22f, 0.38f),
        };
    }

    private Vector2[] CreatePoweredShieldPath()
    {
        Vector2[] points = new Vector2[28];
        float halfWidth = poweredShieldLocalScale.x * 0.5f;
        float halfHeight = poweredShieldLocalScale.y * 0.5f;

        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.PI * 2f;
            points[i] = new Vector2(
                Mathf.Cos(angle) * halfWidth,
                Mathf.Sin(angle) * halfHeight
            );
        }

        return points;
    }

    private Sprite GetFallbackPoweredSprite()
    {
        if (fallbackPoweredSprite != null)
        {
            return fallbackPoweredSprite;
        }

        Texture2D texture = new Texture2D(256, 320, TextureFormat.RGBA32, false);
        texture.name = "RuntimePoweredUpShieldFallback";
        Vector2 center = new Vector2(texture.width * 0.5f, texture.height * 0.5f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                Vector2 p = new Vector2(x, y) - center;
                float nx = p.x / 112f;
                float ny = p.y / 148f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float edge = 1f - Mathf.SmoothStep(0.92f, 1f, d);
                float hollow = Mathf.SmoothStep(0.72f, 0.92f, d);
                float mist = (1f - Mathf.SmoothStep(0f, 0.85f, d)) * 0.12f;
                float alpha = Mathf.Clamp01(edge * hollow + mist);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        fallbackPoweredSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        return fallbackPoweredSprite;
    }

    private Sprite CreateHexSprite()
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.name = "RuntimePushShieldHex";

        Vector2 center = new Vector2(31.5f, 31.5f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                Vector2 p = new Vector2(x, y) - center;
                float qx = Mathf.Abs(p.x);
                float qy = Mathf.Abs(p.y);
                float d = Mathf.Max(qx * 0.866f + qy * 0.5f, qy) / 27f;
                float outerEdge = 1f - Mathf.SmoothStep(0.9f, 1f, d);
                float innerCut = Mathf.SmoothStep(0.72f, 0.82f, d);
                float alpha = Mathf.Clamp01(outerEdge * innerCut * 0.75f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);
    }

    private void TrySetTag(GameObject target, string tagName)
    {
        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
            Debug.LogWarning("Tag '" + tagName + "' does not exist yet. Create it in Unity Tags if shield collisions fail.", target);
        }
    }
}
