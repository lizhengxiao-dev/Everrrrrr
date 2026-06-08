using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-click final neon polish for EverMotion:
/// - Enables URP camera post-processing.
/// - Creates a global Bloom Volume.
/// - Upgrades Laser prefab SpriteRenderer and TrailRenderer to a brighter sci-fi beam.
///
/// Menu path: Team EchoCare > Polish Bloom and Laser VFX
/// </summary>
public static class EverMotionBloomVfxHelper
{
    private const string MenuPath = "Team EchoCare/Polish Bloom and Laser VFX";
    private const string VolumeName = "Global_Neon_Bloom_Volume";
    private const string VolumeProfilePath = "Assets/EverMotionGenerated/VP_EverMotion_NeonBloom.asset";
    private const string LaserPrefabPath = "Assets/Prefabs/LaserPrefab.prefab";
    private const string LaserPrefabSourcePath = "Assets/Prefabs/LaserPrefab_Source.prefab";
    private const string LaserMaterialPath = "Assets/EverMotionGenerated/M_EverMotion_Laser_Additive.mat";

    [MenuItem(MenuPath)]
    public static void PolishBloomAndLaserVfx()
    {
        EnsureGeneratedFolder();
        EnableCameraPostProcessing();
        CreateOrUpdateBloomVolume();
        CreateOrUpdateLaserMaterial();
        PolishLaserPrefab(LaserPrefabPath);
        PolishLaserPrefab(LaserPrefabSourcePath);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("EverMotion Bloom + Laser VFX polish complete. Neon Bloom Volume and laser trail settings were updated.");
    }

    private static void EnableCameraPostProcessing()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = Object.FindFirstObjectByType<Camera>();
        }

        if (camera == null)
        {
            Debug.LogWarning("No camera found. Bloom volume was created, but no camera was configured.");
            return;
        }

        Undo.RecordObject(camera, "Enable EverMotion Camera Post Processing");
        camera.allowHDR = true;

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        Undo.RecordObject(cameraData, "Enable EverMotion Camera Post Processing");
        cameraData.renderPostProcessing = true;
        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(cameraData);
    }

    private static void CreateOrUpdateBloomVolume()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }

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
        vignette.color.Override(new Color(0f, 0f, 0f, 1f));
        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.56f);

        GameObject volumeObject = GameObject.Find(VolumeName);
        if (volumeObject == null)
        {
            volumeObject = new GameObject(VolumeName);
            Undo.RegisterCreatedObjectUndo(volumeObject, "Create EverMotion Bloom Volume");
        }

        Volume volume = volumeObject.GetComponent<Volume>();
        if (volume == null)
        {
            volume = volumeObject.AddComponent<Volume>();
        }

        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 1f;
        volume.sharedProfile = profile;

        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(volumeObject);
    }

    private static Material CreateOrUpdateLaserMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(LaserMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            material = new Material(shader);
            material.name = "M_EverMotion_Laser_Additive";
            AssetDatabase.CreateAsset(material, LaserMaterialPath);
        }

        material.color = Color.white;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void PolishLaserPrefab(string prefabPath)
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
        {
            return;
        }

        GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ConfigureLaserObject(prefab);
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    private static void ConfigureLaserObject(GameObject laserObject)
    {
        SpriteRenderer spriteRenderer = laserObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = laserObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        spriteRenderer.material = CreateOrUpdateLaserMaterial();
        spriteRenderer.sortingOrder = 60;

        TrailRenderer trail = laserObject.GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = laserObject.AddComponent<TrailRenderer>();
        }

        trail.time = 0.24f;
        trail.minVertexDistance = 0.035f;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sortingOrder = 58;
        trail.material = CreateOrUpdateLaserMaterial();
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.42f),
            new Keyframe(0.18f, 0.30f),
            new Keyframe(1f, 0f)
        );
        trail.colorGradient = CreateLaserGradient();

        LaserBehavior behavior = laserObject.GetComponent<LaserBehavior>();
        if (behavior != null)
        {
            behavior.safeColor = new Color(0f, 1f, 1f, 0.55f);
            behavior.warningColor = new Color(1f, 0.9f, 0f, 0.85f);
            behavior.dangerColor = new Color(1f, 0.04f, 0.06f, 1f);
            behavior.chargeLocalScale = new Vector3(0.4f, 0.4f, 1f);
            behavior.launchedLocalScale = new Vector3(3.2f, 0.16f, 1f);
            behavior.launchSpeed = 10.5f;
        }
    }

    private static Gradient CreateLaserGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
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

    private static void EnsureGeneratedFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/EverMotionGenerated"))
        {
            AssetDatabase.CreateFolder("Assets", "EverMotionGenerated");
        }
    }
}
