using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click scene-wide visual polish helper for Team EchoCare's EverMotion.
/// Menu path: Team EchoCare > Polish EverMotion HUD
///
/// Background rule:
/// image_4.png must be a WORLD SpriteRenderer background, not a Canvas Image.
/// Putting it inside Screen Space Overlay Canvas hides the robot/cannons, so this helper disables old
/// CyberBackground_IMG objects and creates CyberBackground_WORLD behind gameplay instead.
/// </summary>
public static class EverMotionPolishHelper
{
    private const string MenuPath = "Team EchoCare/Polish EverMotion HUD";
    private const string OldCanvasBackgroundObjectName = "CyberBackground_IMG";
    private const string WorldBackgroundObjectName = "CyberBackground_WORLD";

    private static readonly Color32 DeepNavyCamera = new Color32(0x0B, 0x0F, 0x19, 0xFF);
    private static readonly Color32 GlassPanelColor = new Color32(0x01, 0x06, 0x10, 150);
    private static readonly Color32 NeonCyanOutline = new Color32(0x00, 0xF2, 0xFF, 180);

    static EverMotionPolishHelper()
    {
        EditorApplication.delayCall += AutoRepairBadCanvasBackground;
    }

    [MenuItem(MenuPath)]
    public static void PolishEverMotionHud()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = Object.FindFirstObjectByType<Camera>();
        }

        if (mainCamera != null)
        {
            Undo.RecordObject(mainCamera, "Polish Main Camera");
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = DeepNavyCamera;
            EditorUtility.SetDirty(mainCamera);
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("EverMotion Polish failed: no Canvas found in the open scene.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Polish EverMotion HUD");

        GameObject backgroundObject = CreateOrUpdateWorldBackground(canvas.transform, mainCamera);
        CreateOrUpdateHudBase(canvas.transform);
        ApplyGlassPolishToGameplayPanels(canvas.transform);
        ApplyHudBaseLayout(canvas.transform);
        ApplyGlassPolishToEndStatePanels(canvas.transform);

        Selection.activeGameObject = backgroundObject;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("EverMotion HUD polished. image_4.png is now a world background behind the robot/cannons, not a Canvas overlay.");
    }

    private static GameObject CreateOrUpdateWorldBackground(Transform canvasTransform, Camera mainCamera)
    {
        Transform oldCanvasBackground = canvasTransform.Find(OldCanvasBackgroundObjectName);
        Sprite oldSprite = null;

        if (oldCanvasBackground != null)
        {
            Image oldImage = oldCanvasBackground.GetComponent<Image>();
            oldSprite = oldImage != null ? oldImage.sprite : null;
            oldCanvasBackground.gameObject.SetActive(false);
            EditorUtility.SetDirty(oldCanvasBackground.gameObject);
        }

        GameObject backgroundObject = GameObject.Find(WorldBackgroundObjectName);
        if (backgroundObject == null)
        {
            backgroundObject = new GameObject(WorldBackgroundObjectName, typeof(SpriteRenderer));
        }

        SpriteRenderer renderer = backgroundObject.GetComponent<SpriteRenderer>();
        Sprite foundImage4 = TryFindImage4Sprite();
        Sprite importedBackground = foundImage4 != null ? foundImage4 : oldSprite;
        if (importedBackground != null)
        {
            renderer.sprite = importedBackground;
        }

        renderer.color = new Color32(255, 255, 255, 220);
        renderer.sortingOrder = -500;
        FitWorldBackgroundToCamera(backgroundObject.transform, renderer.sprite, mainCamera);

        EditorUtility.SetDirty(backgroundObject);
        return backgroundObject;
    }

    private static void FitWorldBackgroundToCamera(Transform backgroundTransform, Sprite sprite, Camera mainCamera)
    {
        if (sprite == null || mainCamera == null || !mainCamera.orthographic)
        {
            backgroundTransform.position = new Vector3(0f, 0f, 9f);
            backgroundTransform.localScale = Vector3.one;
            return;
        }

        float cameraHeight = mainCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * mainCamera.aspect;
        Bounds spriteBounds = sprite.bounds;
        float scale = Mathf.Max(cameraWidth / spriteBounds.size.x, cameraHeight / spriteBounds.size.y);

        backgroundTransform.position = new Vector3(mainCamera.transform.position.x, mainCamera.transform.position.y + 0.15f, 9f);
        backgroundTransform.localScale = new Vector3(scale, scale, 1f);
    }

    private static void AutoRepairBadCanvasBackground()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Transform oldCanvasBackground = canvas.transform.Find(OldCanvasBackgroundObjectName);
        bool needsBackgroundRepair = oldCanvasBackground != null && oldCanvasBackground.gameObject.activeSelf;
        bool needsHudBase = canvas.transform.Find("HUD_Base_IMG") == null && TryFindHudBaseSprite() != null;

        if (!needsBackgroundRepair && !needsHudBase)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = Object.FindFirstObjectByType<Camera>();
        }

        if (needsBackgroundRepair)
        {
            CreateOrUpdateWorldBackground(canvas.transform, mainCamera);
        }

        CreateOrUpdateHudBase(canvas.transform);
        ApplyGlassPolishToGameplayPanels(canvas.transform);
        ApplyHudBaseLayout(canvas.transform);
        ApplyGlassPolishToEndStatePanels(canvas.transform);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("EverMotion auto-repaired: polished HUD_Base layout and moved any bad Canvas background out of the UI layer.");
    }

    private static void CreateOrUpdateHudBase(Transform canvasTransform)
    {
        Sprite hudBaseSprite = TryFindHudBaseSprite();
        if (hudBaseSprite == null)
        {
            return;
        }

        Transform existing = canvasTransform.Find("HUD_Base_IMG");
        GameObject hudBaseObject = existing != null
            ? existing.gameObject
            : new GameObject("HUD_Base_IMG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        hudBaseObject.transform.SetParent(canvasTransform, false);
        hudBaseObject.transform.SetAsFirstSibling();

        RectTransform rect = hudBaseObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 24f);
        rect.sizeDelta = new Vector2(1680f, 145f);
        rect.localScale = Vector3.one;

        Image image = hudBaseObject.GetComponent<Image>();
        image.sprite = hudBaseSprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = true;

        EditorUtility.SetDirty(hudBaseObject);
    }

    private static void ApplyHudBaseLayout(Transform canvasTransform)
    {
        if (canvasTransform.Find("HUD_Base_IMG") == null)
        {
            return;
        }

        MakePanelTransparent("HUD_Bottom_GlassPanel");
        MakePanelTransparent("Panel_Timer");
        MakePanelTransparent("Panel_TargetCounter");

        SetRect("HUD_Bottom_GlassPanel", new Vector2(1040f, 92f), new Vector2(0f, 46f));
        SetRect("Panel_Timer", new Vector2(300f, 86f), new Vector2(190f, 46f));
        SetRect("Panel_TargetCounter", new Vector2(300f, 86f), new Vector2(-190f, 46f));

        PositionText("Text_Timer", new Vector2(250f, 58f), Vector2.zero, 32);
        PositionText("Text_TargetCounter", new Vector2(250f, 58f), Vector2.zero, 32);
        PositionText("Text_EnergyLabel", new Vector2(580f, 24f), new Vector2(0f, 24f), 14);

        GameObject energyBar = GameObject.Find("EnergyBar");
        if (energyBar != null)
        {
            RectTransform rect = energyBar.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(560f, 18f);
                rect.anchoredPosition = new Vector2(0f, -23f);
            }

            Image rootImage = energyBar.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.color = new Color32(0, 0, 0, 0);
            }

            foreach (Image image in energyBar.GetComponentsInChildren<Image>(true))
            {
                string lowerName = image.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("fill"))
                {
                    image.color = new Color32(0, 242, 255, 255);
                }
            }

            EditorUtility.SetDirty(energyBar);
        }
    }

    private static void MakePanelTransparent(string objectName)
    {
        GameObject panel = GameObject.Find(objectName);
        if (panel == null)
        {
            return;
        }

        Image image = panel.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color32(0, 0, 0, 0);
            image.raycastTarget = false;
        }

        Outline outline = panel.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        EditorUtility.SetDirty(panel);
    }

    private static void PositionText(string textName, Vector2 size, Vector2 anchoredPosition, int fontSize)
    {
        GameObject textObject = GameObject.Find(textName);
        if (textObject == null)
        {
            return;
        }

        RectTransform rect = textObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        Text text = textObject.GetComponent<Text>();
        if (text != null)
        {
            text.color = Color.white;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
        }

        Shadow shadow = textObject.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = textObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color32(0, 0, 0, 190);
        shadow.effectDistance = new Vector2(2f, -2f);

        EditorUtility.SetDirty(textObject);
    }

    private static void SetRect(string objectName, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            return;
        }

        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        EditorUtility.SetDirty(target);
    }

    private static void ApplyGlassPolishToGameplayPanels(Transform canvasTransform)
    {
        HashSet<GameObject> panels = new HashSet<GameObject>();

        AddNamedPanel(panels, "HUD_Bottom_GlassPanel");
        AddNamedPanel(panels, "Panel_Timer");
        AddNamedPanel(panels, "Panel_TargetCounter");
        AddTextParentPanel(panels, canvasTransform, "Text_Timer");
        AddTextParentPanel(panels, canvasTransform, "Text_EnergyLabel");
        AddTextParentPanel(panels, canvasTransform, "Text_TargetCounter");

        foreach (GameObject panel in panels)
        {
            ApplyGlassStyle(panel, NeonCyanOutline);
        }
    }

    private static void ApplyGlassPolishToEndStatePanels(Transform canvasTransform)
    {
        string[] possiblePanelNames =
        {
            "Panel_GameClear",
            "Panel_GameOver",
            "GameClearPanel",
            "GameOverPanel"
        };

        foreach (string panelName in possiblePanelNames)
        {
            Transform panel = FindChildRecursive(canvasTransform, panelName);
            if (panel != null)
            {
                ApplyGlassStyle(panel.gameObject, NeonCyanOutline);
            }
        }
    }

    private static void AddNamedPanel(HashSet<GameObject> panels, string panelName)
    {
        GameObject panel = GameObject.Find(panelName);
        if (panel != null && panel.GetComponent<Image>() != null)
        {
            panels.Add(panel);
        }
    }

    private static void AddTextParentPanel(HashSet<GameObject> panels, Transform canvasTransform, string textName)
    {
        Transform textTransform = FindChildRecursive(canvasTransform, textName);
        if (textTransform == null || textTransform.parent == null)
        {
            return;
        }

        Transform parent = textTransform.parent;
        Image parentImage = parent.GetComponent<Image>();
        if (parentImage != null)
        {
            panels.Add(parent.gameObject);
        }
    }

    private static void ApplyGlassStyle(GameObject panel, Color32 outlineColor)
    {
        if (panel == null)
        {
            return;
        }

        Image image = panel.GetComponent<Image>();
        if (image == null)
        {
            image = panel.AddComponent<Image>();
        }

        image.color = GlassPanelColor;

        Outline outline = panel.GetComponent<Outline>();
        if (outline == null)
        {
            outline = panel.AddComponent<Outline>();
        }

        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        EditorUtility.SetDirty(panel);
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Sprite TryFindImage4Sprite()
    {
        string[] spriteGuids = AssetDatabase.FindAssets("image_4 t:Sprite");
        foreach (string guid in spriteGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = LoadFirstSpriteAtPath(path);
            if (sprite != null)
            {
                return sprite;
            }
        }

        string[] textureGuids = AssetDatabase.FindAssets("image_4 t:Texture2D");
        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = LoadFirstSpriteAtPath(path);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    private static Sprite TryFindHudBaseSprite()
    {
        string[] spriteGuids = AssetDatabase.FindAssets("HUD_Base t:Sprite");
        foreach (string guid in spriteGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = LoadFirstSpriteAtPath(path);
            if (sprite != null)
            {
                return sprite;
            }
        }

        string[] textureGuids = AssetDatabase.FindAssets("HUD_Base t:Texture2D");
        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = LoadFirstSpriteAtPath(path);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    private static Sprite LoadFirstSpriteAtPath(string assetPath)
    {
        Sprite directSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (directSprite != null)
        {
            return directSprite;
        }

        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (Object asset in allAssets)
        {
            if (asset is Sprite sprite)
            {
                return sprite;
            }
        }

        return null;
    }
}
