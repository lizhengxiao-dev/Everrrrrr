using System.Collections;
using UnityEngine;

/// <summary>
/// Large, high-contrast cannon warning used during the 5-second laser charge.
/// Designed to be impossible to miss for elderly rehabilitation patients.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CannonChargeWarning : MonoBehaviour
{
    [Header("Timing")]
    public float defaultDuration = 5f;
    public float urgentFinalSeconds = 1f;

    [Header("Pulse")]
    public float normalPulsesPerSecond = 1.45f;
    public float urgentPulsesPerSecond = 3.2f;
    public Vector3 restingScale = new Vector3(0.8f, 0.8f, 1f);
    public Vector3 peakScale = new Vector3(2.55f, 2.55f, 1f);

    [Header("Color")]
    public Color safeNeonCyan = new Color(0f, 1f, 1f, 0.42f);
    public Color dangerNeonRed = new Color(1f, 0f, 0.04f, 1f);
    public Color finalWhiteHot = new Color(1f, 0.96f, 0.88f, 1f);

    [Header("Particles")]
    public ParticleSystem warningParticles;
    public float normalParticleRate = 18f;
    public float dangerParticleRate = 95f;

    private SpriteRenderer spriteRenderer;
    private Coroutine warningRoutine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureParticleSystem();
        HideImmediately();
    }

    public void PlayWarning(float duration)
    {
        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
            warningRoutine = null;
        }

        HideImmediately();
        warningRoutine = StartCoroutine(WarningRoutine(duration > 0f ? duration : defaultDuration));
    }

    public void StopWarning()
    {
        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
            warningRoutine = null;
        }

        HideImmediately();
    }

    private IEnumerator WarningRoutine(float duration)
    {
        EnsureParticleSystem();
        spriteRenderer.enabled = true;

        warningParticles.Clear();
        warningParticles.Play();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            bool urgent = elapsed >= duration - urgentFinalSeconds;
            float pulsesPerSecond = urgent ? urgentPulsesPerSecond : normalPulsesPerSecond;

            float wave = (Mathf.Sin(elapsed * pulsesPerSecond * Mathf.PI * 2f) + 1f) * 0.5f;
            float amplifiedWave = Mathf.SmoothStep(0f, 1f, wave);
            float dangerRamp = Mathf.SmoothStep(0f, 1f, progress);

            Color baseColor = Color.Lerp(safeNeonCyan, dangerNeonRed, dangerRamp);
            if (urgent && amplifiedWave > 0.78f)
            {
                baseColor = Color.Lerp(baseColor, finalWhiteHot, 0.32f);
            }

            baseColor.a = Mathf.Lerp(0.34f, 1f, amplifiedWave);
            spriteRenderer.color = baseColor;

            float urgencyScaleBoost = urgent ? 1.18f : 1f;
            transform.localScale = Vector3.Lerp(restingScale, peakScale * urgencyScaleBoost, amplifiedWave);

            UpdateParticleIntensity(progress, urgent, baseColor);

            yield return null;
        }

        spriteRenderer.color = finalWhiteHot;
        transform.localScale = peakScale * 1.25f;
        UpdateParticleIntensity(1f, true, finalWhiteHot);
        yield return new WaitForSeconds(0.08f);

        HideImmediately();
        warningRoutine = null;
    }

    private void EnsureParticleSystem()
    {
        if (warningParticles == null)
        {
            warningParticles = GetComponent<ParticleSystem>();
        }

        if (warningParticles == null)
        {
            warningParticles = gameObject.AddComponent<ParticleSystem>();
        }

        if (warningParticles.isPlaying)
        {
            warningParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        ParticleSystem.MainModule main = warningParticles.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.44f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.65f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(safeNeonCyan, Color.white);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = warningParticles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = warningParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.38f;
        shape.radiusThickness = 0.22f;

        ParticleSystemRenderer particleRenderer = warningParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sortingOrder = 42;

        Shader additiveShader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (additiveShader != null)
        {
            particleRenderer.material = new Material(additiveShader);
        }
    }

    private void UpdateParticleIntensity(float progress, bool urgent, Color color)
    {
        if (warningParticles == null)
        {
            return;
        }

        float rate = Mathf.Lerp(normalParticleRate, dangerParticleRate, progress);
        if (urgent)
        {
            rate *= 1.65f;
        }

        ParticleSystem.EmissionModule emission = warningParticles.emission;
        emission.rateOverTime = rate;

        ParticleSystem.MainModule main = warningParticles.main;
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
    }

    private void HideImmediately()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        spriteRenderer.color = safeNeonCyan;
        transform.localScale = restingScale;
        spriteRenderer.enabled = false;

        if (warningParticles != null)
        {
            ParticleSystem.EmissionModule emission = warningParticles.emission;
            emission.rateOverTime = 0f;
            warningParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
