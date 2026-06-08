using System.Collections;
using UnityEngine;

/// <summary>
/// Dynamic "Assemble Matrix" shield controller.
/// Uses one active Shield-tagged matrix made from many small shards, instead of three flat static shields.
/// DefenseDirection: 0 = Idle, 1 = Left, 2 = Right, 3 = Top.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ShieldManager : MonoBehaviour
{
    [Header("Animator")]
    public string defenseDirectionParameter = "DefenseDirection";

    [Header("Optional Art")]
    [Tooltip("Optional custom prefab. If empty, the script builds AssembleShield_Matrix automatically.")]
    public GameObject assembleShieldPrefab;

    [Tooltip("Optional shard sprite. If empty, cyber hex shards are generated at runtime.")]
    public Sprite shardSprite;

    [Header("Readable Rehab Shield Placement")]
    public Vector3 leftShieldLocalPosition = new Vector3(-1.55f, 0.46f, 0f);
    public Vector3 rightShieldLocalPosition = new Vector3(1.55f, 0.46f, 0f);
    public Vector3 topShieldLocalPosition = new Vector3(0f, 2.05f, 0f);
    public Vector3 sideShieldLocalScale = new Vector3(1.7f, 2.35f, 1f);
    public Vector3 topShieldLocalScale = new Vector3(1.95f, 2.8f, 1f);

    [Header("Assemble Motion")]
    [Tooltip("Commercial-feel snap assembly. 0.10 = 100 milliseconds.")]
    public float assembleDuration = 0.1f;

    public float dissipateDuration = 0.13f;
    public float impactRattleDuration = 0.16f;
    public float impactRattleStrength = 0.09f;
    public Color cyanCoreColor = new Color(0f, 1f, 1f, 0.95f);
    public Color whiteHotColor = new Color(1f, 1f, 1f, 1f);
    public int sortingOrder = 95;

    private readonly Vector3[] assembledShardPositions =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(-0.34f, 0f, 0f),
        new Vector3(0.34f, 0f, 0f),
        new Vector3(-0.17f, 0.30f, 0f),
        new Vector3(0.17f, 0.30f, 0f),
        new Vector3(-0.17f, -0.30f, 0f),
        new Vector3(0.17f, -0.30f, 0f),
        new Vector3(-0.52f, 0.30f, 0f),
        new Vector3(0.52f, 0.30f, 0f),
        new Vector3(-0.52f, -0.30f, 0f),
        new Vector3(0.52f, -0.30f, 0f),
        new Vector3(0f, 0.60f, 0f),
        new Vector3(0f, -0.60f, 0f),
        new Vector3(-0.69f, 0f, 0f),
        new Vector3(0.69f, 0f, 0f),
    };

    private Animator animator;
    private GameObject shieldMatrix;
    private PolygonCollider2D shieldCollider;
    private Transform[] shards;
    private SpriteRenderer[] shardRenderers;
    private Vector3[] lastStableShardPositions;
    private Vector3[] lastStableShardScales;
    private Quaternion[] lastStableShardRotations;
    private Coroutine motionRoutine;
    private Sprite runtimeHexShardSprite;
    private Sprite runtimeTriangleShardSprite;
    private int currentDirection;
    private float suppressReassembleUntil;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        ForceReadableDefaults();
        EnsureRobotBodyCollider();
        DisableLegacyStaticShields();
        EnsureShieldMatrix();
        HideMatrixInstantly();
    }

    private void Update()
    {
        int requestedDirection = animator.GetInteger(defenseDirectionParameter);

        if (Time.time < suppressReassembleUntil && requestedDirection != 0)
        {
            return;
        }

        if (requestedDirection == currentDirection)
        {
            return;
        }

        currentDirection = requestedDirection;

        if (currentDirection == 0)
        {
            StartMotion(DissipateRoutine());
            return;
        }

        StartMotion(AssembleRoutine(currentDirection));
    }

    public void RegisterShieldImpact(Vector2 impactWorldPosition)
    {
        if (shieldMatrix == null || !shieldMatrix.activeInHierarchy)
        {
            return;
        }

        suppressReassembleUntil = Time.time + 0.25f;
        currentDirection = 0;
        StartMotion(ImpactThenDissipateRoutine(impactWorldPosition));
    }

    private void ForceReadableDefaults()
    {
        // These values are intentionally forced so older serialized "tiny shield" settings cannot survive.
        leftShieldLocalPosition = new Vector3(-1.72f, 0.52f, 0f);
        rightShieldLocalPosition = new Vector3(1.55f, 0.46f, 0f);
        topShieldLocalPosition = new Vector3(0f, 2.05f, 0f);
        sideShieldLocalScale = new Vector3(1.08f, 1.62f, 1f);
        topShieldLocalScale = new Vector3(1.95f, 2.8f, 1f);
        assembleDuration = 0.1f;
        dissipateDuration = 0.13f;
        impactRattleDuration = 0.2f;
        impactRattleStrength = 0.13f;
        sortingOrder = 95;
        cyanCoreColor = new Color(0f, 1f, 1f, 0.95f);
        whiteHotColor = new Color(1f, 1f, 1f, 1f);
    }

    private void EnsureShieldMatrix()
    {
        Transform existing = transform.Find("AssembleShield_Matrix");

        if (existing != null)
        {
            shieldMatrix = existing.gameObject;
        }
        else if (assembleShieldPrefab != null)
        {
            shieldMatrix = Instantiate(assembleShieldPrefab, transform);
            shieldMatrix.name = "AssembleShield_Matrix";
        }
        else
        {
            shieldMatrix = new GameObject("AssembleShield_Matrix");
            shieldMatrix.transform.SetParent(transform, false);
        }

        shieldMatrix.transform.localPosition = Vector3.zero;
        shieldMatrix.transform.localRotation = Quaternion.identity;
        shieldMatrix.transform.localScale = Vector3.one;
        TrySetTag(shieldMatrix, "Shield");

        shieldCollider = shieldMatrix.GetComponent<PolygonCollider2D>();
        if (shieldCollider == null)
        {
            shieldCollider = shieldMatrix.AddComponent<PolygonCollider2D>();
        }

        shieldCollider.isTrigger = true;
        shieldCollider.pathCount = 1;
        shieldCollider.SetPath(0, CreateHexPath(0.82f));
        shieldCollider.enabled = false;

        BuildRuntimeShards();
    }

    private void BuildRuntimeShards()
    {
        int shardCount = assembledShardPositions.Length;
        shards = new Transform[shardCount];
        shardRenderers = new SpriteRenderer[shardCount];
        lastStableShardPositions = new Vector3[shardCount];
        lastStableShardScales = new Vector3[shardCount];
        lastStableShardRotations = new Quaternion[shardCount];

        for (int i = 0; i < shardCount; i++)
        {
            string shardName = "MatrixShard_" + i.ToString("00");
            Transform shard = shieldMatrix.transform.Find(shardName);

            if (shard == null)
            {
                GameObject shardObject = new GameObject(shardName);
                shardObject.transform.SetParent(shieldMatrix.transform, false);
                shard = shardObject.transform;
            }

            SpriteRenderer renderer = shard.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = shard.gameObject.AddComponent<SpriteRenderer>();
            }

            bool triangle = i >= 7 && i % 2 == 0;
            renderer.sprite = shardSprite != null ? shardSprite : (triangle ? GetRuntimeTriangleShardSprite() : GetRuntimeHexShardSprite());
            renderer.color = new Color(cyanCoreColor.r, cyanCoreColor.g, cyanCoreColor.b, 0f);
            renderer.sortingOrder = sortingOrder + (i % 4);
            renderer.enabled = true;

            float scale = i == 0 ? 0.42f : 0.31f;
            shard.localPosition = assembledShardPositions[i];
            shard.localRotation = Quaternion.Euler(0f, 0f, i * 17f);
            shard.localScale = new Vector3(scale, scale, 1f);

            shards[i] = shard;
            shardRenderers[i] = renderer;
            lastStableShardPositions[i] = shard.localPosition;
            lastStableShardScales[i] = shard.localScale;
            lastStableShardRotations[i] = shard.localRotation;
        }
    }

    private IEnumerator AssembleRoutine(int direction)
    {
        shieldMatrix.SetActive(true);
        shieldCollider.enabled = true;
        ConfigureMatrixPose(direction);

        Vector3 sourceLocal = GetAssemblySourceLocal(direction);
        Vector3 sourceWorld = transform.TransformPoint(sourceLocal);
        Vector3 sourceInMatrix = shieldMatrix.transform.InverseTransformPoint(sourceWorld);

        Vector3[] startPositions = new Vector3[shards.Length];
        Vector3[] startScales = new Vector3[shards.Length];
        Quaternion[] startRotations = new Quaternion[shards.Length];

        for (int i = 0; i < shards.Length; i++)
        {
            Vector2 jitter = Random.insideUnitCircle * 0.22f;
            startPositions[i] = sourceInMatrix + new Vector3(jitter.x, jitter.y, 0f);
            startScales[i] = Vector3.one * 0.04f;
            startRotations[i] = Quaternion.Euler(0f, 0f, Random.Range(-180f, 180f));

            shards[i].localPosition = startPositions[i];
            shards[i].localScale = startScales[i];
            shards[i].localRotation = startRotations[i];
            SetRendererAlpha(shardRenderers[i], 0f);
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, assembleDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t);

            for (int i = 0; i < shards.Length; i++)
            {
                Vector3 targetScale = GetAssembledShardScale(i);
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, i * 17f);

                shards[i].localPosition = Vector3.LerpUnclamped(startPositions[i], assembledShardPositions[i], eased);
                shards[i].localScale = Vector3.LerpUnclamped(startScales[i], targetScale, eased);
                shards[i].localRotation = Quaternion.Slerp(startRotations[i], targetRotation, t);
                shardRenderers[i].color = Color.Lerp(new Color(0f, 1f, 1f, 0f), cyanCoreColor, t);

                lastStableShardPositions[i] = assembledShardPositions[i];
                lastStableShardScales[i] = targetScale;
                lastStableShardRotations[i] = targetRotation;
            }

            yield return null;
        }

        SnapToAssembled();
    }

    private IEnumerator DissipateRoutine()
    {
        shieldCollider.enabled = false;

        Vector3[] startPositions = CapturePositions();
        Vector3[] startScales = CaptureScales();
        Quaternion[] startRotations = CaptureRotations();
        Vector3[] endPositions = new Vector3[shards.Length];

        for (int i = 0; i < shards.Length; i++)
        {
            Vector2 outward = Random.insideUnitCircle.normalized;
            if (outward.sqrMagnitude < 0.01f)
            {
                outward = Vector2.up;
            }

            endPositions[i] = startPositions[i] + new Vector3(outward.x, outward.y, 0f) * Random.Range(0.45f, 0.9f);
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, dissipateDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;

            for (int i = 0; i < shards.Length; i++)
            {
                shards[i].localPosition = Vector3.Lerp(startPositions[i], endPositions[i], eased);
                shards[i].localScale = Vector3.Lerp(startScales[i], Vector3.one * 0.02f, eased);
                shards[i].localRotation = Quaternion.Slerp(startRotations[i], Quaternion.Euler(0f, 0f, Random.Range(-90f, 90f)), eased);
                SetRendererAlpha(shardRenderers[i], Mathf.Lerp(1f, 0f, t));
            }

            yield return null;
        }

        HideMatrixInstantly();
    }

    private IEnumerator ImpactThenDissipateRoutine(Vector2 impactWorldPosition)
    {
        shieldCollider.enabled = false;
        SnapToAssembled();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, impactRattleDuration);
        Vector3 localImpact = shieldMatrix.transform.InverseTransformPoint(impactWorldPosition);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float fade = 1f - t;

            for (int i = 0; i < shards.Length; i++)
            {
                Vector3 awayFromImpact = (assembledShardPositions[i] - localImpact).normalized;
                Vector2 randomShake = Random.insideUnitCircle * impactRattleStrength * fade;
                Vector3 directionalKick = awayFromImpact * impactRattleStrength * 0.65f * Mathf.Sin(t * Mathf.PI * 10f);

                shards[i].localPosition = assembledShardPositions[i] + directionalKick + new Vector3(randomShake.x, randomShake.y, 0f);
                shards[i].localScale = GetAssembledShardScale(i) * Mathf.Lerp(1.12f, 1f, t);
                shards[i].localRotation = Quaternion.Euler(0f, 0f, i * 17f + Random.Range(-13f, 13f) * fade);
                shardRenderers[i].color = Color.Lerp(whiteHotColor, cyanCoreColor, t);
            }

            yield return null;
        }

        yield return DissipateRoutine();
    }

    private void StartMotion(IEnumerator routine)
    {
        if (motionRoutine != null)
        {
            StopCoroutine(motionRoutine);
        }

        motionRoutine = StartCoroutine(routine);
    }

    private void ConfigureMatrixPose(int direction)
    {
        if (direction == 1)
        {
            shieldMatrix.transform.localPosition = leftShieldLocalPosition;
            shieldMatrix.transform.localRotation = Quaternion.Euler(0f, 0f, 7f);
            shieldMatrix.transform.localScale = sideShieldLocalScale;
            return;
        }

        if (direction == 2)
        {
            shieldMatrix.transform.localPosition = rightShieldLocalPosition;
            shieldMatrix.transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            shieldMatrix.transform.localScale = sideShieldLocalScale;
            return;
        }

        shieldMatrix.transform.localPosition = topShieldLocalPosition;
        shieldMatrix.transform.localRotation = Quaternion.identity;
        shieldMatrix.transform.localScale = topShieldLocalScale;
    }

    private Vector3 GetAssemblySourceLocal(int direction)
    {
        if (direction == 1)
        {
            return new Vector3(-0.48f, 0.62f, 0f);
        }

        if (direction == 2)
        {
            return new Vector3(0.48f, 0.62f, 0f);
        }

        return new Vector3(0f, 1.05f, 0f);
    }

    private void SnapToAssembled()
    {
        for (int i = 0; i < shards.Length; i++)
        {
            Vector3 targetScale = GetAssembledShardScale(i);
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, i * 17f);

            shards[i].localPosition = assembledShardPositions[i];
            shards[i].localScale = targetScale;
            shards[i].localRotation = targetRotation;
            shardRenderers[i].color = cyanCoreColor;

            lastStableShardPositions[i] = shards[i].localPosition;
            lastStableShardScales[i] = targetScale;
            lastStableShardRotations[i] = targetRotation;
        }
    }

    private void HideMatrixInstantly()
    {
        if (shieldCollider != null)
        {
            shieldCollider.enabled = false;
        }

        if (shieldMatrix != null)
        {
            shieldMatrix.SetActive(true);
        }

        if (shards == null)
        {
            return;
        }

        for (int i = 0; i < shards.Length; i++)
        {
            shards[i].localPosition = assembledShardPositions[i];
            shards[i].localScale = Vector3.one * 0.02f;
            SetRendererAlpha(shardRenderers[i], 0f);
        }
    }

    private Vector3 GetAssembledShardScale(int index)
    {
        float scale = index == 0 ? 0.44f : 0.32f;
        if (index >= 11)
        {
            scale = 0.27f;
        }

        return new Vector3(scale, scale, 1f);
    }

    private Vector3[] CapturePositions()
    {
        Vector3[] values = new Vector3[shards.Length];
        for (int i = 0; i < shards.Length; i++)
        {
            values[i] = shards[i].localPosition;
        }

        return values;
    }

    private Vector3[] CaptureScales()
    {
        Vector3[] values = new Vector3[shards.Length];
        for (int i = 0; i < shards.Length; i++)
        {
            values[i] = shards[i].localScale;
        }

        return values;
    }

    private Quaternion[] CaptureRotations()
    {
        Quaternion[] values = new Quaternion[shards.Length];
        for (int i = 0; i < shards.Length; i++)
        {
            values[i] = shards[i].localRotation;
        }

        return values;
    }

    private void SetRendererAlpha(SpriteRenderer renderer, float alpha)
    {
        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }

    private void EnsureRobotBodyCollider()
    {
        TrySetTag(gameObject, "Player");

        Collider2D bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider == null)
        {
            CapsuleCollider2D capsule = gameObject.AddComponent<CapsuleCollider2D>();
            capsule.isTrigger = true;
            capsule.size = new Vector2(1.25f, 2.15f);
            capsule.offset = new Vector2(0f, 0.45f);
            return;
        }

        bodyCollider.isTrigger = true;
    }

    private void DisableLegacyStaticShields()
    {
        DisableLegacyShield("Shield_Left");
        DisableLegacyShield("Shield_Right");
        DisableLegacyShield("Shield_Top");
    }

    private void DisableLegacyShield(string shieldName)
    {
        Transform legacy = transform.Find(shieldName);
        if (legacy == null)
        {
            return;
        }

        foreach (Collider2D legacyCollider in legacy.GetComponentsInChildren<Collider2D>(true))
        {
            legacyCollider.enabled = false;
        }

        foreach (SpriteRenderer legacyRenderer in legacy.GetComponentsInChildren<SpriteRenderer>(true))
        {
            legacyRenderer.enabled = false;
        }

        legacy.gameObject.SetActive(false);
    }

    private void TrySetTag(GameObject target, string tagName)
    {
        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
            Debug.LogWarning("Tag '" + tagName + "' does not exist yet. Create it in Unity Tags if collision filtering fails.", target);
        }
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private Vector2[] CreateHexPath(float radius)
    {
        Vector2[] points = new Vector2[6];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i + 30f);
            points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        return points;
    }

    private Sprite GetRuntimeHexShardSprite()
    {
        if (runtimeHexShardSprite != null)
        {
            return runtimeHexShardSprite;
        }

        runtimeHexShardSprite = CreateRuntimeShardSprite(false);
        return runtimeHexShardSprite;
    }

    private Sprite GetRuntimeTriangleShardSprite()
    {
        if (runtimeTriangleShardSprite != null)
        {
            return runtimeTriangleShardSprite;
        }

        runtimeTriangleShardSprite = CreateRuntimeShardSprite(true);
        return runtimeTriangleShardSprite;
    }

    private Sprite CreateRuntimeShardSprite(bool triangle)
    {
        const int size = 160;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 fill = new Color32(255, 255, 255, 120);
        Color32 line = new Color32(255, 255, 255, 255);
        Color32 scan = new Color32(255, 255, 255, 70);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Vector2[] polygon = triangle
            ? new[] { center + new Vector2(0f, 58f), center + new Vector2(-55f, -42f), center + new Vector2(55f, -42f) }
            : CreatePixelHex(center, 58f);

        FillPolygon(texture, polygon, fill);

        for (int y = 42; y <= 116; y += 14)
        {
            DrawLine(texture, new Vector2(42f, y), new Vector2(118f, y), scan, 1);
        }

        DrawPolygon(texture, polygon, line, 4);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 256f);
    }

    private Vector2[] CreatePixelHex(Vector2 center, float radius)
    {
        Vector2[] points = new Vector2[6];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i + 30f);
            points[i] = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        return points;
    }

    private void FillPolygon(Texture2D texture, Vector2[] polygon, Color32 color)
    {
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                if (PointInPolygon(new Vector2(x, y), polygon))
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private void DrawPolygon(Texture2D texture, Vector2[] points, Color32 color, int thickness)
    {
        for (int i = 0; i < points.Length; i++)
        {
            DrawLine(texture, points[i], points[(i + 1) % points.Length], color, thickness);
        }
    }

    private void DrawLine(Texture2D texture, Vector2 start, Vector2 end, Color32 color, int thickness)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(start, end));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(start, end, i / (float)steps);
            PaintPixel(texture, Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), color, thickness);
        }
    }

    private void PaintPixel(Texture2D texture, int x, int y, Color32 color, int radius)
    {
        for (int offsetY = -radius; offsetY <= radius; offsetY++)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                int px = x + offsetX;
                int py = y + offsetY;
                if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                {
                    texture.SetPixel(px, py, color);
                }
            }
        }
    }

    private bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            bool intersects = ((polygon[i].y > point.y) != (polygon[j].y > point.y))
                && (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x);

            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
