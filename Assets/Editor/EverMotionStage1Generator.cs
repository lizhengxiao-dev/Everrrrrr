using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class EverMotionStage1Generator
{
    private const string CanvasName = "SciFi_UI_Canvas";
    private const string PrefabsFolder = "Assets/Prefabs";
    private const string LaserPrefabPath = PrefabsFolder + "/LaserPrefab.prefab";

    [MenuItem("EverMotion/Phase 1 - Generate Official HUD + Cannons")]
    public static void Generate()
    {
        DeleteIfExists(CanvasName);
        DeleteIfExists("EventSystem");
        DeleteIfExists("Cannon_Left");
        DeleteIfExists("Cannon_Right");
        DeleteIfExists("Cannon_Top");

        CreateCanvasAndHud();
        CreateEventSystem();
        CreateCannons();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("EverMotion Phase 1 generated: official 120s/12-block HUD + visible cannons.");
    }

    [MenuItem("EverMotion/Phase 2 - Generate Laser System")]
    public static void GenerateLaserSystem()
    {
        GameObject maleRobot = GameObject.Find("Male0001");
        GameObject cannonLeft = GameObject.Find("Cannon_Left");
        GameObject cannonRight = GameObject.Find("Cannon_Right");
        GameObject cannonTop = GameObject.Find("Cannon_Top");

        if (maleRobot == null || cannonLeft == null || cannonRight == null || cannonTop == null)
        {
            Debug.LogError("Phase 2 needs Male0001, Cannon_Left, Cannon_Right, and Cannon_Top in the scene. Run Phase 1 first.");
            return;
        }

        LaserBehavior laserPrefab = CreateOrReplaceLaserPrefab();
        DeleteIfExists("GameManager");

        GameObject gameManager = new GameObject("GameManager");
        LaserSpawner spawner = gameManager.AddComponent<LaserSpawner>();
        spawner.laserPrefab = laserPrefab;
        spawner.robotTarget = maleRobot.transform;
        spawner.cannonLeft = cannonLeft.transform;
        spawner.cannonRight = cannonRight.transform;
        spawner.cannonTop = cannonTop.transform;
        spawner.spawnInterval = 5f;
        spawner.autoStart = true;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("EverMotion Phase 2 generated: LaserPrefab + GameManager LaserSpawner bound to cannons and Male0001.");
    }

    [MenuItem("EverMotion/Phase 3 - Generate Shields + Collisions")]
    public static void GenerateShieldsAndCollisions()
    {
        GameObject maleRobot = GameObject.Find("Male0001");

        if (maleRobot == null)
        {
            Debug.LogError("Phase 3 needs Male0001 in the scene.");
            return;
        }

        EnsureTag("Shield");
        maleRobot.tag = "Player";

        GameObject shieldLeft = CreateShield(maleRobot.transform, "Shield_Left", new Vector3(-0.75f, 0.25f, 0f), new Vector3(0.75f, 0.75f, 1f));
        GameObject shieldRight = CreateShield(maleRobot.transform, "Shield_Right", new Vector3(0.75f, 0.25f, 0f), new Vector3(0.75f, 0.75f, 1f));
        GameObject shieldTop = CreateShield(maleRobot.transform, "Shield_Top", new Vector3(0f, 1.25f, 0f), new Vector3(0.9f, 0.9f, 1f));

        Collider2D bodyCollider = maleRobot.GetComponent<Collider2D>();
        if (bodyCollider == null)
        {
            CapsuleCollider2D capsule = maleRobot.AddComponent<CapsuleCollider2D>();
            capsule.isTrigger = true;
            capsule.size = new Vector2(1.25f, 2.15f);
            capsule.offset = new Vector2(0f, 0.45f);
        }
        else
        {
            bodyCollider.isTrigger = true;
        }

        ShieldManager shieldManager = maleRobot.GetComponent<ShieldManager>();
        if (shieldManager == null)
        {
            shieldManager = maleRobot.AddComponent<ShieldManager>();
        }

        shieldManager.defenseDirectionParameter = "DefenseDirection";

        GameObject blockVfxPrefab = CreateOrReplaceBlockVfxPrefab();
        GameObject laserPrefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(LaserPrefabPath);
        LaserBehavior laserPrefab = laserPrefabObject != null ? laserPrefabObject.GetComponent<LaserBehavior>() : null;
        if (laserPrefab != null)
        {
            laserPrefab.shieldBlockVfxPrefab = blockVfxPrefab;
            EditorUtility.SetDirty(laserPrefab);
        }
        else
        {
            Debug.LogWarning("LaserPrefab was not found. Run Phase 2 again later to bind Shield Block VFX automatically.");
        }

        shieldLeft.SetActive(false);
        shieldRight.SetActive(false);
        shieldTop.SetActive(false);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("EverMotion Phase 3 generated: hex shields, ShieldManager, Player/Shield tags, colliders, and block sparks VFX.");
    }

    [MenuItem("EverMotion/Phase 4 - Generate Game Lifecycle")]
    public static void GenerateGameLifecycle()
    {
        GameObject gameManagerObject = GameObject.Find("GameManager");
        if (gameManagerObject == null)
        {
            gameManagerObject = new GameObject("GameManager");
        }

        EverMotionGameManager gameManager = gameManagerObject.GetComponent<EverMotionGameManager>();
        if (gameManager == null)
        {
            gameManager = gameManagerObject.AddComponent<EverMotionGameManager>();
        }

        GameObject canvasObject = GameObject.Find(CanvasName);
        if (canvasObject == null)
        {
            Debug.LogError("Phase 4 needs SciFi_UI_Canvas. Run Phase 1 first.");
            return;
        }

        gameManager.sessionDuration = 120f;
        gameManager.requiredPerfectBlocks = 12;
        gameManager.timeRemaining = 120f;
        gameManager.targetBlocks = 12;
        gameManager.blocksCaught = 0;
        gameManager.isGameOver = false;
        gameManager.timerText = FindComponentByName<Text>("Text_Timer");
        gameManager.targetCounterText = FindComponentByName<Text>("Text_TargetCounter");
        gameManager.counterText = gameManager.targetCounterText;
        gameManager.energyBar = FindComponentByName<Slider>("EnergyBar");
        gameManager.laserSpawner = gameManagerObject.GetComponent<LaserSpawner>();
        gameManager.gameClearPanel = CreateEndStatePanel(canvasObject.transform, "Panel_GameClear", "GAME CLEAR", new Color32(0, 255, 255, 190));
        gameManager.gameOverPanel = CreateEndStatePanel(canvasObject.transform, "Panel_GameOver", "GAME OVER", new Color32(255, 0, 80, 190));
        gameManager.gameClearPanel.SetActive(false);
        gameManager.gameOverPanel.SetActive(false);

        EditorUtility.SetDirty(gameManager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("EverMotion Phase 4 generated: 120s timer, 12-block energy lifecycle, and victory/failure UI.");
    }

    [MenuItem("EverMotion/Phase 5 - Connect MediaPipe UDP")]
    public static void ConnectMediaPipeUdp()
    {
        GameObject rehabTarget = GameObject.Find("RehabTarget");
        GameObject maleRobot = GameObject.Find("Male0001");

        if (rehabTarget == null)
        {
            Debug.LogError("Phase 5 needs RehabTarget in the scene.");
            return;
        }

        HandTracker[] allTrackers = UnityEngine.Object.FindObjectsByType<HandTracker>(FindObjectsSortMode.None);
        foreach (HandTracker tracker in allTrackers)
        {
            if (tracker.gameObject != rehabTarget)
            {
                tracker.enabled = false;
                EditorUtility.SetDirty(tracker);
            }
        }

        HandTracker handTracker = rehabTarget.GetComponent<HandTracker>();
        if (handTracker == null)
        {
            handTracker = rehabTarget.AddComponent<HandTracker>();
        }

        handTracker.enabled = true;
        handTracker.listenPort = 5052;
        handTracker.worldMinX = -8f;
        handTracker.worldMaxX = 8f;
        handTracker.worldMinY = -5f;
        handTracker.worldMaxY = 5f;
        handTracker.mirrorX = true;
        handTracker.invertY = true;
        handTracker.smoothSpeed = 10f;
        handTracker.fixedZ = 0f;
        handTracker.idleWorldPosition = Vector2.zero;
        handTracker.leftWorldPosition = new Vector2(-4.5f, 0f);
        handTracker.rightWorldPosition = new Vector2(4.5f, 0f);
        handTracker.upWorldPosition = new Vector2(0f, 4f);

        RehabTarget oldTargetBehavior = rehabTarget.GetComponent<RehabTarget>();
        if (oldTargetBehavior != null)
        {
            oldTargetBehavior.enabled = false;
            EditorUtility.SetDirty(oldTargetBehavior);
        }

        if (maleRobot != null)
        {
            RobotAnimationController robotController = maleRobot.GetComponent<RobotAnimationController>();
            if (robotController != null)
            {
                robotController.playerHandTarget = rehabTarget.transform;
                EditorUtility.SetDirty(robotController);
            }
        }

        EditorUtility.SetDirty(handTracker);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("EverMotion Phase 5 connected: RehabTarget now listens for MediaPipe UDP on port 5052.");
    }

    [MenuItem("EverMotion/Polish - Generate Cannon Charge Warnings")]
    public static void GenerateCannonChargeWarnings()
    {
        GameObject cannonLeft = GameObject.Find("Cannon_Left");
        GameObject cannonRight = GameObject.Find("Cannon_Right");
        GameObject cannonTop = GameObject.Find("Cannon_Top");

        if (cannonLeft == null || cannonRight == null || cannonTop == null)
        {
            Debug.LogError("Cannon charge warnings need Cannon_Left, Cannon_Right, and Cannon_Top. Run Phase 1 first.");
            return;
        }

        RepairVisibleCannons();
        CreateOrUpdateChargeWarning(cannonLeft.transform);
        CreateOrUpdateChargeWarning(cannonRight.transform);
        CreateOrUpdateChargeWarning(cannonTop.transform);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("EverMotion polish generated: massive neon WarningGlow objects added to all cannons.");
    }

    [MenuItem("EverMotion/Polish - Repair Visible Cannons")]
    public static void RepairVisibleCannons()
    {
        Camera camera = Camera.main;
        float halfHeight = camera != null && camera.orthographic ? camera.orthographicSize : 5f;
        float halfWidth = camera != null && camera.orthographic ? halfHeight * camera.aspect : 8.89f;
        Vector3 robotChest = new Vector3(0.1f, -0.92f + 0.75f * 0.435f, 0f);
        Vector3 leftPosition = new Vector3(-halfWidth + 1.12f, -0.78f, 0f);
        Vector3 rightPosition = new Vector3(halfWidth - 1.12f, -0.78f, 0f);
        Vector3 topPosition = new Vector3(0.1f, halfHeight - 0.38f, 0f);

        ApplyArcadeRobotScale();
        RepairOneCannon("Cannon_Left", leftPosition, GetAimAngleZ(leftPosition, robotChest), new Color32(255, 255, 255, 255), new Color32(0, 255, 255, 255));
        RepairOneCannon("Cannon_Right", rightPosition, GetAimAngleZ(rightPosition, robotChest), new Color32(255, 255, 255, 255), new Color32(255, 0, 255, 255));
        RepairOneCannon("Cannon_Top", topPosition, GetAimAngleZ(topPosition, robotChest), new Color32(255, 255, 255, 255), new Color32(0, 255, 255, 255));

        RebindGameplayReferences();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("EverMotion polish repaired: cannons moved inward, enlarged, and brightened for visibility.");
    }

    [MenuItem("EverMotion/Repair - Rebind Gameplay References")]
    public static void RebindGameplayReferences()
    {
        GameObject gameManagerObject = GameObject.Find("GameManager");
        if (gameManagerObject == null)
        {
            gameManagerObject = new GameObject("GameManager");
        }

        LaserSpawner spawner = gameManagerObject.GetComponent<LaserSpawner>();
        if (spawner == null)
        {
            spawner = gameManagerObject.AddComponent<LaserSpawner>();
        }

        GameObject laserPrefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(LaserPrefabPath);
        spawner.laserPrefab = laserPrefabObject != null ? laserPrefabObject.GetComponent<LaserBehavior>() : null;

        GameObject maleRobot = GameObject.Find("Male0001");
        GameObject cannonLeft = GameObject.Find("Cannon_Left");
        GameObject cannonRight = GameObject.Find("Cannon_Right");
        GameObject cannonTop = GameObject.Find("Cannon_Top");

        spawner.robotTarget = maleRobot != null ? maleRobot.transform : null;
        spawner.cannonLeft = cannonLeft != null ? cannonLeft.transform : null;
        spawner.cannonRight = cannonRight != null ? cannonRight.transform : null;
        spawner.cannonTop = cannonTop != null ? cannonTop.transform : null;
        spawner.spawnInterval = 5f;
        spawner.autoStart = true;

        EverMotionGameManager gameManager = gameManagerObject.GetComponent<EverMotionGameManager>();
        if (gameManager != null)
        {
            gameManager.laserSpawner = spawner;
            gameManager.timerText = FindComponentByName<Text>("Text_Timer");
            gameManager.targetCounterText = FindComponentByName<Text>("Text_TargetCounter");
            gameManager.counterText = gameManager.targetCounterText;
            gameManager.energyBar = FindComponentByName<Slider>("EnergyBar");
            gameManager.gameClearPanel = GameObject.Find("Panel_GameClear");
            gameManager.gameOverPanel = GameObject.Find("Panel_GameOver");
            EditorUtility.SetDirty(gameManager);
        }

        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("EverMotion repaired: gameplay references rebound on GameManager.");
    }

    private static void CreateCanvasAndHud()
    {
        GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject hudPanel = CreatePanel("HUD_Bottom_GlassPanel", canvasObject.transform, new Color32(7, 17, 31, 145));
        RectTransform hudRect = hudPanel.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0.5f, 0f);
        hudRect.anchorMax = new Vector2(0.5f, 0f);
        hudRect.pivot = new Vector2(0.5f, 0.5f);
        hudRect.anchoredPosition = new Vector2(0f, 70f);
        hudRect.sizeDelta = new Vector2(1200f, 140f);
        AddOutline(hudPanel, new Color32(0, 255, 255, 180), new Vector2(2f, -2f));

        CreateEnergyBar(hudPanel.transform);
        CreateHudText("Text_EnergyLabel", "ENERGY CHARGE", hudPanel.transform, new Vector2(0f, 43f), new Vector2(700f, 26f), 22, new Color32(210, 255, 255, 235));
        CreateInfoPanel("Panel_Timer", "Text_Timer", "02:00", canvasObject.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(220f, 70f), new Vector2(360f, 100f), new Color32(0, 255, 255, 180), 44);
        CreateInfoPanel("Panel_TargetCounter", "Text_TargetCounter", "0 / 12", canvasObject.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-240f, 70f), new Vector2(380f, 100f), new Color32(255, 0, 255, 170), 42);
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        Image image = panel.GetComponent<Image>();
        image.sprite = GetUiSprite();
        image.type = Image.Type.Sliced;
        image.color = color;

        return panel;
    }

    private static void CreateEnergyBar(Transform parent)
    {
        GameObject sliderObject = new GameObject("EnergyBar", typeof(RectTransform), typeof(Slider), typeof(Image), typeof(Outline));
        sliderObject.transform.SetParent(parent, false);

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(0f, -10f);
        sliderRect.sizeDelta = new Vector2(700f, 35f);

        Image rootImage = sliderObject.GetComponent<Image>();
        rootImage.color = new Color32(10, 20, 35, 180);
        rootImage.sprite = GetUiSprite();
        rootImage.type = Image.Type.Sliced;

        Outline outline = sliderObject.GetComponent<Outline>();
        outline.effectColor = new Color32(255, 0, 255, 130);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject background = CreatePanel("Background", sliderObject.transform, new Color32(10, 20, 35, 180));
        StretchToParent(background.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        StretchToParent(fillArea.GetComponent<RectTransform>(), 8f, 8f, 6f, 6f);

        GameObject fill = CreatePanel("Fill", fillArea.transform, new Color32(0, 255, 255, 230));
        StretchToParent(fill.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 0f;
        slider.interactable = false;
        slider.targetGraphic = rootImage;
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.direction = Slider.Direction.LeftToRight;
    }

    private static void CreateInfoPanel(string panelName, string textName, string text, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color outlineColor, int fontSize)
    {
        GameObject panel = CreatePanel(panelName, parent, new Color32(7, 17, 31, 145));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = anchorMin;
        panelRect.anchorMax = anchorMax;
        panelRect.pivot = pivot;
        panelRect.anchoredPosition = position;
        panelRect.sizeDelta = size;
        AddOutline(panel, outlineColor, new Vector2(2f, -2f));

        Text uiText = CreateHudText(textName, text, panel.transform, Vector2.zero, Vector2.zero, fontSize, new Color32(220, 255, 255, 255));
        StretchToParent(uiText.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
    }

    private static GameObject CreateEndStatePanel(Transform canvasTransform, string panelName, string title, Color outlineColor)
    {
        DeleteIfExists(panelName);

        GameObject panel = CreatePanel(panelName, canvasTransform, new Color32(7, 17, 31, 205));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 90f);
        rect.sizeDelta = new Vector2(620f, 230f);
        AddOutline(panel, outlineColor, new Vector2(3f, -3f));

        CreateHudText(panelName + "_Title", title, panel.transform, new Vector2(0f, 42f), new Vector2(560f, 80f), 56, new Color32(235, 255, 255, 255));
        CreateHudText(panelName + "_Subtitle", title == "GAME CLEAR" ? "ENERGY FULLY CHARGED" : "ENERGY CHARGE FAILED", panel.transform, new Vector2(0f, -42f), new Vector2(560f, 48f), 28, new Color32(190, 255, 255, 230));

        return panel;
    }

    private static Text CreateHudText(string textName, string text, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color)
    {
        GameObject textObject = new GameObject(textName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = size;

        Text uiText = textObject.GetComponent<Text>();
        uiText.text = text;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.fontSize = fontSize;
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.color = color;

        return uiText;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

        if (inputSystemModuleType != null)
        {
            eventSystem.AddComponent(inputSystemModuleType);
            return;
        }

        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private static void CreateCannons()
    {
        Camera camera = Camera.main;
        float halfHeight = camera != null && camera.orthographic ? camera.orthographicSize : 5f;
        float halfWidth = camera != null && camera.orthographic ? halfHeight * camera.aspect : 8.89f;
        Vector3 robotChest = new Vector3(0.1f, -0.92f + 0.75f * 0.435f, 0f);
        Vector3 leftPosition = new Vector3(-halfWidth + 1.12f, -0.78f, 0f);
        Vector3 rightPosition = new Vector3(halfWidth - 1.12f, -0.78f, 0f);
        Vector3 topPosition = new Vector3(0.1f, halfHeight - 0.38f, 0f);

        CreateCannon("Cannon_Left", leftPosition, GetAimAngleZ(leftPosition, robotChest), new Color32(255, 255, 255, 255), new Color32(0, 255, 255, 255));
        CreateCannon("Cannon_Right", rightPosition, GetAimAngleZ(rightPosition, robotChest), new Color32(255, 255, 255, 255), new Color32(255, 0, 255, 255));
        CreateCannon("Cannon_Top", topPosition, GetAimAngleZ(topPosition, robotChest), new Color32(255, 255, 255, 255), new Color32(0, 255, 255, 255));
    }

    private static float GetAimAngleZ(Vector3 from, Vector3 to)
    {
        Vector2 direction = to - from;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    private static void CreateCannon(string name, Vector3 position, float rotationZ, Color32 bodyColor, Color32 glowColor)
    {
        GameObject cannon = new GameObject(name, typeof(SpriteRenderer));
        cannon.transform.position = position;
        cannon.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        cannon.transform.localScale = new Vector3(1.15f, 1.15f, 1f);

        SpriteRenderer body = cannon.GetComponent<SpriteRenderer>();
        body.sprite = GetCyberpunkCannonSprite();
        body.color = bodyColor;
        body.sortingOrder = 22;

        GameObject muzzle = new GameObject("MuzzleGlow", typeof(SpriteRenderer));
        muzzle.transform.SetParent(cannon.transform, false);
        muzzle.transform.localPosition = new Vector3(0.78f, 0f, -0.01f);
        muzzle.transform.localRotation = Quaternion.identity;
        muzzle.transform.localScale = new Vector3(0.32f, 0.32f, 1f);

        SpriteRenderer glow = muzzle.GetComponent<SpriteRenderer>();
        glow.sprite = GetCircleSprite();
        glow.color = glowColor;
        glow.sortingOrder = 24;

        CreateOrUpdateChargeWarning(cannon.transform);
    }

    private static void RepairOneCannon(string name, Vector3 position, float rotationZ, Color32 bodyColor, Color32 glowColor)
    {
        GameObject cannon = GameObject.Find(name);
        if (cannon == null)
        {
            CreateCannon(name, position, rotationZ, bodyColor, glowColor);
            return;
        }

        cannon.transform.position = position;
        cannon.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        cannon.transform.localScale = new Vector3(1.15f, 1.15f, 1f);

        SpriteRenderer body = cannon.GetComponent<SpriteRenderer>();
        if (body == null)
        {
            body = cannon.AddComponent<SpriteRenderer>();
        }

        body.sprite = GetCyberpunkCannonSprite();
        body.color = bodyColor;
        body.sortingOrder = 22;
        body.enabled = true;

        Transform muzzleTransform = cannon.transform.Find("MuzzleGlow");
        GameObject muzzle = muzzleTransform != null
            ? muzzleTransform.gameObject
            : new GameObject("MuzzleGlow", typeof(SpriteRenderer));

        muzzle.transform.SetParent(cannon.transform, false);
        muzzle.transform.localPosition = new Vector3(0.78f, 0f, -0.01f);
        muzzle.transform.localRotation = Quaternion.identity;
        muzzle.transform.localScale = new Vector3(0.36f, 0.36f, 1f);

        SpriteRenderer glow = muzzle.GetComponent<SpriteRenderer>();
        if (glow == null)
        {
            glow = muzzle.AddComponent<SpriteRenderer>();
        }

        glow.sprite = GetCircleSprite();
        glow.color = glowColor;
        glow.sortingOrder = 24;
        glow.enabled = true;

        CreateOrUpdateChargeWarning(cannon.transform);

        EditorUtility.SetDirty(cannon);
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(muzzle);
        EditorUtility.SetDirty(glow);
    }

    private static void ApplyArcadeRobotScale()
    {
        GameObject maleRobot = GameObject.Find("Male0001");
        if (maleRobot == null)
        {
            return;
        }

        maleRobot.transform.position = new Vector3(0.1f, -0.92f, 0f);
        maleRobot.transform.localScale = new Vector3(0.7f, 0.435f, 1f);
        EditorUtility.SetDirty(maleRobot);
    }

    private static void CreateOrUpdateChargeWarning(Transform cannon)
    {
        Transform existing = cannon.Find("WarningGlow");
        if (existing == null)
        {
            existing = cannon.Find("ChargeWarningGlow");
        }

        GameObject warningObject = existing != null
            ? existing.gameObject
            : new GameObject("WarningGlow", typeof(SpriteRenderer));

        warningObject.name = "WarningGlow";
        warningObject.transform.SetParent(cannon, false);
        warningObject.transform.localPosition = new Vector3(0.78f, 0f, -0.05f);
        warningObject.transform.localRotation = Quaternion.identity;
        warningObject.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        warningObject.SetActive(true);

        SpriteRenderer warningRenderer = warningObject.GetComponent<SpriteRenderer>();
        if (warningRenderer == null)
        {
            warningRenderer = warningObject.AddComponent<SpriteRenderer>();
        }

        warningRenderer.sprite = GetCircleSprite();
        warningRenderer.color = new Color32(0, 255, 255, 90);
        warningRenderer.sortingOrder = 35;
        warningRenderer.enabled = false;

        CannonChargeWarning warning = warningObject.GetComponent<CannonChargeWarning>();
        if (warning == null)
        {
            warning = warningObject.AddComponent<CannonChargeWarning>();
        }

        warning.defaultDuration = 5f;
        warning.urgentFinalSeconds = 1f;
        warning.normalPulsesPerSecond = 1.35f;
        warning.urgentPulsesPerSecond = 2.7f;
        warning.restingScale = new Vector3(0.8f, 0.8f, 1f);
        warning.peakScale = new Vector3(2.0f, 2.0f, 1f);
        warning.safeNeonCyan = new Color(0f, 1f, 1f, 0.35f);
        warning.dangerNeonRed = new Color(1f, 0f, 0.06f, 0.95f);

        EditorUtility.SetDirty(warningObject);
        EditorUtility.SetDirty(warningRenderer);
        EditorUtility.SetDirty(warning);
    }

    private static LaserBehavior CreateOrReplaceLaserPrefab()
    {
        EnsureFolder(PrefabsFolder);

        GameObject prefabSource = new GameObject("LaserPrefab");
        prefabSource.transform.position = Vector3.zero;
        prefabSource.transform.localScale = Vector3.one;

        SpriteRenderer spriteRenderer = prefabSource.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetSquareSprite();
        spriteRenderer.color = new Color32(0, 255, 255, 180);
        spriteRenderer.sortingOrder = 30;

        Rigidbody2D rigidbody = prefabSource.AddComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.gravityScale = 0f;

        BoxCollider2D collider = prefabSource.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1f, 0.35f);

        TrailRenderer trail = prefabSource.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(1f, 0f)
        );
        trail.colorGradient = CreateLaserTrailGradient();
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.sortingOrder = 29;
        trail.material = CreateOrGetTrailMaterial();

        LaserBehavior laser = prefabSource.AddComponent<LaserBehavior>();
        laser.chargeDuration = 5f;
        laser.flashSpeed = 12f;
        laser.chargeLocalScale = new Vector3(0.35f, 0.35f, 1f);
        laser.launchedLocalScale = new Vector3(2.4f, 0.18f, 1f);
        laser.launchSpeed = 9f;
        laser.destroyDistance = 0.22f;
        laser.maxLaunchedLifetime = 4f;

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(prefabSource, LaserPrefabPath);
        UnityEngine.Object.DestroyImmediate(prefabSource);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return prefabAsset.GetComponent<LaserBehavior>();
    }

    private static Gradient CreateLaserTrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0f, 0.05f), 0f),
                new GradientColorKey(new Color(1f, 0f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        return gradient;
    }

    private static Material CreateOrGetTrailMaterial()
    {
        string materialPath = "Assets/EverMotionGenerated/M_LaserTrail.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (existing != null)
        {
            return existing;
        }

        EnsureFolder("Assets/EverMotionGenerated");
        Shader shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        material.name = "M_LaserTrail";
        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }

    private static GameObject CreateShield(Transform parent, string shieldName, Vector3 localPosition, Vector3 localScale)
    {
        Transform existing = parent.Find(shieldName);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        GameObject shield = new GameObject(shieldName, typeof(SpriteRenderer), typeof(PolygonCollider2D));
        shield.transform.SetParent(parent, false);
        shield.transform.localPosition = localPosition;
        shield.transform.localRotation = Quaternion.identity;
        shield.transform.localScale = localScale;
        shield.tag = "Shield";

        SpriteRenderer renderer = shield.GetComponent<SpriteRenderer>();
        renderer.sprite = GetHexShieldSprite();
        renderer.color = new Color32(0, 255, 255, 150);
        renderer.sortingOrder = 25;

        PolygonCollider2D collider = shield.GetComponent<PolygonCollider2D>();
        collider.isTrigger = true;
        collider.pathCount = 1;
        collider.SetPath(0, CreateHexColliderPath(0.52f));

        return shield;
    }

    private static Vector2[] CreateHexColliderPath(float radius)
    {
        Vector2[] points = new Vector2[6];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i + 30f);
            points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        return points;
    }

    private static GameObject CreateOrReplaceBlockVfxPrefab()
    {
        EnsureFolder(PrefabsFolder);

        string prefabPath = PrefabsFolder + "/ShieldBlockSparks.prefab";
        GameObject source = new GameObject("ShieldBlockSparks");
        ParticleSystem particles = source.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color32(0, 255, 255, 230), new Color32(255, 255, 255, 255));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0f, 1f, 1f), 0f),
                new GradientColorKey(new Color(1f, 1f, 1f), 0.55f),
                new GradientColorKey(new Color(0f, 0.65f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer particleRenderer = source.GetComponent<ParticleSystemRenderer>();
        particleRenderer.material = CreateOrGetTrailMaterial();
        particleRenderer.sortingOrder = 40;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
        UnityEngine.Object.DestroyImmediate(source);
        return prefab;
    }

    private static void StretchToParent(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(folderPath);
        string folder = System.IO.Path.GetFileName(folderPath);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void EnsureTag(string tagName)
    {
        UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManagerAssets == null || tagManagerAssets.Length == 0)
        {
            Debug.LogWarning("Could not open TagManager.asset to create tag: " + tagName);
            return;
        }

        SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");

        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == tagName)
            {
                return;
            }
        }

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tagName;
        tagManager.ApplyModifiedProperties();
    }

    private static T FindComponentByName<T>(string objectName) where T : Component
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.GetComponent<T>() : null;
    }

    private static Sprite GetUiSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static Sprite GetSquareSprite()
    {
        return GetGeneratedSprite("EverMotion_CannonBody.asset", false);
    }

    private static Sprite GetCircleSprite()
    {
        return GetGeneratedSprite("EverMotion_MuzzleGlow.asset", true);
    }

    private static Sprite GetCyberpunkCannonSprite()
    {
        string assetPath = "Assets/EverMotionGenerated/EverMotion_CyberpunkCannon_Short.asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        if (existing != null)
        {
            return existing;
        }

        EnsureFolder("Assets/EverMotionGenerated");

        const int width = 1024;
        const int height = 420;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 darkBody = new Color32(15, 28, 48, 255);
        Color32 darkerBody = new Color32(8, 15, 28, 255);
        Color32 backPlate = new Color32(20, 33, 55, 255);
        Color32 backPlateEdge = new Color32(55, 72, 104, 255);
        Color32 cyan = new Color32(0, 235, 255, 255);
        Color32 cyanSoft = new Color32(0, 235, 255, 120);
        Color32 black = new Color32(0, 0, 0, 255);

        ClearTexture(texture, clear);

        Vector2[] backFin =
        {
            new Vector2(42f, 48f),
            new Vector2(210f, 86f),
            new Vector2(210f, 334f),
            new Vector2(42f, 372f)
        };
        FillPolygon(texture, backFin, backPlate);
        DrawPolygon(texture, backFin, backPlateEdge, 7);

        FillRoundedRect(texture, 180, 102, 760, 318, 22, darkBody);
        DrawRoundedRect(texture, 180, 102, 760, 318, 22, cyan, 6);

        Vector2[] muzzle =
        {
            new Vector2(760f, 56f),
            new Vector2(944f, 122f),
            new Vector2(944f, 298f),
            new Vector2(760f, 364f)
        };
        FillPolygon(texture, muzzle, darkBody);
        DrawPolygon(texture, muzzle, cyan, 8);

        FillRoundedRect(texture, 235, 172, 640, 248, 36, black);
        FillRoundedRect(texture, 260, 188, 616, 232, 24, cyan, 2);

        FillRect(texture, 842, 156, 922, 264, darkerBody);
        FillCircle(texture, new Vector2(882f, 210f), 34f, cyan);
        DrawCircle(texture, new Vector2(882f, 210f), 52f, cyanSoft, 5);

        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 520f);
        sprite.name = "EverMotion_CyberpunkCannon_Short";
        AssetDatabase.CreateAsset(sprite, assetPath);
        AssetDatabase.SaveAssets();
        return sprite;
    }

    private static Sprite GetHexShieldSprite()
    {
        string assetPath = "Assets/EverMotionGenerated/EverMotion_HexShield.asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        if (existing != null)
        {
            return existing;
        }

        EnsureFolder("Assets/EverMotionGenerated");

        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 faintFill = new Color32(255, 255, 255, 20);
        Color32 line = new Color32(255, 255, 255, 230);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Vector2[] outerHex = CreatePixelHex(center, 106f);
        FillPolygon(texture, outerHex, faintFill);
        DrawPolygon(texture, outerHex, line, 3);

        float cellRadius = 22f;
        float horizontalStep = Mathf.Sqrt(3f) * cellRadius;
        float verticalStep = 1.5f * cellRadius;

        for (float y = 34f; y < size - 28f; y += verticalStep)
        {
            int row = Mathf.RoundToInt((y - 34f) / verticalStep);
            float rowOffset = row % 2 == 0 ? 0f : horizontalStep * 0.5f;

            for (float x = 28f + rowOffset; x < size - 28f; x += horizontalStep)
            {
                Vector2 cellCenter = new Vector2(x, y);
                if (PointInPolygon(cellCenter, outerHex))
                {
                    DrawPolygon(texture, CreatePixelHex(cellCenter, cellRadius), line, 1);
                }
            }
        }

        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 256f);
        sprite.name = "EverMotion_HexShield";
        AssetDatabase.CreateAsset(sprite, assetPath);
        AssetDatabase.SaveAssets();
        return sprite;
    }

    private static Vector2[] CreatePixelHex(Vector2 center, float radius)
    {
        Vector2[] points = new Vector2[6];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i + 30f);
            points[i] = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        return points;
    }

    private static void DrawPolygon(Texture2D texture, Vector2[] points, Color32 color, int thickness)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Length];
            DrawLine(texture, start, end, color, thickness);
        }
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

    private static void ClearTexture(Texture2D texture, Color32 color)
    {
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }

    private static void FillRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color32 color)
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

    private static void FillRoundedRect(Texture2D texture, int minX, int minY, int maxX, int maxY, int radius, Color32 color, int inset = 0)
    {
        minX += inset;
        minY += inset;
        maxX -= inset;
        maxY -= inset;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (PointInRoundedRect(x, y, minX, minY, maxX, maxY, radius))
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private static void DrawRoundedRect(Texture2D texture, int minX, int minY, int maxX, int maxY, int radius, Color32 color, int thickness)
    {
        int innerMinX = minX + thickness;
        int innerMinY = minY + thickness;
        int innerMaxX = maxX - thickness;
        int innerMaxY = maxY - thickness;
        int innerRadius = Mathf.Max(0, radius - thickness);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                bool insideOuter = PointInRoundedRect(x, y, minX, minY, maxX, maxY, radius);
                bool insideInner = PointInRoundedRect(x, y, innerMinX, innerMinY, innerMaxX, innerMaxY, innerRadius);

                if (insideOuter && !insideInner)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private static bool PointInRoundedRect(int x, int y, int minX, int minY, int maxX, int maxY, int radius)
    {
        int innerMinX = minX + radius;
        int innerMaxX = maxX - radius;
        int innerMinY = minY + radius;
        int innerMaxY = maxY - radius;

        if ((x >= innerMinX && x <= innerMaxX && y >= minY && y <= maxY)
            || (y >= innerMinY && y <= innerMaxY && x >= minX && x <= maxX))
        {
            return true;
        }

        Vector2 topLeft = new Vector2(innerMinX, innerMaxY);
        Vector2 topRight = new Vector2(innerMaxX, innerMaxY);
        Vector2 bottomLeft = new Vector2(innerMinX, innerMinY);
        Vector2 bottomRight = new Vector2(innerMaxX, innerMinY);
        Vector2 point = new Vector2(x, y);

        return Vector2.Distance(point, topLeft) <= radius
            || Vector2.Distance(point, topRight) <= radius
            || Vector2.Distance(point, bottomLeft) <= radius
            || Vector2.Distance(point, bottomRight) <= radius;
    }

    private static void FillCircle(Texture2D texture, Vector2 center, float radius, Color32 color)
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

    private static void DrawCircle(Texture2D texture, Vector2 center, float radius, Color32 color, int thickness)
    {
        int minX = Mathf.FloorToInt(center.x - radius - thickness);
        int maxX = Mathf.CeilToInt(center.x + radius + thickness);
        int minY = Mathf.FloorToInt(center.y - radius - thickness);
        int maxY = Mathf.CeilToInt(center.y + radius + thickness);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (x >= 0 && x < texture.width && y >= 0 && y < texture.height
                    && distance >= radius - thickness && distance <= radius + thickness)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private static void FillPolygon(Texture2D texture, Vector2[] polygon, Color32 color)
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

    private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
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

    private static Sprite GetGeneratedSprite(string fileName, bool circle)
    {
        const int size = 64;
        string folderPath = "Assets/EverMotionGenerated";
        string assetPath = folderPath + "/" + fileName;
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        if (existing != null)
        {
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "EverMotionGenerated");
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 solid = new Color32(255, 255, 255, 255);
        float radius = size * 0.42f;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool visible = !circle || Vector2.Distance(new Vector2(x, y), center) <= radius;
                texture.SetPixel(x, y, visible ? solid : clear);
            }
        }

        texture.Apply();

        Sprite generated = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        generated.name = fileName.Replace(".asset", string.Empty);
        AssetDatabase.CreateAsset(generated, assetPath);
        AssetDatabase.SaveAssets();

        return generated;
    }

    private static void DeleteIfExists(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }
    }
}
