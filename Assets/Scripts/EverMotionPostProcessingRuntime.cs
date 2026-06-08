using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Runtime safety pass for neon Bloom.
/// The Editor helper can bake this into the scene, but this keeps Play mode correct even if the menu is not clicked.
/// </summary>
public static class EverMotionPostProcessingRuntime
{
    private const string VolumeName = "Global_Neon_Bloom_Volume";

    public static void EnsureNeonBloom()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.allowHDR = true;

            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            cameraData.renderPostProcessing = true;
        }

        GameObject volumeObject = GameObject.Find(VolumeName);
        if (volumeObject == null)
        {
            volumeObject = new GameObject(VolumeName);
        }

        Volume volume = volumeObject.GetComponent<Volume>();
        if (volume == null)
        {
            volume = volumeObject.AddComponent<Volume>();
        }

        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 1f;

        if (volume.profile == null)
        {
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile.name = "Runtime_EverMotion_NeonBloom";
        }

        ConfigureProfile(volume.profile);
    }

    private static void ConfigureProfile(VolumeProfile profile)
    {
        if (!profile.TryGet(out Bloom bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }

        bloom.active = true;
        bloom.threshold.Override(0.72f);
        bloom.intensity.Override(1.85f);
        bloom.scatter.Override(0.72f);
        bloom.clamp.Override(6.5f);
        bloom.tint.Override(new Color(0.78f, 1f, 1f, 1f));
        bloom.highQualityFiltering.Override(true);

        if (!profile.TryGet(out ColorAdjustments colorAdjustments))
        {
            colorAdjustments = profile.Add<ColorAdjustments>(true);
        }

        colorAdjustments.active = true;
        colorAdjustments.postExposure.Override(0.05f);
        colorAdjustments.contrast.Override(10f);
        colorAdjustments.saturation.Override(6f);

        if (!profile.TryGet(out Vignette vignette))
        {
            vignette = profile.Add<Vignette>(true);
        }

        vignette.active = true;
        vignette.color.Override(Color.black);
        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.56f);
    }
}
