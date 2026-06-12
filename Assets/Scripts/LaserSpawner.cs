using System.Collections;
using UnityEngine;

/// <summary>
/// Repeatedly chooses a random cannon and spawns one laser on a strict interval.
/// Official Team EchoCare timing: one laser begins charging every 5 seconds.
/// </summary>
public class LaserSpawner : MonoBehaviour
{
    private const string FemaleRobotName = "Female0001";
    private const string MaleRobotName = "Male0001";
    private const string CannonLeftName = "Cannon_Left";
    private const string CannonRightName = "Cannon_Right";
    private const string CannonTopName = "Cannon_Top";

    private static readonly Vector3 GameplayRobotPosition = new Vector3(0.1f, -0.92f, 0f);
    private static readonly Vector3 GameplayRobotScale = new Vector3(0.7f, 0.435f, 1f);
    private const float RobotChestOffsetY = 0.75f;
    private const float CannonEdgePadding = 0.18f;

    [Header("Scene References")]
    [Tooltip("Laser prefab created from the hierarchy.")]
    public LaserBehavior laserPrefab;

    [Tooltip("Robot target. Drag Female0001 or Male0001 here.")]
    public Transform robotTarget;

    [Tooltip("Left cannon transform.")]
    public Transform cannonLeft;

    [Tooltip("Right cannon transform.")]
    public Transform cannonRight;

    [Tooltip("Top cannon transform.")]
    public Transform cannonTop;

    [Header("Active Cannons")]
    [Tooltip("Disable this for the two-action Push/PoweredUp game.")]
    public bool useLeftCannon = true;

    public bool useRightCannon = true;
    public bool useTopCannon = true;

    [Header("High-Fidelity Cannon Polish")]
    [Tooltip("Drag your imported cyberpunk cannon PNG sprite here. The source image should point RIGHT.")]
    public Sprite highFidelityCannonSprite;

    [Tooltip("Leave this false unless the imported cannon sprite has a real transparent background.")]
    public bool useImportedCannonSprite = false;

    [Tooltip("If true, a manually assigned SpriteRenderer sprite on each cannon is preserved.")]
    public bool preserveSceneCannonSprites = false;

    [Tooltip("Visual size for the cannon body in world space.")]
    public Vector3 cannonVisualScale = new Vector3(0.92f, 0.92f, 1f);

    [Tooltip("Final on-screen cannon length in world units. Prevents imported reference images from becoming giant wall panels.")]
    public float cannonWorldLength = 2.65f;

    [Tooltip("Local muzzle position. Source cannon sprite points RIGHT, so positive X is the muzzle.")]
    public Vector3 muzzleLocalPosition = new Vector3(1.04f, 0f, -0.01f);

    [Tooltip("Optional custom laser beam sprite.")]
    public Sprite highFidelityLaserSprite;

    [Header("Loop Settings")]
    [Tooltip("Official spawn interval. A new laser starts charging every 5 seconds.")]
    public float spawnInterval = 5f;

    [Tooltip("Start spawning automatically when Play begins.")]
    public bool autoStart = true;

    private Transform[] cannons;
    private Coroutine spawnLoop;
    private Sprite runtimeLaserSprite;
    private Sprite runtimeCannonSprite;
    private Sprite runtimeGlowSprite;
    private Material runtimeAdditiveMaterial;
    private Material runtimeSpriteMaterial;

    private void Awake()
    {
        ResolveSceneReferences();
        ApplyRuntimeArcadeLayout();
    }

    private void Start()
    {
        ResolveSceneReferences();
        ApplyRuntimeArcadeLayout();

        if (autoStart)
        {
            StartSpawning();
        }
    }

    public void StartSpawning()
    {
        if (spawnLoop != null)
        {
            return;
        }

        spawnLoop = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (spawnLoop == null)
        {
            return;
        }

        StopCoroutine(spawnLoop);
        spawnLoop = null;
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (!CanSpawn())
            {
                Debug.LogWarning("LaserSpawner could not find Robot Target or Cannon references. Retrying...");
                yield return new WaitForSeconds(1f);
                continue;
            }

            Transform[] activeCannons = GetActiveCannons();
            Transform selectedCannon = activeCannons[Random.Range(0, activeCannons.Length)];
            Vector3 spawnPosition = GetCannonMuzzlePosition(selectedCannon);
            LaserBehavior laser = CreateLaser(selectedCannon, spawnPosition);
            laser.name = "Laser_From_" + selectedCannon.name;
            laser.target = robotTarget;
            laser.SetRequiredDefenseFromCannonName(selectedCannon.name);

            CannonChargeWarning chargeWarning = selectedCannon.GetComponentInChildren<CannonChargeWarning>(true);
            if (chargeWarning != null)
            {
                chargeWarning.PlayWarning(laser.chargeDuration);
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private bool CanSpawn()
    {
        ResolveSceneReferences();
        ApplyRuntimeArcadeLayout();

        return robotTarget != null && GetActiveCannons().Length > 0;
    }

    private void ResolveSceneReferences()
    {
        if (robotTarget == null)
        {
            GameObject robot = GameObject.Find(FemaleRobotName);
            if (robot == null)
            {
                robot = GameObject.Find(MaleRobotName);
            }

            if (robot != null)
            {
                robotTarget = robot.transform;
            }
        }

        cannonLeft = ResolveTransform(cannonLeft, CannonLeftName);
        cannonRight = ResolveTransform(cannonRight, CannonRightName);
        cannonTop = ResolveTransform(cannonTop, CannonTopName);
        cannons = new[] { cannonLeft, cannonRight, cannonTop };
    }

    private Transform[] GetActiveCannons()
    {
        ResolveSceneReferences();

        System.Collections.Generic.List<Transform> activeCannons = new System.Collections.Generic.List<Transform>();
        AddCannonIfActive(activeCannons, cannonLeft, useLeftCannon);
        AddCannonIfActive(activeCannons, cannonRight, useRightCannon);
        AddCannonIfActive(activeCannons, cannonTop, useTopCannon);
        return activeCannons.ToArray();
    }

    private static void AddCannonIfActive(System.Collections.Generic.List<Transform> activeCannons, Transform cannon, bool enabled)
    {
        if (enabled && cannon != null && cannon.gameObject.activeInHierarchy)
        {
            activeCannons.Add(cannon);
        }
    }

    private void ApplyRuntimeArcadeLayout()
    {
        Camera camera = Camera.main;
        float halfHeight = camera != null && camera.orthographic ? camera.orthographicSize : 5f;
        float halfWidth = camera != null && camera.orthographic ? halfHeight * camera.aspect : 8.89f;

        if (robotTarget != null)
        {
            robotTarget.position = GameplayRobotPosition;
            robotTarget.localScale = GameplayRobotScale;
        }

        float sideInset = cannonWorldLength * 0.5f + CannonEdgePadding;
        float topInset = cannonWorldLength * 0.5f + CannonEdgePadding;

        Vector3 leftPosition = new Vector3(-halfWidth + sideInset, 0f, 0f);
        Vector3 rightPosition = new Vector3(halfWidth - sideInset, 0f, 0f);
        Vector3 topPosition = new Vector3(0f, halfHeight - topInset, 0f);
        Vector3 chestPosition = GameplayRobotPosition + new Vector3(0f, RobotChestOffsetY * GameplayRobotScale.y, 0f);

        SetupCannonVisual(cannonLeft, leftPosition, GetAimAngleZ(leftPosition, chestPosition), new Color32(0, 255, 255, 255));
        SetupCannonVisual(cannonRight, rightPosition, GetAimAngleZ(rightPosition, chestPosition), new Color32(255, 0, 255, 255));
        SetupCannonVisual(cannonTop, topPosition, GetAimAngleZ(topPosition, chestPosition), new Color32(0, 255, 255, 255));
    }

    private float GetAimAngleZ(Vector3 from, Vector3 to)
    {
        Vector2 direction = to - from;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    private void SetupCannonVisual(Transform cannon, Vector3 position, float rotationZ, Color32 glowColor)
    {
        if (cannon == null)
        {
            return;
        }

        cannon.position = position;
        cannon.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        cannon.gameObject.SetActive(true);

        SpriteRenderer body = cannon.GetComponent<SpriteRenderer>();
        if (body == null)
        {
            body = cannon.gameObject.AddComponent<SpriteRenderer>();
        }

        body.sprite = ResolveCannonSprite(body);
        body.color = Color.white;
        body.material = GetRuntimeSpriteMaterial();
        body.sortingOrder = 28;
        body.enabled = true;
        FitCannonToSprite(cannon, body.sprite);

        Vector3 resolvedMuzzleLocalPosition = GetResolvedMuzzleLocalPosition(body.sprite);

        SpriteRenderer muzzleRenderer = EnsureChildRenderer(cannon, "MuzzleGlow", resolvedMuzzleLocalPosition, new Vector3(0.48f, 0.48f, 1f));
        muzzleRenderer.sprite = GetRuntimeGlowSprite();
        muzzleRenderer.color = glowColor;
        muzzleRenderer.material = GetRuntimeAdditiveMaterial();
        muzzleRenderer.sortingOrder = 34;
        muzzleRenderer.enabled = true;

        SpriteRenderer warningRenderer = EnsureChildRenderer(cannon, "WarningGlow", resolvedMuzzleLocalPosition + new Vector3(0.04f, 0f, -0.04f), new Vector3(0.95f, 0.95f, 1f));
        warningRenderer.sprite = GetRuntimeGlowSprite();
        warningRenderer.color = new Color32(0, 255, 255, 120);
        warningRenderer.material = GetRuntimeAdditiveMaterial();
        warningRenderer.sortingOrder = 48;
        warningRenderer.enabled = false;

        CannonChargeWarning warning = warningRenderer.GetComponent<CannonChargeWarning>();
        if (warning == null)
        {
            warning = warningRenderer.gameObject.AddComponent<CannonChargeWarning>();
        }

        warning.defaultDuration = 5f;
        warning.urgentFinalSeconds = 1f;
        warning.normalPulsesPerSecond = 1.45f;
        warning.urgentPulsesPerSecond = 3.2f;
        warning.restingScale = new Vector3(0.95f, 0.95f, 1f);
        warning.peakScale = new Vector3(2.75f, 2.75f, 1f);
        warning.normalParticleRate = 18f;
        warning.dangerParticleRate = 95f;
    }

    private Sprite ResolveCannonSprite(SpriteRenderer body)
    {
        if (useImportedCannonSprite && highFidelityCannonSprite != null && !LooksLikeFullBackgroundReference(highFidelityCannonSprite))
        {
            return highFidelityCannonSprite;
        }

        if (useImportedCannonSprite
            && preserveSceneCannonSprites
            && body.sprite != null
            && !body.sprite.name.StartsWith("Runtime_")
            && !LooksLikeFullBackgroundReference(body.sprite))
        {
            return body.sprite;
        }

        return GetRuntimeCannonSprite();
    }

    private bool LooksLikeFullBackgroundReference(Sprite sprite)
    {
        if (sprite == null)
        {
            return false;
        }

        string spriteName = sprite.name.ToLowerInvariant();
        string textureName = sprite.texture != null ? sprite.texture.name.ToLowerInvariant() : string.Empty;

        // The user's first imported image is a whole reference card with dark background, not a transparent cannon cutout.
        return (spriteName.Contains("cyberpunk_canno") || textureName.Contains("cyberpunk_canno"))
            && !spriteName.Contains("cutout")
            && !textureName.Contains("cutout");
    }

    private void FitCannonToSprite(Transform cannon, Sprite sprite)
    {
        float spriteWidth = sprite != null ? sprite.bounds.size.x : 0f;
        float fittedScale = spriteWidth > 0.01f ? cannonWorldLength / spriteWidth : cannonVisualScale.x;
        cannon.localScale = new Vector3(fittedScale, fittedScale, 1f);
    }

    private Vector3 GetResolvedMuzzleLocalPosition(Sprite sprite)
    {
        if (sprite == null)
        {
            return muzzleLocalPosition;
        }

        Bounds bounds = sprite.bounds;
        return new Vector3(bounds.extents.x * 0.82f, bounds.center.y, muzzleLocalPosition.z);
    }

    private SpriteRenderer EnsureChildRenderer(Transform parent, string childName, Vector3 localPosition, Vector3 localScale)
    {
        Transform child = parent.Find(childName);
        if (child == null && childName == "WarningGlow")
        {
            child = parent.Find("ChargeWarningGlow");
        }

        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(parent, false);
        }

        child.name = childName;
        child.localPosition = localPosition;
        child.localRotation = Quaternion.identity;
        child.localScale = localScale;
        child.gameObject.SetActive(true);

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = child.gameObject.AddComponent<SpriteRenderer>();
        }

        return renderer;
    }

    private Transform ResolveTransform(Transform current, string objectName)
    {
        if (current != null)
        {
            return current;
        }

        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    private Vector3 GetCannonMuzzlePosition(Transform selectedCannon)
    {
        Transform muzzle = selectedCannon.Find("MuzzleGlow");
        if (muzzle != null)
        {
            return muzzle.position;
        }

        return selectedCannon.position + selectedCannon.right * 0.9f;
    }

    private LaserBehavior CreateLaser(Transform selectedCannon, Vector3 spawnPosition)
    {
        if (laserPrefab != null)
        {
            return Instantiate(laserPrefab, spawnPosition, selectedCannon.rotation);
        }

        return CreateRuntimeLaserInstance(selectedCannon, spawnPosition);
    }

    private LaserBehavior CreateRuntimeLaserInstance(Transform selectedCannon, Vector3 spawnPosition)
    {
        GameObject laserObject = new GameObject("RuntimeLaser", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(TrailRenderer), typeof(LaserBehavior));
        laserObject.transform.position = spawnPosition;
        laserObject.transform.rotation = selectedCannon.rotation;

        SpriteRenderer spriteRenderer = laserObject.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = highFidelityLaserSprite != null ? highFidelityLaserSprite : GetRuntimeLaserSprite();
        spriteRenderer.color = new Color32(255, 255, 255, 255);
        spriteRenderer.material = GetRuntimeAdditiveMaterial();
        spriteRenderer.sortingOrder = 60;

        Rigidbody2D rigidbody = laserObject.GetComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.gravityScale = 0f;

        BoxCollider2D collider = laserObject.GetComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1f, 0.35f);

        TrailRenderer trail = laserObject.GetComponent<TrailRenderer>();
        trail.time = 0.24f;
        trail.minVertexDistance = 0.035f;
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.42f),
            new Keyframe(0.18f, 0.3f),
            new Keyframe(1f, 0f)
        );
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.material = GetRuntimeAdditiveMaterial();
        trail.sortingOrder = 58;
        trail.colorGradient = CreateRuntimeLaserGradient();

        return laserObject.GetComponent<LaserBehavior>();
    }

    private Gradient CreateRuntimeLaserGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0f, 0.95f, 1f), 0.2f),
                new GradientColorKey(new Color(0f, 0.95f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.72f, 0.22f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        return gradient;
    }

    private Sprite GetRuntimeLaserSprite()
    {
        if (runtimeLaserSprite != null)
        {
            return runtimeLaserSprite;
        }

        const int width = 128;
        const int height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 outer = new Color32(0, 242, 255, 130);
        Color32 core = new Color32(0, 255, 255, 245);
        Color32 hot = new Color32(245, 255, 255, 255);

        ClearTexture(texture, clear);

        for (int y = 0; y < height; y++)
        {
            float centerDistance = Mathf.Abs(y - (height - 1) * 0.5f);
            float outerAlpha = Mathf.Clamp01(1f - centerDistance / 15f);
            float coreAlpha = Mathf.Clamp01(1f - centerDistance / 6f);

            for (int x = 0; x < width; x++)
            {
                float taper = Mathf.Clamp01(Mathf.Min(x / 18f, (width - 1 - x) / 18f));
                if (outerAlpha > 0.02f)
                {
                    Color pixel = Color.Lerp(outer, core, coreAlpha);
                    pixel.a *= outerAlpha * taper;
                    texture.SetPixel(x, y, pixel);
                }
            }
        }

        FillRect(texture, 18, 13, 110, 18, hot);
        texture.Apply();
        runtimeLaserSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 96f);
        runtimeLaserSprite.name = "Runtime_HiFi_LaserBeam";
        return runtimeLaserSprite;
    }

    private Material GetRuntimeAdditiveMaterial()
    {
        if (runtimeAdditiveMaterial != null)
        {
            return runtimeAdditiveMaterial;
        }

        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        runtimeAdditiveMaterial = new Material(shader);
        runtimeAdditiveMaterial.name = "Runtime_Additive_Neon";
        return runtimeAdditiveMaterial;
    }

    private Material GetRuntimeSpriteMaterial()
    {
        if (runtimeSpriteMaterial != null)
        {
            return runtimeSpriteMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        }

        runtimeSpriteMaterial = new Material(shader);
        runtimeSpriteMaterial.name = "Runtime_Visible_Sprite";
        return runtimeSpriteMaterial;
    }

    private Sprite GetRuntimeGlowSprite()
    {
        if (runtimeGlowSprite != null)
        {
            return runtimeGlowSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float normalized = Mathf.Clamp01(distance / radius);
                byte alpha = distance <= radius ? (byte)Mathf.RoundToInt(Mathf.Pow(1f - normalized, 1.65f) * 255f) : (byte)0;
                texture.SetPixel(x, y, new Color32(255, 255, 255, alpha));
            }
        }

        texture.Apply();
        runtimeGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 128f);
        runtimeGlowSprite.name = "Runtime_Radial_Additive_Glow";
        return runtimeGlowSprite;
    }

    private Sprite GetRuntimeCannonSprite()
    {
        if (runtimeCannonSprite != null)
        {
            return runtimeCannonSprite;
        }

        const int width = 900;
        const int height = 360;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 body = new Color32(15, 25, 42, 255);
        Color32 body2 = new Color32(30, 48, 72, 255);
        Color32 dark = new Color32(5, 10, 18, 255);
        Color32 edge = new Color32(0, 235, 255, 255);
        Color32 cyanSoft = new Color32(0, 220, 255, 190);
        Color32 magenta = new Color32(255, 48, 224, 255);
        Color32 glass = new Color32(90, 140, 170, 120);
        Color32 hotCore = new Color32(235, 255, 255, 255);

        ClearTexture(texture, clear);

        // Rear stabilizer and glassy back panel.
        FillPolygon(texture, new[]
        {
            new Vector2(34f, 58f),
            new Vector2(152f, 94f),
            new Vector2(152f, 266f),
            new Vector2(34f, 302f)
        }, body2);
        DrawPolygon(texture, new[]
        {
            new Vector2(34f, 58f),
            new Vector2(152f, 94f),
            new Vector2(152f, 266f),
            new Vector2(34f, 302f)
        }, new Color32(80, 120, 150, 180), 5);

        // Main cannon chassis.
        FillRect(texture, 142, 96, 525, 264, body);
        DrawRect(texture, 142, 96, 525, 264, edge, 5);
        FillRect(texture, 178, 126, 330, 234, body2);
        DrawRect(texture, 178, 126, 330, 234, new Color32(70, 110, 145, 190), 3);

        // Frosted glass panel and tiny UI accents.
        FillRect(texture, 248, 132, 366, 228, glass);
        DrawRect(texture, 248, 132, 366, 228, cyanSoft, 3);
        for (int row = 0; row < 3; row++)
        {
            FillRect(texture, 276, 150 + row * 22, 332, 160 + row * 22, cyanSoft);
        }
        FillRect(texture, 390, 126, 450, 158, cyanSoft);
        FillRect(texture, 390, 174, 450, 196, magenta);
        FillRect(texture, 390, 210, 456, 232, cyanSoft);

        // Multi-barrel assembly.
        FillPolygon(texture, new[]
        {
            new Vector2(500f, 80f),
            new Vector2(650f, 104f),
            new Vector2(650f, 256f),
            new Vector2(500f, 280f)
        }, body2);
        DrawPolygon(texture, new[]
        {
            new Vector2(500f, 80f),
            new Vector2(650f, 104f),
            new Vector2(650f, 256f),
            new Vector2(500f, 280f)
        }, new Color32(75, 110, 140, 210), 4);

        for (int i = 0; i < 4; i++)
        {
            int y = 118 + i * 36;
            FillRect(texture, 470, y, 730, y + 20, dark);
            FillRect(texture, 492, y + 5, 700, y + 13, new Color32(70, 125, 150, 170));
            DrawRect(texture, 470, y, 730, y + 20, new Color32(70, 90, 118, 220), 2);
        }

        // Front muzzle ring, pointing right by default.
        FillPolygon(texture, new[]
        {
            new Vector2(650f, 72f),
            new Vector2(850f, 112f),
            new Vector2(850f, 248f),
            new Vector2(650f, 288f)
        }, body);
        DrawPolygon(texture, new[]
        {
            new Vector2(650f, 72f),
            new Vector2(850f, 112f),
            new Vector2(850f, 248f),
            new Vector2(650f, 288f)
        }, edge, 6);

        FillCircle(texture, new Vector2(792f, 180f), 62f, cyanSoft);
        FillCircle(texture, new Vector2(792f, 180f), 48f, body2);
        FillCircle(texture, new Vector2(792f, 180f), 36f, edge);
        FillCircle(texture, new Vector2(792f, 180f), 22f, hotCore);

        // Lower power cylinder.
        FillRect(texture, 170, 250, 350, 318, body2);
        DrawRect(texture, 170, 250, 350, 318, new Color32(70, 110, 140, 220), 4);
        FillRect(texture, 198, 268, 286, 284, cyanSoft);
        FillRect(texture, 296, 268, 332, 284, magenta);

        // Neon cable and side highlights.
        DrawLine(texture, new Vector2(210f, 244f), new Vector2(360f, 286f), cyanSoft, 6);
        DrawLine(texture, new Vector2(130f, 180f), new Vector2(230f, 180f), edge, 7);
        DrawLine(texture, new Vector2(230f, 180f), new Vector2(280f, 118f), magenta, 5);
        DrawLine(texture, new Vector2(280f, 118f), new Vector2(455f, 118f), cyanSoft, 5);

        texture.Apply();
        runtimeCannonSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 360f);
        runtimeCannonSprite.name = "Runtime_HiFi_CyberCannon";
        return runtimeCannonSprite;
    }

    private void ClearTexture(Texture2D texture, Color32 color)
    {
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }

    private void FillRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color32 color)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private void DrawRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color32 color, int thickness)
    {
        FillRect(texture, minX, minY, maxX, minY + thickness, color);
        FillRect(texture, minX, maxY - thickness, maxX, maxY, color);
        FillRect(texture, minX, minY, minX + thickness, maxY, color);
        FillRect(texture, maxX - thickness, minY, maxX, maxY, color);
    }

    private void FillCircle(Texture2D texture, Vector2 center, float radius, Color32 color)
    {
        int minX = Mathf.FloorToInt(center.x - radius);
        int maxX = Mathf.CeilToInt(center.x + radius);
        int minY = Mathf.FloorToInt(center.y - radius);
        int maxY = Mathf.CeilToInt(center.y + radius);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (x >= 0 && x < texture.width && y >= 0 && y < texture.height
                    && Vector2.Distance(new Vector2(x, y), center) <= radius)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
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
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Length];
            DrawLine(texture, start, end, color, thickness);
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
