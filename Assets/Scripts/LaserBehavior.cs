using UnityEngine;

/// <summary>
/// Controls one laser from warning charge to launch.
/// The laser waits at the cannon for a charge period, flashes, then flies toward the robot.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class LaserBehavior : MonoBehaviour
{
    public enum RequiredDefense
    {
        Left = 1,
        Right = 2,
        Top = 3
    }

    [Header("Target")]
    [Tooltip("The robot transform. Usually Female0001 or Male0001.")]
    public Transform target;

    [Tooltip("Used as a fallback when Target is empty in the Inspector.")]
    public string fallbackTargetName = "Female0001";

    [Tooltip("Aim slightly above the robot pivot so the laser points at the chest.")]
    public Vector2 targetChestOffset = new Vector2(0f, 0.75f);

    [Header("Charge")]
    [Tooltip("How long the laser stays at the cannon before launching.")]
    public float chargeDuration = 5f;

    [Tooltip("Color used early in the warning phase.")]
    public Color safeColor = new Color(0f, 1f, 1f, 0.45f);

    [Tooltip("Color used near the end of the warning phase.")]
    public Color warningColor = new Color(1f, 0.85f, 0f, 0.75f);

    [Tooltip("Final danger color before launch.")]
    public Color dangerColor = new Color(1f, 0f, 0.05f, 1f);

    [Tooltip("White-hot core color used after launch so Bloom catches the projectile strongly.")]
    public Color launchedCoreColor = new Color(1f, 1f, 1f, 1f);

    [Tooltip("How fast the warning flash pulses.")]
    public float flashSpeed = 12f;

    [Tooltip("Small scale while charging at the cannon.")]
    public Vector3 chargeLocalScale = new Vector3(0.35f, 0.35f, 1f);

    [Header("Launch")]
    [Tooltip("Beam shape after charging. X should be long and Y should be thin.")]
    public Vector3 launchedLocalScale = new Vector3(2.4f, 0.18f, 1f);

    [Tooltip("Movement speed after the laser launches.")]
    public float launchSpeed = 9f;

    [Tooltip("Laser destroys itself when it gets this close to the robot chest.")]
    public float destroyDistance = 0.22f;

    [Tooltip("Distance from the robot chest where the laser resolves block/body-hit logic.")]
    public float resolveDistance = 0.55f;

    [Tooltip("Safety cleanup if the laser somehow misses the target.")]
    public float maxLaunchedLifetime = 4f;

    [Tooltip("Which robot pose blocks this laser.")]
    public RequiredDefense requiredDefense = RequiredDefense.Left;

    [Header("Collision VFX")]
    [Tooltip("Spawned when the laser hits a shield.")]
    public GameObject shieldBlockVfxPrefab;

    [Tooltip("Optional VFX spawned when the laser hits the robot body.")]
    public GameObject bodyHitVfxPrefab;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private TrailRenderer trailRenderer;
    private Animator targetAnimator;
    private Vector3 launchDirection;
    private float chargeTimer;
    private float launchedTimer;
    private bool launched;
    private bool alreadyResolvedCollision;
    private bool warnedMissingTarget;
    private static Material additiveLaserMaterial;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        trailRenderer = GetComponent<TrailRenderer>();
        if (trailRenderer == null)
        {
            trailRenderer = gameObject.AddComponent<TrailRenderer>();
        }

        ConfigureLaserVfx();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        transform.localScale = chargeLocalScale;
    }

    private void Update()
    {
        if (target == null)
        {
            TryFindFallbackTarget();

            if (target == null)
            {
                if (!warnedMissingTarget)
                {
                    Debug.LogWarning("LaserBehavior has no target. Drag Male0001 into Target, or let LaserSpawner assign it.", this);
                    warnedMissingTarget = true;
                }

                return;
            }
        }

        if (targetAnimator == null)
        {
            targetAnimator = target.GetComponent<Animator>();
        }

        if (!launched)
        {
            UpdateCharge();
            return;
        }

        UpdateLaunch();
    }

    private void UpdateCharge()
    {
        chargeTimer += Time.deltaTime;

        float progress = Mathf.Clamp01(chargeTimer / chargeDuration);
        float pulse = Mathf.PingPong(Time.time * flashSpeed, 1f);

        Color baseColor = progress < 0.5f
            ? Color.Lerp(safeColor, warningColor, progress / 0.5f)
            : Color.Lerp(warningColor, dangerColor, (progress - 0.5f) / 0.5f);

        baseColor.a = Mathf.Lerp(0.35f, 1f, pulse);
        spriteRenderer.color = baseColor;

        float warningScalePulse = Mathf.Lerp(0.9f, 1.25f, pulse);
        transform.localScale = chargeLocalScale * warningScalePulse;

        if (chargeTimer >= chargeDuration)
        {
            Launch();
        }
    }

    private void Launch()
    {
        launched = true;
        spriteRenderer.color = launchedCoreColor;
        transform.localScale = launchedLocalScale;
        trailRenderer.Clear();
        trailRenderer.emitting = true;

        Vector3 targetPosition = GetTargetChestPosition();
        launchDirection = (targetPosition - transform.position).normalized;

        float angle = Mathf.Atan2(launchDirection.y, launchDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void ConfigureLaserVfx()
    {
        Material additiveMaterial = GetAdditiveLaserMaterial();

        spriteRenderer.material = additiveMaterial;
        spriteRenderer.sortingOrder = 62;

        trailRenderer.time = 0.24f;
        trailRenderer.minVertexDistance = 0.035f;
        trailRenderer.alignment = LineAlignment.View;
        trailRenderer.textureMode = LineTextureMode.Stretch;
        trailRenderer.material = additiveMaterial;
        trailRenderer.sortingOrder = 60;
        trailRenderer.emitting = false;
        trailRenderer.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.42f),
            new Keyframe(0.18f, 0.3f),
            new Keyframe(1f, 0f)
        );
        trailRenderer.colorGradient = CreateLaserTrailGradient();
    }

    private Material GetAdditiveLaserMaterial()
    {
        if (additiveLaserMaterial != null)
        {
            return additiveLaserMaterial;
        }

        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        additiveLaserMaterial = new Material(shader);
        additiveLaserMaterial.name = "Runtime_Laser_Additive";
        return additiveLaserMaterial;
    }

    private Gradient CreateLaserTrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0f, 0.95f, 1f), 0.18f),
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

    private void UpdateLaunch()
    {
        launchedTimer += Time.deltaTime;
        transform.position += launchDirection * launchSpeed * Time.deltaTime;

        float distanceToTarget = Vector3.Distance(transform.position, GetTargetChestPosition());
        if (distanceToTarget <= resolveDistance)
        {
            ResolveAtRobot();
            return;
        }

        if (launchedTimer >= maxLaunchedLifetime)
        {
            Destroy(gameObject);
        }
    }

    private Vector3 GetTargetChestPosition()
    {
        float referenceRobotScaleY = 0.55699f;
        float scaleRatio = referenceRobotScaleY > 0f ? target.lossyScale.y / referenceRobotScaleY : 1f;
        Vector3 offset = new Vector3(targetChestOffset.x, targetChestOffset.y * scaleRatio, 0f);
        return target.position + offset;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!launched || alreadyResolvedCollision)
        {
            return;
        }

        if (col.CompareTag("Shield"))
        {
            if (!IsCurrentDefenseCorrect())
            {
                ResolveAtRobot();
                return;
            }

            Vector2 impactPosition = col.ClosestPoint(transform.position);
            NotifyShieldImpact(col, impactPosition);
            ResolvePerfectBlock(impactPosition);
            return;
        }

        if (col.CompareTag("Player"))
        {
            ResolveAtRobot();
        }
    }

    public void SetRequiredDefenseFromCannonName(string cannonName)
    {
        if (cannonName.Contains("Left"))
        {
            requiredDefense = RequiredDefense.Left;
            return;
        }

        if (cannonName.Contains("Right"))
        {
            requiredDefense = RequiredDefense.Right;
            return;
        }

        requiredDefense = RequiredDefense.Top;
    }

    private void ResolveAtRobot()
    {
        if (alreadyResolvedCollision)
        {
            return;
        }

        if (IsCurrentDefenseCorrect())
        {
            NotifyShieldImpact(null, transform.position);
            ResolvePerfectBlock(transform.position);
            return;
        }

        alreadyResolvedCollision = true;
        SpawnVfx(bodyHitVfxPrefab, transform.position);
        Debug.Log("Hit Body! Lose Health! Needed " + requiredDefense + " but current DefenseDirection was " + GetCurrentDefenseDirection());

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterBodyHit();
        }

        Destroy(gameObject);
    }

    private bool IsCurrentDefenseCorrect()
    {
        return GetCurrentDefenseDirection() == (int)requiredDefense;
    }

    private int GetCurrentDefenseDirection()
    {
        return targetAnimator != null ? targetAnimator.GetInteger("DefenseDirection") : 0;
    }

    private void ResolvePerfectBlock(Vector2 vfxPosition)
    {
        if (alreadyResolvedCollision)
        {
            return;
        }

        alreadyResolvedCollision = true;
        SpawnVfx(shieldBlockVfxPrefab, vfxPosition);
        Debug.Log("Perfect Block! Score +1 (" + requiredDefense + ")");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddBlock();
        }

        Destroy(gameObject);
    }

    private void NotifyShieldImpact(Collider2D shieldCollider, Vector2 impactPosition)
    {
        ShieldManager shieldManager = null;

        if (shieldCollider != null)
        {
            shieldManager = shieldCollider.GetComponentInParent<ShieldManager>();
        }

        if (shieldManager == null && target != null)
        {
            shieldManager = target.GetComponent<ShieldManager>();
        }

        if (shieldManager != null)
        {
            shieldManager.RegisterShieldImpact(impactPosition);
        }

        PushShieldVfx pushShieldVfx = shieldCollider != null
            ? shieldCollider.GetComponentInParent<PushShieldVfx>()
            : null;

        if (pushShieldVfx == null && target != null)
        {
            pushShieldVfx = target.GetComponent<PushShieldVfx>();
        }

        if (pushShieldVfx != null)
        {
            pushShieldVfx.PlayImpact(impactPosition);
        }
    }

    private void SpawnVfx(GameObject vfxPrefab, Vector2 position)
    {
        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, position, Quaternion.identity);
            return;
        }

        SpawnRuntimeSparkBurst(position);
    }

    private void SpawnRuntimeSparkBurst(Vector2 position)
    {
        GameObject sparks = new GameObject("RuntimeShieldBlockSparks");
        sparks.transform.position = position;

        ParticleSystem particles = sparks.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color32(0, 255, 255, 245), new Color32(255, 255, 255, 255));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.32f;

        ParticleSystemRenderer particleRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sortingOrder = 60;

        particles.Play();
    }

    private void TryFindFallbackTarget()
    {
        if (string.IsNullOrWhiteSpace(fallbackTargetName))
        {
            return;
        }

        GameObject fallbackTarget = GameObject.Find(fallbackTargetName);
        if (fallbackTarget == null && fallbackTargetName != "Male0001")
        {
            fallbackTarget = GameObject.Find("Male0001");
        }

        if (fallbackTarget != null)
        {
            target = fallbackTarget.transform;
        }
    }
}
