using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Removes the failed static sprite/shader shield and restores a code-generated
/// dynamic energy shields for push and powered-up actions.
/// </summary>
public static class EverMotionShieldRedo
{
    private const string MenuPath = "EverMotion/Redo Push Shield";
    private const string BadMaterialPath = "Assets/EverMotionGenerated/M_LeftPush_CyberHexShield.mat";
    private const string BadBloomPath = "Assets/EverMotionGenerated/VP_EverMotion_LeftShieldBloom.asset";
    private const string BadBloomObjectName = "Global_LeftPush_Shield_Bloom";

    [MenuItem(MenuPath)]
    public static void RedoPushShield()
    {
        DisableStaticShield("Shield_Left");
        DisableStaticShield("Shield_Right");
        DisableStaticShield("Shield_Top");
        ConfigureActiveRobotShieldManager();
        RemoveBadGeneratedAssets();
        EnsureNeonBloom();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Shield VFX redone: push uses the honeycomb umbrella shield; UP uses PoweredUp_Shield full-body shield.");
    }

    private static void DisableStaticShield(string shieldName)
    {
        GameObject shield = FindSceneObjectIncludingInactive(shieldName);
        if (shield == null)
        {
            return;
        }

        Undo.RecordObject(shield, "Disable Static Shield");

        foreach (SpriteRenderer renderer in shield.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Undo.RecordObject(renderer, "Disable Static Shield Renderer");
            renderer.enabled = false;
            renderer.sharedMaterial = null;
            EditorUtility.SetDirty(renderer);
        }

        foreach (Collider2D collider in shield.GetComponentsInChildren<Collider2D>(true))
        {
            Undo.RecordObject(collider, "Disable Static Shield Collider");
            collider.enabled = false;
            EditorUtility.SetDirty(collider);
        }

        shield.SetActive(false);
        EditorUtility.SetDirty(shield);
    }

    private static void ConfigureActiveRobotShieldManager()
    {
        GameObject robot = FindActiveRobot();
        if (robot == null)
        {
            Debug.LogWarning("No active robot found. Build/install a robot first, then rerun EverMotion > Redo Push Shield.");
            return;
        }

        DisableOldShieldManagerVisuals(robot);

        PushShieldVfx existingPushShieldVfx = robot.GetComponent<PushShieldVfx>();
        if (existingPushShieldVfx != null)
        {
            Undo.DestroyObjectImmediate(existingPushShieldVfx);
        }

        PushShieldVfx pushShieldVfx = Undo.AddComponent<PushShieldVfx>(robot);
        Undo.RecordObject(pushShieldVfx, "Configure Push Shield VFX");
        pushShieldVfx.defenseDirectionParameter = "DefenseDirection";
        pushShieldVfx.leftLocalPosition = new Vector3(-3.85f, 0.55f, -0.05f);
        pushShieldVfx.rightLocalPosition = new Vector3(3.85f, 0.55f, -0.05f);
        pushShieldVfx.topLocalPosition = new Vector3(0f, 2.35f, -0.05f);
        pushShieldVfx.sideScale = new Vector3(1.65f, 2.25f, 1f);
        pushShieldVfx.topScale = new Vector3(1.55f, 1.35f, 1f);
        pushShieldVfx.shieldColor = new Color(0f, 1f, 1f, 0.78f);
        pushShieldVfx.hotColor = new Color(0.92f, 1f, 1f, 1f);
        pushShieldVfx.poweredUpShieldSprite = LoadPoweredUpShieldSprite();
        pushShieldVfx.poweredShieldLocalPosition = new Vector3(0f, 0.48f, -0.06f);
        pushShieldVfx.poweredShieldLocalScale = new Vector3(2.15f, 3.25f, 1f);
        pushShieldVfx.poweredShieldColor = new Color(0.35f, 1f, 1f, 0.42f);
        pushShieldVfx.poweredShieldEdgeColor = new Color(0.88f, 1f, 1f, 0.82f);
        pushShieldVfx.poweredPulseSpeed = 4.2f;
        pushShieldVfx.poweredPulseAmount = 0.035f;
        pushShieldVfx.sortingOrder = 140;
        pushShieldVfx.fadeSpeed = 14f;
        pushShieldVfx.showEditorPreview = true;
        pushShieldVfx.forceVisibleForDebug = false;
        pushShieldVfx.editorPreviewDirection = 3;
        pushShieldVfx.RebuildNow();
        EditorUtility.SetDirty(pushShieldVfx);
    }

    private static void DisableOldShieldManagerVisuals(GameObject robot)
    {
        ShieldManager shieldManager = robot.GetComponent<ShieldManager>();
        if (shieldManager != null)
        {
            Undo.RecordObject(shieldManager, "Disable Old Shield Manager Visuals");
            shieldManager.enabled = false;
            EditorUtility.SetDirty(shieldManager);
        }

        Transform oldMatrix = robot.transform.Find("AssembleShield_Matrix");
        if (oldMatrix != null)
        {
            Undo.RecordObject(oldMatrix.gameObject, "Disable Old Shield Matrix");
            oldMatrix.gameObject.SetActive(false);
            EditorUtility.SetDirty(oldMatrix.gameObject);
        }
    }

    private static Sprite LoadPoweredUpShieldSprite()
    {
        string[] candidatePaths =
        {
            "Assets/PoweredUp_Shield.png",
            "Assets/FemaleRobot/PoweredUp_Shield.png",
        };

        for (int i = 0; i < candidatePaths.Length; i++)
        {
            string path = candidatePaths[i];
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 100f;
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    sprite = assets[assetIndex] as Sprite;
                    if (sprite != null)
                    {
                        break;
                    }
                }
            }

            if (sprite != null)
            {
                return sprite;
            }
        }

        Debug.LogWarning("Could not find PoweredUp_Shield.png. PushShieldVfx will use a generated fallback full-body shield.");
        return null;
    }

    private static void RemoveBadGeneratedAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(BadMaterialPath) != null)
        {
            AssetDatabase.DeleteAsset(BadMaterialPath);
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(BadBloomPath) != null)
        {
            AssetDatabase.DeleteAsset(BadBloomPath);
        }

        GameObject badBloomObject = FindSceneObjectIncludingInactive(BadBloomObjectName);
        if (badBloomObject != null)
        {
            Undo.DestroyObjectImmediate(badBloomObject);
        }
    }

    private static void EnsureNeonBloom()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = Object.FindFirstObjectByType<Camera>();
        }

        if (camera == null)
        {
            return;
        }

        Undo.RecordObject(camera, "Enable Neon Bloom Camera");
        camera.allowHDR = true;

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            cameraData = Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
        }

        Undo.RecordObject(cameraData, "Enable Neon Bloom Camera Data");
        cameraData.renderPostProcessing = true;
        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(cameraData);
    }

    private static GameObject FindActiveRobot()
    {
        GameObject female = FindSceneObjectIncludingInactive("Female0001");
        if (female != null && female.activeInHierarchy)
        {
            return female;
        }

        GameObject male = FindSceneObjectIncludingInactive("Male0001");
        if (male != null && male.activeInHierarchy)
        {
            return male;
        }

        return female != null ? female : male;
    }

    private static GameObject FindSceneObjectIncludingInactive(string objectName)
    {
        GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }
}
