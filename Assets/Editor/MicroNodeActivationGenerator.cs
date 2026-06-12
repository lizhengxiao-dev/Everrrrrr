using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MicroNodeActivationGenerator
{
    private const string SourceScenePath = "Assets/Shared/Try/Pulse Defense Matrix.unity";
    private const string TargetScenePath = "Assets/Shared/Try/MicroNodeActivation.unity";
    private const string GeneratedFolder = "Assets/MicroNodeActivation";
    private const string PinchSpritesFolder = GeneratedFolder + "/PinchSprites";
    private const string BackgroundPath = "Assets/MedicalRoom_Background.png";

    [MenuItem("EverMotion/Micro Node Activation - Build Scene")]
    public static void BuildScene()
    {
        EnsureFolder(GeneratedFolder);
        EnsureFolder(PinchSpritesFolder);
        EnsureFolder(PinchSpritesFolder + "/Male");
        EnsureFolder(PinchSpritesFolder + "/Female");

        List<Sprite> malePinchSprites = GetSprites(PinchSpritesFolder + "/Male");
        List<Sprite> femalePinchSprites = GetSprites(PinchSpritesFolder + "/Female");
        AnimatorController maleController = BuildRobotController("Male", malePinchSprites);
        AnimatorController femaleController = BuildRobotController("Female", femalePinchSprites);

        Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(sourceScene, TargetScenePath, true);
        Scene targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        DisableOldGameplay();
        ConfigureMedicalBackground();
        ConfigureRobots(maleController, femaleController, malePinchSprites, femalePinchSprites);
        ConfigureHandInput();
        ConfigureHudAndManager();
        EnsureSceneInBuildSettings();

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Micro Node Activation scene built at " + TargetScenePath + ".");
    }

    private static void EnsureSceneInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(scene => scene.path != TargetScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(TargetScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static AnimatorController BuildRobotController(string gender, List<Sprite> pinchSprites)
    {
        string idleClipPath = "Assets/NewRobotGame/" + gender + "_Idle.anim";
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(idleClipPath);
        if (idleClip == null)
        {
            throw new InvalidOperationException("Missing idle animation: " + idleClipPath);
        }

        string precisionClipPath = GeneratedFolder + "/" + gender + "_PrecisionActivate.anim";
        List<Sprite> fallbackSprites = GetSprites("Assets/NewRobotGame/" + gender + "/Push");
        AnimationClip precisionClip = CreatePrecisionClip(
            precisionClipPath,
            gender + "_PrecisionActivate",
            pinchSprites.Count > 0 ? pinchSprites : fallbackSprites
        );

        string controllerPath = GeneratedFolder + "/" + gender + "_MicroNode.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        AnimatorState idle = stateMachine.AddState(gender + "_Idle", new Vector3(160f, 60f, 0f));
        idle.motion = idleClip;
        idle.writeDefaultValues = true;

        AnimatorState precision = stateMachine.AddState(gender + "_PrecisionActivate", new Vector3(390f, 60f, 0f));
        precision.motion = precisionClip;
        precision.writeDefaultValues = true;

        stateMachine.defaultState = idle;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimationClip CreatePrecisionClip(string path, string clipName, List<Sprite> sourceSprites)
    {
        if (sourceSprites.Count == 0)
        {
            throw new InvalidOperationException("No Push sprites found for " + clipName + ".");
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            clip.ClearCurves();
        }

        clip.name = clipName;
        clip.frameRate = 30f;

        int frameCount = sourceSprites.Count;
        ObjectReferenceKeyframe[] frames = new ObjectReferenceKeyframe[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            float normalizedTime = frameCount <= 1 ? 0f : i / (float)(frameCount - 1);
            int sourceIndex = Mathf.RoundToInt(normalizedTime * (sourceSprites.Count - 1));
            frames[i] = new ObjectReferenceKeyframe
            {
                time = i / clip.frameRate,
                value = sourceSprites[sourceIndex]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(
            clip,
            new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            },
            frames
        );

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void DisableOldGameplay()
    {
        if (SceneManager.GetActiveScene().path != TargetScenePath)
        {
            throw new InvalidOperationException(
                "Refusing to remove legacy gameplay outside " + TargetScenePath + "."
            );
        }

        GameObject gameManagerObject = FindSceneObject("GameManager");
        if (gameManagerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(gameManagerObject);
        }

        DestroySceneObject("Cannon_Left");
        DestroySceneObject("Cannon_Right");
        DestroySceneObject("Cannon_Top");
        DestroySceneObject("RuntimePushShieldLine");
        DestroySceneObject("PlayerHand");

        LaserBehavior[] lasers = UnityEngine.Object.FindObjectsByType<LaserBehavior>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (LaserBehavior laser in lasers)
        {
            if (laser.gameObject.scene.IsValid())
            {
                UnityEngine.Object.DestroyImmediate(laser.gameObject);
            }
        }
    }

    private static void ConfigureMedicalBackground()
    {
        Sprite backgroundSprite = LoadSprite(BackgroundPath);
        if (backgroundSprite == null)
        {
            throw new InvalidOperationException("Missing background sprite: " + BackgroundPath);
        }

        GameObject background = FindSceneObject("CyberBackground_WORLD");
        if (background == null)
        {
            background = new GameObject("CyberBackground_WORLD", typeof(SpriteRenderer));
        }

        SpriteRenderer renderer = background.GetComponent<SpriteRenderer>();
        renderer.sprite = backgroundSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = -500;

        Camera camera = Camera.main;
        if (camera != null && camera.orthographic)
        {
            float cameraHeight = camera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * camera.aspect;
            float scale = Mathf.Max(
                cameraWidth / backgroundSprite.bounds.size.x,
                cameraHeight / backgroundSprite.bounds.size.y
            );

            background.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 9f);
            background.transform.localScale = Vector3.one * scale;
            camera.backgroundColor = new Color32(232, 244, 255, 255);
        }

        EditorUtility.SetDirty(background);
    }

    private static void ConfigureRobots(
        AnimatorController maleController,
        AnimatorController femaleController,
        List<Sprite> malePinchSprites,
        List<Sprite> femalePinchSprites)
    {
        ConfigureRobot(
            FindSceneObject("Male0001"),
            maleController,
            "Male_Idle",
            "Male_PrecisionActivate",
            new Vector3(1.55f, 0.7f, -0.08f),
            malePinchSprites
        );

        ConfigureRobot(
            FindSceneObject("Female0001"),
            femaleController,
            "Female_Idle",
            "Female_PrecisionActivate",
            new Vector3(1.35f, 0.72f, -0.08f),
            femalePinchSprites
        );

        GameObject male = FindSceneObject("Male0001");
        GameObject female = FindSceneObject("Female0001");
        if (male != null)
        {
            male.SetActive(true);
        }

        if (female != null)
        {
            female.SetActive(false);
        }
    }

    private static void ConfigureRobot(
        GameObject robot,
        RuntimeAnimatorController controller,
        string idleState,
        string precisionState,
        Vector3 fingertipPosition,
        List<Sprite> gestureSprites)
    {
        if (robot == null)
        {
            return;
        }

        Animator animator = robot.GetComponent<Animator>();
        bool usesGestureSprites = gestureSprites != null && gestureSprites.Count > 0;
        if (animator != null)
        {
            animator.runtimeAnimatorController = controller;
            animator.speed = 1f;
            animator.enabled = !usesGestureSprites;
            EditorUtility.SetDirty(animator);
        }

        SpriteRenderer spriteRenderer = robot.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && usesGestureSprites)
        {
            spriteRenderer.sprite = gestureSprites[0];
            EditorUtility.SetDirty(spriteRenderer);
        }

        DestroyComponent<RobotAnimationController>(robot);
        DestroyComponent<ShieldManager>(robot);
        DestroyComponent<PushShieldVfx>(robot);
        DestroyChild(robot.transform, "Shield_Left");
        DestroyChild(robot.transform, "Shield_Right");
        DestroyChild(robot.transform, "Shield_Top");
        DestroyChild(robot.transform, "PushShield_VFX");
        DestroyChild(robot.transform, "PoweredUpShield_VFX");
        DestroyChild(robot.transform, "PoweredUpShield_Sprite");

        MicroNodeRobotFeedback feedback = robot.GetComponent<MicroNodeRobotFeedback>();
        if (feedback == null)
        {
            feedback = robot.AddComponent<MicroNodeRobotFeedback>();
        }

        feedback.idleStateName = idleState;
        feedback.precisionStateName = precisionState;
        feedback.useGestureSynchronizedSprites = usesGestureSprites;
        feedback.gestureSprites = gestureSprites != null ? gestureSprites.ToArray() : Array.Empty<Sprite>();
        feedback.fingertipGlowLocalPosition = fingertipPosition;
        feedback.sortingOrder = 165;
        EditorUtility.SetDirty(robot);
    }

    private static void ConfigureHandInput()
    {
        GameObject rehabTarget = MediaPipePrefabSceneUtility.EnsureMediaPipeObject("RehabTarget");

        RehabTarget oldTarget = rehabTarget.GetComponent<RehabTarget>();
        if (oldTarget != null)
        {
            oldTarget.enabled = false;
        }

        HandTracker tracker = rehabTarget.GetComponent<HandTracker>();
        if (tracker == null)
        {
            tracker = rehabTarget.AddComponent<HandTracker>();
        }

        tracker.enabled = true;
        tracker.listenPort = 5052;
        tracker.worldMinX = -7.1f;
        tracker.worldMaxX = 7.1f;
        tracker.worldMinY = -2.8f;
        tracker.worldMaxY = 3.7f;
        tracker.mirrorX = false;
        tracker.invertY = false;
        tracker.smoothSpeed = 20f;
        tracker.fixedZ = 0f;

        SpriteRenderer oldRenderer = rehabTarget.GetComponent<SpriteRenderer>();
        if (oldRenderer != null)
        {
            oldRenderer.enabled = false;
        }

        EditorUtility.SetDirty(rehabTarget);
    }

    private static void ConfigureHudAndManager()
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            throw new InvalidOperationException("Micro Node scene needs the existing EverMotion Canvas.");
        }

        GameObject clearPanel = EnsureEndPanel(canvas.transform, "Panel_GameClear", "PRECISION RESTORED", new Color32(0, 255, 255, 205));
        DestroySceneObject("Panel_GameOver");
        Image[] roundIndicators = EnsurePrecisionHeader(canvas.transform);
        GameObject storyPanel = EnsureStoryPanel(canvas.transform);
        CanvasGroup roundTransition = EnsureRoundTransition(canvas.transform);
        clearPanel.SetActive(false);

        GameObject managerObject = FindSceneObject("MicroNodeGameManager");
        if (managerObject == null)
        {
            managerObject = new GameObject("MicroNodeGameManager");
        }

        MicroNodeActivationManager manager = managerObject.GetComponent<MicroNodeActivationManager>();
        if (manager == null)
        {
            manager = managerObject.AddComponent<MicroNodeActivationManager>();
        }

        manager.roundCount = 3;
        manager.roundDuration = 60f;
        manager.requiredCaptures = 18;
        manager.baseNodeLifetime = 18f;
        manager.disappearSpeedMultiplier = 1.5f;
        manager.activeNodeCount = 3;
        manager.easyActiveNodes = 2;
        manager.mediumActiveNodes = 3;
        manager.easyCaptureRadius = 1.25f;
        manager.mediumCaptureRadius = 1.05f;
        manager.easyNodeScale = 1.55f;
        manager.mediumNodeScale = 1.35f;
        manager.minimumNodeSpacing = 1.25f;
        manager.handTracker = FindSceneComponent<HandTracker>("RehabTarget");
        manager.handCursor = manager.handTracker != null ? manager.handTracker.transform : null;
        manager.allowMouseFallback = false;
        manager.waitForMediaPipeBeforeStarting = true;
        manager.timerText = FindSceneComponent<Text>("Text_Timer");
        manager.counterText = FindSceneComponent<Text>("Text_TargetCounter");
        manager.energyBar = FindSceneComponent<Slider>("EnergyBar");
        manager.energyBarFill = manager.energyBar != null && manager.energyBar.fillRect != null
            ? manager.energyBar.fillRect.GetComponent<Image>()
            : null;
        manager.systemStatusText = FindSceneComponent<Text>("Text_PrecisionStatus");
        manager.roundText = FindSceneComponent<Text>("Text_Round");
        manager.roundIndicators = roundIndicators;
        manager.storyPanel = storyPanel;
        manager.roundTransitionGroup = roundTransition;
        manager.roundTransitionText = FindSceneComponent<Text>("Text_RoundTransition");
        manager.gameClearPanel = clearPanel;
        manager.gameOverPanel = null;
        manager.maleRobot = FindSceneObject("Male0001");
        manager.femaleRobot = FindSceneObject("Female0001");

        MicroNodeChargingStation chargingStation = managerObject.GetComponent<MicroNodeChargingStation>();
        if (chargingStation == null)
        {
            chargingStation = managerObject.AddComponent<MicroNodeChargingStation>();
        }
        chargingStation.robotTarget = manager.maleRobot != null ? manager.maleRobot.transform : null;
        manager.chargingStation = chargingStation;

        Text energyLabel = FindSceneComponent<Text>("Text_EnergyLabel");
        if (energyLabel != null)
        {
            energyLabel.text = "ROBOT CHARGE";
        }

        EditorUtility.SetDirty(manager);
    }

    private static Image[] EnsurePrecisionHeader(Transform canvas)
    {
        GameObject header = FindSceneObject("MicroNode_PrecisionHeader");
        if (header == null)
        {
            header = new GameObject(
                "MicroNode_PrecisionHeader",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline)
            );
            header.transform.SetParent(canvas, false);
        }

        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 1f);
        headerRect.anchorMax = new Vector2(0.5f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -24f);
        headerRect.sizeDelta = new Vector2(920f, 76f);

        Image headerImage = header.GetComponent<Image>();
        headerImage.color = new Color32(5, 21, 34, 218);
        headerImage.raycastTarget = false;

        Outline headerOutline = header.GetComponent<Outline>();
        headerOutline.effectColor = new Color32(0, 220, 255, 150);
        headerOutline.effectDistance = new Vector2(2f, -2f);

        EnsureHeaderText(
            header.transform,
            "Text_PrecisionTitle",
            "PRECISION SYSTEM",
            new Vector2(-300f, 11f),
            new Vector2(270f, 34f),
            25,
            TextAnchor.MiddleLeft,
            new Color32(225, 249, 255, 255)
        );

        EnsureHeaderText(
            header.transform,
            "Text_Round",
            "ROUND 1 / 3",
            new Vector2(0f, 11f),
            new Vector2(230f, 34f),
            23,
            TextAnchor.MiddleCenter,
            new Color32(255, 255, 255, 255)
        );

        EnsureHeaderText(
            header.transform,
            "Text_PrecisionStatus",
            "AWAITING HAND LINK",
            new Vector2(295f, 11f),
            new Vector2(280f, 34f),
            19,
            TextAnchor.MiddleRight,
            new Color32(255, 200, 64, 255)
        );

        Image[] indicators = new Image[3];
        for (int i = 0; i < indicators.Length; i++)
        {
            string name = "RoundIndicator_" + (i + 1);
            Transform existing = header.transform.Find(name);
            GameObject indicatorObject = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(Image));

            indicatorObject.transform.SetParent(header.transform, false);
            RectTransform indicatorRect = indicatorObject.GetComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
            indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
            indicatorRect.pivot = new Vector2(0.5f, 0.5f);
            indicatorRect.anchoredPosition = new Vector2((i - 1) * 62f, -22f);
            indicatorRect.sizeDelta = new Vector2(48f, 6f);

            indicators[i] = indicatorObject.GetComponent<Image>();
            indicators[i].raycastTarget = false;
            indicators[i].color = i == 0
                ? new Color32(0, 242, 255, 255)
                : new Color32(42, 92, 108, 120);
        }

        return indicators;
    }

    private static GameObject EnsureStoryPanel(Transform canvas)
    {
        GameObject panel = FindSceneObject("MicroNode_StoryPanel");
        if (panel == null)
        {
            panel = new GameObject(
                "MicroNode_StoryPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline),
                typeof(CanvasGroup),
                typeof(MicroNodeIntroAnimator)
            );
            panel.transform.SetParent(canvas, false);
        }
        if (panel.GetComponent<CanvasGroup>() == null)
        {
            panel.AddComponent<CanvasGroup>();
        }
        MicroNodeIntroAnimator animator = panel.GetComponent<MicroNodeIntroAnimator>();
        if (animator == null)
        {
            animator = panel.AddComponent<MicroNodeIntroAnimator>();
        }

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 100f);
        panelRect.sizeDelta = new Vector2(960f, 210f);

        Image image = panel.GetComponent<Image>();
        image.color = new Color32(5, 20, 34, 232);
        image.raycastTarget = false;

        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color32(255, 65, 105, 190);
        outline.effectDistance = new Vector2(3f, -3f);

        Text title = EnsureHeaderText(
            panel.transform,
            "Text_StoryTitle",
            "PRECISION SYSTEM // LAST POWER CELLS",
            new Vector2(0f, 50f),
            new Vector2(880f, 52f),
            34,
            TextAnchor.MiddleCenter,
            new Color32(255, 96, 125, 255)
        );

        Text body = EnsureHeaderText(
            panel.transform,
            "Text_StoryBody",
            "TINY NODES ARE FADING // PINCH TO RESTORE FINGER CONTROL",
            new Vector2(0f, -31f),
            new Vector2(860f, 64f),
            22,
            TextAnchor.MiddleCenter,
            new Color32(98, 238, 255, 255)
        );

        animator.titleText = title;
        animator.bodyText = body;
        EditorUtility.SetDirty(animator);

        return panel;
    }

    private static CanvasGroup EnsureRoundTransition(Transform canvas)
    {
        GameObject panel = FindSceneObject("MicroNode_RoundTransition");
        if (panel == null)
        {
            panel = new GameObject(
                "MicroNode_RoundTransition",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline),
                typeof(CanvasGroup)
            );
            panel.transform.SetParent(canvas, false);
        }

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 85f);
        panelRect.sizeDelta = new Vector2(620f, 126f);

        Image image = panel.GetComponent<Image>();
        image.color = new Color32(4, 24, 38, 235);
        image.raycastTarget = false;

        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color32(0, 238, 255, 185);
        outline.effectDistance = new Vector2(3f, -3f);

        EnsureHeaderText(
            panel.transform,
            "Text_RoundTransition",
            "ROUND 1 // EASY\nNODE WINDOW  13.5S",
            Vector2.zero,
            new Vector2(580f, 106f),
            29,
            TextAnchor.MiddleCenter,
            new Color32(225, 252, 255, 255)
        );

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        panel.SetActive(false);
        return group;
    }

    private static Text EnsureHeaderText(
        Transform parent,
        string name,
        string value,
        Vector2 position,
        Vector2 size,
        int fontSize,
        TextAnchor alignment,
        Color color)
    {
        Transform existing = parent.Find(name);
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(Text));

        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject EnsureEndPanel(Transform canvas, string name, string title, Color32 outlineColor)
    {
        GameObject panel = FindSceneObject(name);
        if (panel == null)
        {
            panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            panel.transform.SetParent(canvas, false);
        }

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 70f);
        panelRect.sizeDelta = new Vector2(920f, 280f);

        Image image = panel.GetComponent<Image>();
        image.color = new Color32(5, 18, 30, 225);

        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(3f, -3f);

        EnsurePanelText(panel.transform, name + "_Title", title, new Vector2(0f, 55f), 44);
        EnsurePanelText(panel.transform, name + "_Subtitle", "Press R to restart", new Vector2(0f, -48f), 22);
        return panel;
    }

    private static void EnsurePanelText(Transform panel, string name, string value, Vector2 position, int fontSize)
    {
        Transform existing = panel.Find(name);
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(Text));

        textObject.transform.SetParent(panel, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(860f, 110f);

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }

    private static List<Sprite> GetSprites(string folder)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        return guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path =>
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            )
            .OrderBy(GetTrailingNumber)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                EnsureSprite(path);
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            })
            .Where(sprite => sprite != null)
            .ToList();
    }

    private static int GetTrailingNumber(string path)
    {
        Match match = Regex.Match(Path.GetFileNameWithoutExtension(path), @"(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
    }

    private static Sprite LoadSprite(string path)
    {
        EnsureSprite(path);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    private static void EnsureSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || importer.mipmapEnabled;

        if (!changed)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private static void SetComponentEnabled<T>(GameObject target, bool enabled) where T : Behaviour
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            component.enabled = enabled;
            EditorUtility.SetDirty(component);
        }
    }

    private static void SetObjectActive(string name, bool active)
    {
        GameObject target = FindSceneObject(name);
        if (target != null)
        {
            target.SetActive(active);
            EditorUtility.SetDirty(target);
        }
    }

    private static void DestroySceneObject(string name)
    {
        GameObject target = FindSceneObject(name);
        if (target != null)
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void DestroyChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void DestroyComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
    }

    private static T FindSceneComponent<T>(string name) where T : Component
    {
        GameObject target = FindSceneObject(name);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static GameObject FindSceneObject(string name)
    {
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (GameObject sceneObject in objects)
        {
            if (sceneObject.name == name && sceneObject.scene.IsValid())
            {
                return sceneObject;
            }
        }

        return null;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
        string name = Path.GetFileName(folder);

        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
