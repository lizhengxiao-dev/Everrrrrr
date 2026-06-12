using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime UI polish pass for the EverMotion rehab HUD.
/// Applies a dark Web3/cyberpunk glassmorphism look without requiring manual Inspector work.
/// </summary>
public static class EverMotionUIPolisher
{
    private static Sprite sciFiGridSprite;

    public static void ApplyPolish()
    {
        MoveWrongCanvasBackgroundIntoWorld();
        PolishCameraAndBackground();
        bool usingHudBase = PolishHudBaseOverlay();
        if (!usingHudBase)
        {
            PolishHudPanels();
        }
        PolishEnergyBar();
        PolishText();
        PolishEndPanels();
    }

    private static void PolishCameraAndBackground()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = HasWorldBackgroundSprite()
                ? HexColor(0xE8, 0xF4, 0xFF, 0xFF)
                : HexColor(0x0B, 0x0F, 0x19, 0xFF);
        }

        if (HasWorldBackgroundSprite())
        {
            GameObject existingGrid = FindSceneObject("DeepSciFi_Background_Grid");
            if (existingGrid != null)
            {
                existingGrid.SetActive(false);
            }

            return;
        }

        GameObject background = FindSceneObject("DeepSciFi_Background_Grid");
        if (background == null)
        {
            background = new GameObject("DeepSciFi_Background_Grid", typeof(SpriteRenderer));
        }

        SpriteRenderer renderer = background.GetComponent<SpriteRenderer>();
        renderer.sprite = GetSciFiGridSprite();
        renderer.color = new Color32(30, 250, 255, 42);
        renderer.sortingOrder = -250;

        if (camera != null && camera.orthographic)
        {
            float height = camera.orthographicSize * 2f;
            float width = height * camera.aspect;
            background.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 8f);
            background.transform.localScale = new Vector3(width / 10f, height / 10f, 1f);
        }
        else
        {
            background.transform.position = new Vector3(0f, 0f, 8f);
            background.transform.localScale = new Vector3(2.2f, 1.4f, 1f);
        }
    }

    private static void MoveWrongCanvasBackgroundIntoWorld()
    {
        GameObject canvasBackground = FindSceneObject("CyberBackground_IMG");
        Image canvasImage = canvasBackground != null ? canvasBackground.GetComponent<Image>() : null;
        Sprite importedBackgroundSprite = canvasImage != null ? canvasImage.sprite : null;

        if (canvasBackground != null)
        {
            canvasBackground.SetActive(false);
        }

        if (importedBackgroundSprite == null)
        {
            return;
        }

        GameObject worldBackground = FindSceneObject("CyberBackground_WORLD");
        if (worldBackground == null)
        {
            worldBackground = new GameObject("CyberBackground_WORLD", typeof(SpriteRenderer));
        }

        SpriteRenderer renderer = worldBackground.GetComponent<SpriteRenderer>();
        renderer.sprite = importedBackgroundSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = -500;

        Camera camera = Camera.main;
        if (camera != null && camera.orthographic)
        {
            float cameraHeight = camera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * camera.aspect;
            Bounds spriteBounds = importedBackgroundSprite.bounds;
            float scale = Mathf.Max(cameraWidth / spriteBounds.size.x, cameraHeight / spriteBounds.size.y);

            worldBackground.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y + 0.15f, 9f);
            worldBackground.transform.localScale = new Vector3(scale, scale, 1f);
        }
        else
        {
            worldBackground.transform.position = new Vector3(0f, 0f, 9f);
            worldBackground.transform.localScale = Vector3.one;
        }
    }

    private static bool HasWorldBackgroundSprite()
    {
        GameObject worldBackground = FindSceneObject("CyberBackground_WORLD");
        if (worldBackground == null || !worldBackground.activeInHierarchy)
        {
            return false;
        }

        SpriteRenderer renderer = worldBackground.GetComponent<SpriteRenderer>();
        return renderer != null && renderer.sprite != null;
    }

    private static void PolishHudPanels()
    {
        PolishPanel("HUD_Bottom_GlassPanel", new Color32(0, 0, 0, 118), new Color32(0, 242, 255, 120), new Vector2(2f, -2f));
        PolishPanel("Panel_Timer", new Color32(0, 0, 0, 150), new Color32(0, 242, 255, 180), new Vector2(1f, -1f));
        PolishPanel("Panel_TargetCounter", new Color32(0, 0, 0, 150), new Color32(255, 40, 230, 175), new Vector2(1f, -1f));

        SetRect("HUD_Bottom_GlassPanel", new Vector2(980f, 96f), new Vector2(0f, 54f));
        SetRect("Panel_Timer", new Vector2(260f, 70f), new Vector2(210f, 72f));
        SetRect("Panel_TargetCounter", new Vector2(260f, 70f), new Vector2(-210f, 72f));
    }

    private static bool PolishHudBaseOverlay()
    {
        GameObject hudBase = FindSceneObject("HUD_Base_IMG");
        if (hudBase == null || !hudBase.activeInHierarchy)
        {
            return false;
        }

        RectTransform baseRect = hudBase.GetComponent<RectTransform>();
        if (baseRect != null)
        {
            baseRect.anchorMin = new Vector2(0.5f, 0f);
            baseRect.anchorMax = new Vector2(0.5f, 0f);
            baseRect.pivot = new Vector2(0.5f, 0f);
            baseRect.anchoredPosition = new Vector2(0f, 24f);
            baseRect.sizeDelta = new Vector2(1680f, 145f);
            hudBase.transform.SetAsFirstSibling();
        }

        Image baseImage = hudBase.GetComponent<Image>();
        if (baseImage != null)
        {
            baseImage.raycastTarget = false;
            baseImage.color = Color.white;
            baseImage.preserveAspect = true;
        }

        MakePanelTransparent("HUD_Bottom_GlassPanel");
        MakePanelTransparent("Panel_Timer");
        MakePanelTransparent("Panel_TargetCounter");

        SetRect("Panel_Timer", new Vector2(300f, 86f), new Vector2(190f, 46f));
        SetRect("Panel_TargetCounter", new Vector2(300f, 86f), new Vector2(-190f, 46f));
        SetRect("HUD_Bottom_GlassPanel", new Vector2(1040f, 92f), new Vector2(0f, 46f));

        PositionText("Text_Timer", new Vector2(250f, 58f), Vector2.zero, 32);
        PositionText("Text_TargetCounter", new Vector2(250f, 58f), Vector2.zero, 32);
        PositionText("Text_EnergyLabel", new Vector2(580f, 24f), new Vector2(0f, 24f), 14);

        return true;
    }

    private static void MakePanelTransparent(string objectName)
    {
        GameObject panel = FindSceneObject(objectName);
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
    }

    private static void PositionText(string textName, Vector2 size, Vector2 anchoredPosition, int fontSize)
    {
        GameObject textObject = FindSceneObject(textName);
        if (textObject == null)
        {
            return;
        }

        Text text = textObject.GetComponent<Text>();
        if (text != null)
        {
            text.color = Color.white;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
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
    }

    private static void PolishPanel(string objectName, Color32 fillColor, Color32 outlineColor, Vector2 outlineDistance)
    {
        GameObject panel = FindSceneObject(objectName);
        if (panel == null)
        {
            return;
        }

        Image image = panel.GetComponent<Image>();
        if (image != null)
        {
            image.color = fillColor;
            image.raycastTarget = false;
        }

        Outline outline = AddOrGet<Outline>(panel);
        outline.effectColor = outlineColor;
        outline.effectDistance = outlineDistance;
        outline.useGraphicAlpha = true;
    }

    private static void PolishEnergyBar()
    {
        GameObject energyObject = FindSceneObject("EnergyBar");
        if (energyObject == null)
        {
            return;
        }

        Image rootImage = energyObject.GetComponent<Image>();
        if (rootImage != null)
        {
            bool usingHudBase = FindSceneObject("HUD_Base_IMG") != null && FindSceneObject("HUD_Base_IMG").activeInHierarchy;
            rootImage.color = usingHudBase ? new Color32(0, 0, 0, 0) : new Color32(0, 0, 0, 95);
        }

        Outline outline = AddOrGet<Outline>(energyObject);
        outline.effectColor = new Color32(0, 242, 255, 135);
        outline.effectDistance = new Vector2(1f, -1f);

        Slider slider = energyObject.GetComponent<Slider>();
        if (slider != null)
        {
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
        }

        Image[] images = energyObject.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            string lowerName = image.gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("background"))
            {
                image.color = new Color32(26, 26, 26, 205);
            }
            else if (lowerName.Contains("fill"))
            {
                image.color = new Color32(0, 242, 255, 255);
                image.type = Image.Type.Simple;
            }
        }

        RectTransform rect = energyObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            bool usingHudBase = FindSceneObject("HUD_Base_IMG") != null && FindSceneObject("HUD_Base_IMG").activeInHierarchy;
            rect.sizeDelta = usingHudBase ? new Vector2(560f, 18f) : new Vector2(680f, 24f);
            rect.anchoredPosition = usingHudBase ? new Vector2(0f, -23f) : new Vector2(0f, -14f);
        }
    }

    private static void PolishText()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Text[] texts = canvas.GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            string lowerName = text.gameObject.name.ToLowerInvariant();
            text.color = lowerName.Contains("energy")
                ? new Color32(210, 255, 255, 245)
                : new Color32(255, 255, 255, 255);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;

            if (lowerName.Contains("timer"))
            {
                text.fontSize = 34;
            }
            else if (lowerName.Contains("targetcounter"))
            {
                text.fontSize = 34;
            }
            else if (lowerName.Contains("energy"))
            {
                text.fontSize = 16;
            }

            Shadow shadow = AddOrGet<Shadow>(text.gameObject);
            shadow.effectColor = new Color32(0, 242, 255, 145);
            shadow.effectDistance = new Vector2(2f, -2f);

            Outline outline = text.GetComponent<Outline>();
            if (outline == null)
            {
                outline = text.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color32(0, 0, 0, 170);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }
    }

    private static void PolishEndPanels()
    {
        PolishPanel("Panel_GameClear", new Color32(0, 0, 0, 190), new Color32(0, 242, 255, 180), new Vector2(2f, -2f));
        PolishPanel("Panel_GameOver", new Color32(0, 0, 0, 190), new Color32(255, 40, 100, 185), new Vector2(2f, -2f));
    }

    private static void SetRect(string objectName, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject target = FindSceneObject(objectName);
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
    }

    private static void SetTextRect(Text text, Vector2 size, Vector2 anchoredPosition)
    {
        RectTransform rect = text.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static T AddOrGet<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform transform in transforms)
        {
            if (transform.name == objectName && transform.gameObject.scene.IsValid())
            {
                return transform.gameObject;
            }
        }

        return GameObject.Find(objectName);
    }

    private static Color32 HexColor(byte r, byte g, byte b, byte a)
    {
        return new Color32(r, g, b, a);
    }

    private static Sprite GetSciFiGridSprite()
    {
        if (sciFiGridSprite != null)
        {
            return sciFiGridSprite;
        }

        const int width = 1024;
        const int height = 576;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 line = new Color32(0, 242, 255, 58);
        Color32 soft = new Color32(0, 242, 255, 24);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        for (int x = 0; x < width; x += 128)
        {
            DrawLine(texture, new Vector2(x, 0f), new Vector2(x, height - 1f), soft, 1);
        }

        for (int y = 0; y < height; y += 96)
        {
            DrawLine(texture, new Vector2(0f, y), new Vector2(width - 1f, y), soft, 1);
        }

        DrawLine(texture, new Vector2(0f, 92f), new Vector2(280f, 92f), line, 2);
        DrawLine(texture, new Vector2(280f, 92f), new Vector2(360f, 40f), line, 2);
        DrawLine(texture, new Vector2(360f, 40f), new Vector2(680f, 40f), line, 2);
        DrawLine(texture, new Vector2(680f, 40f), new Vector2(750f, 92f), line, 2);
        DrawLine(texture, new Vector2(750f, 92f), new Vector2(width - 1f, 92f), line, 2);

        DrawLine(texture, new Vector2(0f, 480f), new Vector2(340f, 480f), soft, 2);
        DrawLine(texture, new Vector2(620f, 470f), new Vector2(width - 1f, 470f), soft, 2);

        texture.Apply();
        sciFiGridSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        sciFiGridSprite.name = "Runtime_Deep_SciFi_Grid";
        return sciFiGridSprite;
    }

    private static void DrawLine(Texture2D texture, Vector2 start, Vector2 end, Color32 color, int thickness)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(start, end));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(start, end, i / (float)steps);
            PaintPixel(texture, Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), color, thickness);
        }
    }

    private static void PaintPixel(Texture2D texture, int x, int y, Color32 color, int radius)
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
}
