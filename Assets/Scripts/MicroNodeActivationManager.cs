using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(100)]
public class MicroNodeActivationManager : MonoBehaviour
{
    [Header("Session")]
    public int roundCount = 3;
    public float roundDuration = 60f;
    public int requiredCaptures = 18;
    public float baseNodeLifetime = 18f;
    public float disappearSpeedMultiplier = 1.5f;
    public int activeNodeCount = 3;

    [Header("Play Field")]
    public Vector2 minWorld = new Vector2(-7.1f, -2.8f);
    public Vector2 maxWorld = new Vector2(7.1f, 3.7f);
    public float minimumNodeSpacing = 1.25f;
    public float captureRadius = 0.9f;

    [Header("Difficulty")]
    public int easyActiveNodes = 2;
    public int mediumActiveNodes = 3;
    public float easyCaptureRadius = 1.25f;
    public float mediumCaptureRadius = 1.05f;
    public float easyNodeScale = 1.55f;
    public float mediumNodeScale = 1.35f;

    [Header("Input")]
    public HandTracker handTracker;
    public Transform handCursor;
    public bool allowMouseFallback = false;
    public bool waitForMediaPipeBeforeStarting = true;

    [Header("HUD")]
    public Text timerText;
    public Text counterText;
    public Slider energyBar;
    public Image energyBarFill;
    public Text systemStatusText;
    public Text roundText;
    public Image[] roundIndicators;
    public GameObject storyPanel;
    public CanvasGroup roundTransitionGroup;
    public Text roundTransitionText;
    public GameObject gameClearPanel;
    public GameObject gameOverPanel;

    [Header("Robots")]
    public GameObject maleRobot;
    public GameObject femaleRobot;
    public MicroNodeChargingStation chargingStation;

    private readonly List<MicroNodeTarget> activeNodes = new List<MicroNodeTarget>();
    private Transform nodeRoot;
    private SpriteRenderer cursorRenderer;
    private float elapsedSessionTime;
    private int currentRound = 1;
    private int capturedCount;
    private int missedCount;
    private bool previousPinch;
    private bool sessionEnded;
    private bool sessionStarted;
    private bool sessionStarting;
    private bool roundTransitionActive;
    private static Material circuitLineMaterial;
    private static Sprite squareSprite;

    public int CurrentRound => currentRound;
    public int CapturedCount => capturedCount;

    private void Awake()
    {
        DisablePreviousMinigameSystems();
        AutoBindReferences();
        if (handTracker != null)
        {
            handTracker.mirrorX = false;
            handTracker.invertY = false;
        }
        EnsureRuntimeObjects();
    }

    private void Start()
    {
        elapsedSessionTime = 0f;
        currentRound = 1;
        capturedCount = 0;
        missedCount = 0;
        sessionEnded = false;
        sessionStarted = !waitForMediaPipeBeforeStarting;
        sessionStarting = false;
        roundTransitionActive = false;

        EverMotionUIPolisher.ApplyPolish();
        EverMotionCountdownOverlay.PolishStoryPanel(
            storyPanel,
            "MICRO NODE ACTIVATION // PRECISION SYSTEM OFFLINE",
            "Tiny power cells are flickering across the circuit field. Move your hand to a node and pinch to send power back into the robot fingertips.",
            "Mission cue: pinch one calm target at a time. Each minute gently increases the challenge.",
            new Color(0.25f, 1f, 0.85f, 1f)
        );
        SetPanelVisible(storyPanel, !sessionStarted);
        SetPanelVisible(gameClearPanel, false);
        SetPanelVisible(gameOverPanel, false);
        SetCanvasGroupVisible(roundTransitionGroup, false);
        if (sessionStarted)
        {
            FillActiveNodes();
        }
        UpdateHud();
    }

    private void Update()
    {
        if (WasFemalePressed())
        {
            SetActiveRobot(true);
        }
        else if (WasMalePressed())
        {
            SetActiveRobot(false);
        }

        bool usingMediaPipe = UpdateCursorAndPinch(out bool isPinching);

        if (!sessionStarted && !sessionStarting && (usingMediaPipe || allowMouseFallback))
        {
            StartCoroutine(BeginSessionRoutine());
        }

        if (sessionEnded)
        {
            if (WasRestartPressed())
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }

            previousPinch = isPinching;
            return;
        }

        if (!sessionStarted || roundTransitionActive)
        {
            previousPinch = false;
            UpdateHud();
            return;
        }

        elapsedSessionTime += Time.deltaTime;
        int requestedRound = Mathf.Min(roundCount, Mathf.FloorToInt(elapsedSessionTime / roundDuration) + 1);

        if (requestedRound != currentRound)
        {
            StartCoroutine(RoundTransitionRoutine(requestedRound));
            previousPinch = isPinching;
            UpdateHud();
            return;
        }

        if (elapsedSessionTime >= roundCount * roundDuration)
        {
            EndSession();
            previousPinch = isPinching;
            return;
        }

        if (isPinching && !previousPinch)
        {
            TryCaptureNearestNode();
        }

        previousPinch = isPinching;
        UpdateHud();
    }

    public void NotifyNodeCaptured(MicroNodeTarget node)
    {
        activeNodes.Remove(node);
        capturedCount++;
        EverMotionFeedbackToast.Show(this, "NODE RESTORED  +" + capturedCount.ToString("00"), GetRoundColor(currentRound));

        MicroNodeRobotFeedback feedback = GetActiveRobotFeedback();
        if (feedback != null)
        {
            feedback.PlayActivation(node.transform.position);
        }
        if (chargingStation != null)
        {
            chargingStation.PulseFromNode(node.transform.position);
        }

        RefillOneNode();
        UpdateHud();
    }

    public void NotifyNodeExpired(MicroNodeTarget node)
    {
        activeNodes.Remove(node);
        missedCount++;
        RefillOneNode();
    }

    private void SpawnNode()
    {
        float speedFactor = Mathf.Pow(disappearSpeedMultiplier, currentRound - 1);
        float lifetime = baseNodeLifetime / speedFactor;
        Vector2 spawnPosition = FindSpawnPosition();

        GameObject nodeObject = new GameObject("MicroNode_R" + currentRound);
        nodeObject.transform.SetParent(nodeRoot, true);
        nodeObject.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, -0.2f);

        MicroNodeTarget node = nodeObject.AddComponent<MicroNodeTarget>();
        node.Initialize(
            this,
            Vector2.zero,
            lifetime,
            GetRoundColor(currentRound),
            GetRoundNodeScale(),
            120
        );

        activeNodes.Add(node);
    }

    private void FillActiveNodes()
    {
        while (!sessionEnded && activeNodes.Count < GetRoundActiveNodeCount())
        {
            SpawnNode();
        }
    }

    private void RefillOneNode()
    {
        if (sessionStarted && !sessionEnded && activeNodes.Count < GetRoundActiveNodeCount())
        {
            SpawnNode();
        }
    }

    private Vector2 FindSpawnPosition()
    {
        Vector2 candidate = Vector2.zero;

        for (int attempt = 0; attempt < 24; attempt++)
        {
            candidate = new Vector2(
                Random.Range(minWorld.x, maxWorld.x),
                Random.Range(minWorld.y, maxWorld.y)
            );

            bool hasEnoughSpace = true;
            foreach (MicroNodeTarget node in activeNodes)
            {
                if (node != null && Vector2.Distance(candidate, node.transform.position) < minimumNodeSpacing)
                {
                    hasEnoughSpace = false;
                    break;
                }
            }

            if (hasEnoughSpace)
            {
                return candidate;
            }
        }

        return candidate;
    }

    private void TryCaptureNearestNode()
    {
        if (handCursor == null)
        {
            return;
        }

        MicroNodeTarget nearest = null;
        float nearestDistance = GetRoundCaptureRadius();

        for (int i = activeNodes.Count - 1; i >= 0; i--)
        {
            MicroNodeTarget node = activeNodes[i];
            if (node == null)
            {
                activeNodes.RemoveAt(i);
                continue;
            }

            float distance = Vector2.Distance(handCursor.position, node.transform.position);
            if (distance <= nearestDistance)
            {
                nearest = node;
                nearestDistance = distance;
            }
        }

        if (nearest != null)
        {
            nearest.Capture();
        }
    }

    private bool UpdateCursorAndPinch(out bool isPinching)
    {
        bool usingMediaPipe = handTracker != null && handTracker.HasRecentMessage();

        if (usingMediaPipe)
        {
            isPinching = handTracker.IsPinching();
        }
        else
        {
            isPinching = allowMouseFallback && IsMousePressed();

            if (allowMouseFallback && handCursor != null && Camera.main != null)
            {
                Vector2 screenPosition = GetMousePosition();
                Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
                handCursor.position = new Vector3(world.x, world.y, 0f);
            }
        }

        if (cursorRenderer != null)
        {
            cursorRenderer.color = !usingMediaPipe && !allowMouseFallback
                ? new Color(0.35f, 0.55f, 0.6f, 0.3f)
                : isPinching
                    ? new Color(1f, 0.25f, 0.8f, 0.95f)
                    : new Color(0f, 1f, 1f, 0.85f);
            cursorRenderer.transform.localScale = isPinching ? Vector3.one * 0.7f : Vector3.one;
        }

        return usingMediaPipe;
    }

    private void EndSession()
    {
        sessionEnded = true;
        ClearActiveNodes();

        SetPanelVisible(gameClearPanel, true);
        SetPanelVisible(gameOverPanel, false);
        SetEndPanelText(
            gameClearPanel,
            "PRECISION RESTORED",
            "Thank you... the fingertip circuits are lighting up again. I can feel each small movement returning.\n"
                + "Energy nodes restored: " + capturedCount
        );

        UpdateHud();
    }

    private IEnumerator BeginSessionRoutine()
    {
        sessionStarting = true;
        yield return new WaitForSecondsRealtime(1.25f);

        SetPanelVisible(storyPanel, false);
        yield return ShowRoundTransition(1);
        yield return EverMotionCountdownOverlay.Play(
            "MICRO NODE ACTIVATION",
            "Move to a node, then pinch to capture.",
            new Color(0.25f, 1f, 0.85f, 1f)
        );

        sessionStarted = true;
        sessionStarting = false;
        FillActiveNodes();
        UpdateHud();
    }

    private IEnumerator RoundTransitionRoutine(int nextRound)
    {
        roundTransitionActive = true;
        ClearActiveNodes();
        currentRound = nextRound;
        UpdateHud();

        yield return ShowRoundTransition(nextRound);

        FillActiveNodes();
        roundTransitionActive = false;
    }

    private IEnumerator ShowRoundTransition(int round)
    {
        if (roundTransitionGroup == null)
        {
            yield break;
        }

        float speedFactor = Mathf.Pow(disappearSpeedMultiplier, round - 1);
        if (roundTransitionText != null)
        {
            float lifetime = baseNodeLifetime / speedFactor;
            roundTransitionText.text = GetDifficultyName(round)
                + "\nNODE WINDOW  " + lifetime.ToString("0.0") + "S";
        }

        SetCanvasGroupVisible(roundTransitionGroup, true);
        roundTransitionGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            roundTransitionGroup.alpha = Mathf.Clamp01(elapsed / 0.2f);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.75f);

        elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.unscaledDeltaTime;
            roundTransitionGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.25f);
            yield return null;
        }

        SetCanvasGroupVisible(roundTransitionGroup, false);
    }

    private void UpdateHud()
    {
        float totalDuration = roundCount * roundDuration;
        float remaining = Mathf.Max(0f, totalDuration - elapsedSessionTime);

        if (timerText != null)
        {
            int secondsTotal = Mathf.CeilToInt(remaining);
            timerText.text = (secondsTotal / 60).ToString("00") + ":" + (secondsTotal % 60).ToString("00");
        }

        if (counterText != null)
        {
            counterText.text = capturedCount + " / " + requiredCaptures;
        }

        if (energyBar != null)
        {
            energyBar.minValue = 0f;
            energyBar.maxValue = requiredCaptures;
            energyBar.value = Mathf.Min(capturedCount, requiredCaptures);
        }

        float charge01 = requiredCaptures <= 0
            ? 0f
            : Mathf.Clamp01(capturedCount / (float)requiredCaptures);
        Color chargeColor = GetChargeColor(charge01);
        if (energyBarFill != null)
        {
            float pulse = charge01 >= 1f ? 0.86f + Mathf.Sin(Time.unscaledTime * 6f) * 0.14f : 1f;
            energyBarFill.color = new Color(chargeColor.r, chargeColor.g, chargeColor.b, pulse);
        }
        if (chargingStation != null)
        {
            chargingStation.SetCharge01(charge01);
        }

        if (roundText != null)
        {
            roundText.text = currentRound + " / " + roundCount;
        }

        if (systemStatusText != null)
        {
            if (sessionEnded)
            {
                systemStatusText.text = "SYSTEM RESTORED";
                systemStatusText.color = new Color(0.25f, 1f, 0.65f, 1f);
            }
            else if (!sessionStarted)
            {
                systemStatusText.text = sessionStarting ? "SYSTEM STARTING // 3-2-1" : "AWAITING HAND LINK";
                systemStatusText.color = new Color(1f, 0.78f, 0.25f, 1f);
            }
            else
            {
                systemStatusText.text = charge01 >= 1f ? "PRECISION FULL // KEEP PRACTICING" : "PINCH NODES TO CHARGE";
                systemStatusText.color = new Color(0.2f, 1f, 0.85f, 1f);
            }
        }

        if (roundIndicators != null)
        {
            for (int i = 0; i < roundIndicators.Length; i++)
            {
                if (roundIndicators[i] == null)
                {
                    continue;
                }

                roundIndicators[i].color = i < currentRound - 1
                    ? new Color(0.2f, 1f, 0.55f, 0.95f)
                    : i == currentRound - 1
                        ? GetRoundColor(currentRound)
                        : new Color(0.2f, 0.42f, 0.5f, 0.42f);
            }
        }
    }

    private void AutoBindReferences()
    {
        if (handTracker == null)
        {
            handTracker = Object.FindFirstObjectByType<HandTracker>();
        }

        if (handCursor == null && handTracker != null)
        {
            handCursor = handTracker.transform;
        }

        if (timerText == null)
        {
            timerText = FindSceneComponent<Text>("Text_Timer");
        }

        if (counterText == null)
        {
            counterText = FindSceneComponent<Text>("Text_TargetCounter");
        }

        if (energyBar == null)
        {
            energyBar = FindSceneComponent<Slider>("EnergyBar");
        }
        if (energyBarFill == null && energyBar != null && energyBar.fillRect != null)
        {
            energyBarFill = energyBar.fillRect.GetComponent<Image>();
        }

        if (systemStatusText == null)
        {
            systemStatusText = FindSceneComponent<Text>("Text_PrecisionStatus");
        }

        if (roundText == null)
        {
            roundText = FindSceneComponent<Text>("Text_Round");
        }

        if (gameClearPanel == null)
        {
            gameClearPanel = FindSceneObject("Panel_GameClear");
        }

        if (storyPanel == null)
        {
            storyPanel = FindSceneObject("MicroNode_StoryPanel");
        }

        if (roundTransitionGroup == null)
        {
            GameObject transition = FindSceneObject("MicroNode_RoundTransition");
            roundTransitionGroup = transition != null ? transition.GetComponent<CanvasGroup>() : null;
        }

        if (roundTransitionText == null)
        {
            roundTransitionText = FindSceneComponent<Text>("Text_RoundTransition");
        }

        if (gameOverPanel == null)
        {
            gameOverPanel = FindSceneObject("Panel_GameOver");
        }

        if (maleRobot == null)
        {
            maleRobot = FindSceneObject("Male0001");
        }

        if (femaleRobot == null)
        {
            femaleRobot = FindSceneObject("Female0001");
        }
        if (chargingStation == null)
        {
            chargingStation = GetComponent<MicroNodeChargingStation>();
        }
    }

    private void EnsureRuntimeObjects()
    {
        GameObject rootObject = FindSceneObject("MicroNode_PlayField");
        if (rootObject == null)
        {
            rootObject = new GameObject("MicroNode_PlayField");
        }

        nodeRoot = rootObject.transform;
        EnsureCircuitField();
        EnsureReferenceHud();

        if (chargingStation == null)
        {
            chargingStation = gameObject.AddComponent<MicroNodeChargingStation>();
        }
        chargingStation.SetRobotTarget(GetActiveRobotTransform());

        if (handCursor != null)
        {
            Transform visual = handCursor.Find("MicroNode_CursorVisual");
            GameObject visualObject;

            if (visual == null)
            {
                visualObject = new GameObject("MicroNode_CursorVisual");
                visualObject.transform.SetParent(handCursor, false);
            }
            else
            {
                visualObject = visual.gameObject;
            }

            cursorRenderer = visualObject.GetComponent<SpriteRenderer>();
            if (cursorRenderer == null)
            {
                cursorRenderer = visualObject.AddComponent<SpriteRenderer>();
            }

            cursorRenderer.sprite = CreateCursorSprite();
            cursorRenderer.sortingOrder = 190;
            cursorRenderer.color = new Color(0f, 1f, 1f, 0.85f);
        }
    }

    private void EnsureReferenceHud()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Transform canvasTransform = canvas.transform;
        Text title = EnsureHudText(canvasTransform, "Text_MicroNodeHeroTitle", "MICRO NODE ACTIVATION", new Vector2(0f, -28f), new Vector2(620f, 42f), 30, TextAnchor.MiddleCenter);
        title.color = new Color32(226, 242, 255, 255);
        Text subtitle = EnsureHudText(canvasTransform, "Text_MicroNodeHeroSubtitle", "Precision System (Fingers)", new Vector2(0f, -62f), new Vector2(520f, 28f), 17, TextAnchor.MiddleCenter);
        subtitle.color = new Color32(136, 201, 255, 255);

        Transform leftPanel = EnsureHudPanel(canvasTransform, "MicroNode_LeftHudPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -72f), new Vector2(170f, 128f));
        EnsureHudText(leftPanel, "Text_MiniGame", "MINI GAME 1/3", new Vector2(0f, -18f), new Vector2(128f, 24f), 13, TextAnchor.MiddleLeft).color = new Color32(136, 179, 225, 255);
        EnsureHudText(leftPanel, "Text_TimeLabel", "TIME REMAINING", new Vector2(0f, -58f), new Vector2(128f, 24f), 12, TextAnchor.MiddleLeft).color = new Color32(118, 160, 205, 255);
        timerText = timerText != null ? timerText : EnsureHudText(leftPanel, "Text_Timer", "00:00", new Vector2(12f, -86f), new Vector2(116f, 32f), 23, TextAnchor.MiddleLeft);
        ReparentHudText(timerText, leftPanel, new Vector2(12f, -88f), new Vector2(116f, 34f), 23, TextAnchor.MiddleLeft, new Color32(226, 238, 250, 255));

        Transform roundPanel = EnsureHudPanel(canvasTransform, "MicroNode_RoundHudPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-96f, -76f), new Vector2(170f, 136f));
        EnsureHudText(roundPanel, "Text_RoundLabel", "ROUND", new Vector2(0f, -20f), new Vector2(128f, 24f), 12, TextAnchor.MiddleLeft).color = new Color32(118, 160, 205, 255);
        roundText = roundText != null ? roundText : EnsureHudText(roundPanel, "Text_Round", "1 / 3", new Vector2(0f, -50f), new Vector2(128f, 28f), 22, TextAnchor.MiddleLeft);
        ReparentHudText(roundText, roundPanel, new Vector2(0f, -52f), new Vector2(128f, 30f), 22, TextAnchor.MiddleLeft, new Color32(226, 238, 250, 255));
        EnsureHudText(roundPanel, "Text_SpeedLabel", "SPEED MULTIPLIER", new Vector2(0f, -88f), new Vector2(132f, 24f), 12, TextAnchor.MiddleLeft).color = new Color32(118, 160, 205, 255);
        EnsureHudText(roundPanel, "Text_SpeedValue", "x1.0", new Vector2(0f, -114f), new Vector2(132f, 24f), 18, TextAnchor.MiddleLeft).color = new Color32(200, 222, 245, 255);

        Transform statusPanel = EnsureHudPanel(canvasTransform, "MicroNode_StatusHudPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-96f, -264f), new Vector2(190f, 190f));
        EnsureHudText(statusPanel, "Text_NodesLabel", "NODES COLLECTED", new Vector2(0f, -22f), new Vector2(146f, 24f), 12, TextAnchor.MiddleCenter).color = new Color32(118, 190, 235, 255);
        counterText = counterText != null ? counterText : EnsureHudText(statusPanel, "Text_TargetCounter", "0 / 18", new Vector2(0f, -54f), new Vector2(146f, 30f), 24, TextAnchor.MiddleCenter);
        ReparentHudText(counterText, statusPanel, new Vector2(0f, -56f), new Vector2(146f, 32f), 24, TextAnchor.MiddleCenter, new Color32(226, 238, 250, 255));
        energyBar = energyBar != null ? energyBar : EnsureHudSlider(statusPanel, "EnergyBar");
        ReparentHudSlider(energyBar, statusPanel, new Vector2(0f, -88f), new Vector2(152f, 16f));
        EnsureHudText(statusPanel, "Text_EnergyLabel", "SYSTEM POWER", new Vector2(0f, -130f), new Vector2(146f, 22f), 12, TextAnchor.MiddleCenter).color = new Color32(118, 160, 205, 255);

        for (int i = 0; i < 12; i++)
        {
            Transform bar = EnsureHudPanel(statusPanel, "SystemPowerBar_" + i, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-66f + i * 12f, -158f), new Vector2(7f, 28f));
            Image image = bar.GetComponent<Image>();
            image.color = i < 10 ? new Color32(105, 207, 255, 235) : new Color32(35, 78, 119, 150);
        }
    }

    private static Transform EnsureHudPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject panel = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = panel.AddComponent<RectTransform>();
        }
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        if (image == null)
        {
            image = panel.AddComponent<Image>();
        }
        image.color = new Color32(4, 15, 39, 218);
        image.raycastTarget = false;

        Outline outline = panel.GetComponent<Outline>();
        if (outline == null)
        {
            outline = panel.AddComponent<Outline>();
        }
        outline.effectColor = new Color32(74, 155, 255, 112);
        outline.effectDistance = new Vector2(2f, -2f);

        return panel.transform;
    }

    private static Text EnsureHudText(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        Transform existing = parent.Find(name);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        if (text == null)
        {
            text = textObject.AddComponent<Text>();
        }
        text.text = value;
        ApplyHudTextStyle(text, fontSize, alignment, new Color32(226, 238, 250, 255));

        RectTransform rect = textObject.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = textObject.AddComponent<RectTransform>();
        }
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        return text;
    }

    private static void ReparentHudText(Text text, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        if (text == null)
        {
            return;
        }

        text.transform.SetParent(parent, false);
        ApplyHudTextStyle(text, fontSize, alignment, color);

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void ApplyHudTextStyle(Text text, int fontSize, TextAnchor alignment, Color color)
    {
        text.font = text.font != null ? text.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.resizeTextForBestFit = false;
        text.raycastTarget = false;
    }

    private static Slider EnsureHudSlider(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject sliderObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);

        Slider slider = sliderObject.GetComponent<Slider>();
        if (slider == null)
        {
            slider = sliderObject.AddComponent<Slider>();
        }
        slider.transition = Selectable.Transition.None;

        Transform background = sliderObject.transform.Find("Background");
        if (background == null)
        {
            background = new GameObject("Background", typeof(RectTransform), typeof(Image)).transform;
            background.SetParent(sliderObject.transform, false);
        }
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = background.gameObject.AddComponent<Image>();
        }
        backgroundImage.color = new Color32(7, 24, 52, 220);
        backgroundImage.raycastTarget = false;

        Transform fillArea = sliderObject.transform.Find("Fill Area");
        if (fillArea == null)
        {
            fillArea = new GameObject("Fill Area", typeof(RectTransform)).transform;
            fillArea.SetParent(sliderObject.transform, false);
        }
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(2f, 2f);
        fillAreaRect.offsetMax = new Vector2(-2f, -2f);

        Transform fill = fillArea.Find("Fill");
        if (fill == null)
        {
            fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).transform;
            fill.SetParent(fillArea, false);
        }
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.GetComponent<Image>();
        if (fillImage == null)
        {
            fillImage = fill.gameObject.AddComponent<Image>();
        }
        fillImage.color = new Color32(114, 234, 76, 255);
        fillImage.raycastTarget = false;

        slider.fillRect = fillRect;
        slider.targetGraphic = fillImage;
        slider.handleRect = null;
        return slider;
    }

    private void ReparentHudSlider(Slider slider, Transform parent, Vector2 position, Vector2 size)
    {
        if (slider == null)
        {
            return;
        }

        slider.transform.SetParent(parent, false);
        RectTransform rect = slider.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        if (slider.fillRect != null)
        {
            energyBarFill = slider.fillRect.GetComponent<Image>();
        }
    }

    private void EnsureCircuitField()
    {
        GameObject existing = FindSceneObject("MicroNode_CircuitField");
        if (existing != null)
        {
            Destroy(existing);
        }

        GameObject field = new GameObject("MicroNode_CircuitField");
        field.transform.position = Vector3.zero;

        CreateBlock(field.transform, "DeepSpaceBackdrop", new Vector3(0f, 0.35f, 0.45f), new Vector3(18.8f, 10.8f, 1f), new Color(0.012f, 0.025f, 0.095f, 1f), 0);
        CreateBlock(field.transform, "HangarHaze", new Vector3(0f, 0.62f, 0.42f), new Vector3(15.4f, 7.2f, 1f), new Color(0.02f, 0.12f, 0.3f, 0.42f), 1);

        CreateChamberPanel(field.transform, "LeftWall", new Vector3(-5.7f, 1.1f, 0.32f), new Vector3(3.5f, 4.1f, 1f));
        CreateChamberPanel(field.transform, "CenterDoor", new Vector3(0f, 1.15f, 0.31f), new Vector3(4.25f, 4.35f, 1f));
        CreateChamberPanel(field.transform, "RightWall", new Vector3(5.7f, 1.1f, 0.32f), new Vector3(3.5f, 4.1f, 1f));

        CreateBlock(field.transform, "FloorPlate", new Vector3(0f, -2.08f, 0.28f), new Vector3(17.6f, 3.2f, 1f), new Color(0.015f, 0.055f, 0.16f, 0.86f), 3);
        CreateEllipseLine(field.transform, "FloorOuterRing", new Vector3(0f, -1.58f, 0.18f), 4.6f, 1.05f, 72, 0.1f, new Color(0.13f, 0.42f, 1f, 0.5f), 8);
        CreateEllipseLine(field.transform, "FloorInnerRing", new Vector3(0f, -1.58f, 0.17f), 3.35f, 0.72f, 72, 0.045f, new Color(0.25f, 0.85f, 1f, 0.38f), 9);
        CreateEllipseLine(field.transform, "FloorPowerArc", new Vector3(-0.35f, -1.58f, 0.16f), 3.7f, 0.82f, 52, 0.16f, new Color(0.52f, 1f, 0.24f, 0.95f), 10, -168f, 74f);

        CreatePointerBeam(field.transform);
        CreateCyberHand(field.transform);

        Vector3[][] paths =
        {
            new[] { new Vector3(-7.4f, 2.9f), new Vector3(-3.2f, 2.9f), new Vector3(-2.65f, 2.45f), new Vector3(2.2f, 2.45f), new Vector3(2.75f, 2.9f), new Vector3(7.4f, 2.9f) },
            new[] { new Vector3(-7.35f, 1.3f), new Vector3(-4.65f, 1.3f), new Vector3(-4.1f, 0.85f), new Vector3(4.1f, 0.85f), new Vector3(4.65f, 1.3f), new Vector3(7.35f, 1.3f) },
            new[] { new Vector3(-7.2f, -0.35f), new Vector3(-2.1f, -0.35f), new Vector3(-1.55f, 0.05f), new Vector3(1.55f, 0.05f), new Vector3(2.1f, -0.35f), new Vector3(7.2f, -0.35f) },
            new[] { new Vector3(-6.85f, -2.45f), new Vector3(-3.8f, -1.72f), new Vector3(3.8f, -1.72f), new Vector3(6.85f, -2.45f) },
            new[] { new Vector3(-5.45f, -2.7f), new Vector3(-5.45f, -0.9f), new Vector3(-4.9f, -0.45f), new Vector3(-4.9f, 2.65f) },
            new[] { new Vector3(5.45f, -2.7f), new Vector3(5.45f, -0.9f), new Vector3(4.9f, -0.45f), new Vector3(4.9f, 2.65f) }
        };

        for (int i = 0; i < paths.Length; i++)
        {
            CreateCircuitLine(field.transform, "CircuitTrace_" + i, paths[i]);
        }
    }

    private static void CreateChamberPanel(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        CreateBlock(parent, name + "_Glass", position, scale, new Color(0.02f, 0.12f, 0.36f, 0.34f), 4);
        CreateBlock(parent, name + "_CoreGlow", position + new Vector3(0f, 0f, -0.02f), new Vector3(scale.x * 0.58f, scale.y * 0.56f, 1f), new Color(0.05f, 0.28f, 0.86f, 0.22f), 5);
        CreateBlock(parent, name + "_TopLight", position + new Vector3(0f, scale.y * 0.42f, -0.04f), new Vector3(scale.x * 0.32f, 0.08f, 1f), new Color(0.22f, 0.72f, 1f, 0.78f), 11);
    }

    private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Color color, int order)
    {
        GameObject block = new GameObject(name);
        block.transform.SetParent(parent, false);
        block.transform.localPosition = position;
        block.transform.localScale = scale;
        SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSquareSprite();
        renderer.color = color;
        renderer.sortingOrder = order;
        return block;
    }

    private static void CreatePointerBeam(Transform parent)
    {
        CreateCircuitLine(
            parent,
            "FingerGuidanceBeam",
            new[]
            {
                new Vector3(1.05f, -2.45f, 0.05f),
                new Vector3(1.45f, -1.4f, 0.05f),
                new Vector3(2.0f, -0.55f, 0.05f),
                new Vector3(2.68f, 0.25f, 0.05f)
            },
            0.055f,
            new Color(0.48f, 1f, 1f, 0.72f),
            145
        );
    }

    private static void CreateCyberHand(Transform parent)
    {
        Transform hand = new GameObject("MicroNode_CyberHand").transform;
        hand.SetParent(parent, false);
        hand.localPosition = new Vector3(4.45f, -2.25f, -0.06f);
        hand.localRotation = Quaternion.Euler(0f, 0f, -24f);

        CreateBlock(hand, "Palm", Vector3.zero, new Vector3(1.2f, 1.55f, 1f), new Color(0.72f, 0.78f, 0.86f, 0.96f), 130);
        CreateBlock(hand, "PalmShadow", new Vector3(-0.1f, -0.04f, 0.01f), new Vector3(0.92f, 1.28f, 1f), new Color(0.04f, 0.08f, 0.14f, 0.9f), 129);
        CreateBlock(hand, "Wrist", new Vector3(0.02f, -1.15f, 0.01f), new Vector3(1.05f, 0.56f, 1f), new Color(0.62f, 0.68f, 0.77f, 0.98f), 128);
        CreateBlock(hand, "BlueSocket", new Vector3(0.32f, -0.64f, -0.02f), new Vector3(0.26f, 0.26f, 1f), new Color(0.08f, 0.48f, 1f, 0.95f), 132);

        for (int i = 0; i < 4; i++)
        {
            float x = -0.48f + i * 0.32f;
            float y = 0.78f + Mathf.Abs(i - 1.5f) * 0.04f;
            CreateBlock(hand, "Finger_" + i + "_Base", new Vector3(x, y, 0f), new Vector3(0.22f, 0.72f, 1f), new Color(0.78f, 0.83f, 0.9f, 0.96f), 133);
            CreateBlock(hand, "Finger_" + i + "_Tip", new Vector3(x, y + 0.53f, -0.01f), new Vector3(0.18f, 0.42f, 1f), new Color(0.9f, 0.94f, 0.98f, 0.96f), 134);
            CreateBlock(hand, "Finger_" + i + "_Joint", new Vector3(x, y + 0.15f, -0.02f), new Vector3(0.22f, 0.12f, 1f), new Color(0.04f, 0.08f, 0.14f, 0.96f), 135);
        }

        CreateBlock(hand, "Thumb", new Vector3(-0.78f, -0.08f, -0.01f), new Vector3(0.34f, 0.92f, 1f), new Color(0.82f, 0.87f, 0.93f, 0.96f), 134).transform.localRotation = Quaternion.Euler(0f, 0f, 38f);
    }

    private static void CreateEllipseLine(Transform parent, string name, Vector3 center, float radiusX, float radiusY, int points, float width, Color color, int order, float startDegrees = 0f, float arcDegrees = 360f)
    {
        Vector3[] positions = new Vector3[points + 1];
        for (int i = 0; i < positions.Length; i++)
        {
            float t = i / (float)points;
            float angle = (startDegrees + arcDegrees * t) * Mathf.Deg2Rad;
            positions[i] = center + new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0f);
        }

        CreateCircuitLine(parent, name, positions, width, color, order);
    }

    private static void CreateCircuitLine(Transform parent, string name, Vector3[] positions)
    {
        CreateCircuitLine(parent, name, positions, 0.025f, new Color(0f, 0.72f, 1f, 0.16f), 12);
    }

    private static void CreateCircuitLine(Transform parent, string name, Vector3[] positions, float width, Color color, int order)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = positions.Length;
        line.SetPositions(positions);
        line.widthMultiplier = width;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.startColor = color;
        line.endColor = color;
        line.sortingOrder = order;

        if (circuitLineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                circuitLineMaterial = new Material(shader)
                {
                    name = "Runtime_MicroNodeCircuit"
                };
            }
        }

        if (circuitLineMaterial != null)
        {
            line.sharedMaterial = circuitLineMaterial;
        }
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite == null)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
            squareSprite.name = "Runtime_MicroNodeSquare";
        }

        return squareSprite;
    }

    private void DisablePreviousMinigameSystems()
    {
        EverMotionGameManager previousManager = Object.FindFirstObjectByType<EverMotionGameManager>();
        if (previousManager != null)
        {
            previousManager.enabled = false;
        }

        LaserSpawner spawner = Object.FindFirstObjectByType<LaserSpawner>();
        if (spawner != null)
        {
            spawner.StopSpawning();
            spawner.enabled = false;
        }

        RehabTarget oldTarget = Object.FindFirstObjectByType<RehabTarget>();
        if (oldTarget != null)
        {
            oldTarget.enabled = false;
        }

        RobotAnimationController[] robotControllers = Object.FindObjectsByType<RobotAnimationController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (RobotAnimationController controller in robotControllers)
        {
            controller.enabled = false;
        }
    }

    private MicroNodeRobotFeedback GetActiveRobotFeedback()
    {
        GameObject robot = maleRobot != null && maleRobot.activeInHierarchy ? maleRobot : femaleRobot;
        return robot != null ? robot.GetComponent<MicroNodeRobotFeedback>() : null;
    }

    private void SetActiveRobot(bool useFemale)
    {
        if (maleRobot != null)
        {
            maleRobot.SetActive(!useFemale);
        }

        if (femaleRobot != null)
        {
            femaleRobot.SetActive(useFemale);
        }

        if (chargingStation != null)
        {
            chargingStation.SetRobotTarget(GetActiveRobotTransform());
        }
    }

    private void ClearActiveNodes()
    {
        for (int i = activeNodes.Count - 1; i >= 0; i--)
        {
            if (activeNodes[i] != null)
            {
                Destroy(activeNodes[i].gameObject);
            }
        }

        activeNodes.Clear();
    }

    private static Color GetRoundColor(int round)
    {
        switch (round)
        {
            case 2:
                return new Color(0.2f, 1f, 0.55f, 1f);
            case 3:
                return new Color(1f, 0.3f, 0.85f, 1f);
            default:
                return new Color(0f, 0.95f, 1f, 1f);
        }
    }

    private int GetRoundActiveNodeCount()
    {
        if (currentRound <= 1)
        {
            return Mathf.Max(1, easyActiveNodes);
        }
        if (currentRound == 2)
        {
            return Mathf.Max(1, mediumActiveNodes);
        }

        return Mathf.Max(1, activeNodeCount);
    }

    private float GetRoundCaptureRadius()
    {
        if (currentRound <= 1)
        {
            return easyCaptureRadius;
        }
        if (currentRound == 2)
        {
            return mediumCaptureRadius;
        }

        return captureRadius;
    }

    private float GetRoundNodeScale()
    {
        if (currentRound <= 1)
        {
            return easyNodeScale;
        }
        if (currentRound == 2)
        {
            return mediumNodeScale;
        }

        return 1f;
    }

    private Transform GetActiveRobotTransform()
    {
        if (femaleRobot != null && femaleRobot.activeInHierarchy)
        {
            return femaleRobot.transform;
        }

        return maleRobot != null ? maleRobot.transform : null;
    }

    private static string GetDifficultyName(int round)
    {
        switch (round)
        {
            case 1:
                return "ROUND 1 // EASY";
            case 2:
                return "ROUND 2 // MEDIUM";
            default:
                return "ROUND 3 // HARD";
        }
    }

    private static Color GetChargeColor(float value)
    {
        if (value < 0.5f)
        {
            return Color.Lerp(
                new Color(1f, 0.12f, 0.2f, 1f),
                new Color(1f, 0.78f, 0.08f, 1f),
                value / 0.5f
            );
        }

        return Color.Lerp(
            new Color(1f, 0.78f, 0.08f, 1f),
            new Color(0.16f, 1f, 0.42f, 1f),
            (value - 0.5f) / 0.5f
        );
    }

    private static void SetPanelVisible(GameObject panel, bool visible)
    {
        if (panel != null)
        {
            panel.SetActive(visible);
        }
    }

    private static void SetCanvasGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(visible);
    }

    private static void SetEndPanelText(GameObject panel, string title, string subtitle)
    {
        if (panel == null)
        {
            return;
        }

        Text[] texts = panel.GetComponentsInChildren<Text>(true);
        if (texts.Length > 0)
        {
            texts[0].text = title;
        }

        if (texts.Length > 1)
        {
            texts[1].text = subtitle;
        }
    }

    private static T FindSceneComponent<T>(string objectName) where T : Component
    {
        GameObject target = FindSceneObject(objectName);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Transform transform in transforms)
        {
            if (transform.name == objectName && transform.gameObject.scene.IsValid())
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    private static Sprite CreateCursorSprite()
    {
        const int size = 80;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pixel = new Vector2(x, y);
                float distance = Vector2.Distance(pixel, center);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - radius * 0.55f) / 2.5f) * 0.55f;
                float core = Mathf.Clamp01(1f - distance / 8f);
                bool left = x < 18;
                bool right = x > size - 19;
                bool top = y > size - 19;
                bool bottom = y < 18;
                bool horizontalCorner = (top || bottom) && (x < 30 || x > size - 31);
                bool verticalCorner = (left || right) && (y < 30 || y > size - 31);
                float bracket = horizontalCorner || verticalCorner ? 0.95f : 0f;
                float cross = Mathf.Abs(x - center.x) < 1.5f || Mathf.Abs(y - center.y) < 1.5f ? 0.5f : 0f;
                texture.SetPixel(x, y, new Color(0.68f, 1f, 1f, Mathf.Max(bracket, Mathf.Max(ring, Mathf.Max(core, cross)))));
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "Runtime_MicroNode_Cursor";
        return sprite;
    }

    private static bool IsMousePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    private static Vector2 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    private static bool WasRestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    private static bool WasFemalePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F);
#endif
    }

    private static bool WasMalePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.M);
#endif
    }
}
