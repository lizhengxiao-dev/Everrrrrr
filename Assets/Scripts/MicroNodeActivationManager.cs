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
    public int requiredCaptures = 20;
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

    [Header("Runtime World UI Tuning")]
    public Vector2 titleFrameOffset = Vector2.zero;
    [Range(0.65f, 1.45f)] public float titleFrameScale = 1f;
    [Range(0.65f, 1.65f)] public float titleFontScale = 1f;
    public Vector2 nodeStatusPanelOffset = Vector2.zero;
    [Range(0.65f, 1.45f)] public float nodeStatusPanelScale = 1f;
    [Range(0.65f, 1.65f)] public float nodeStatusFontScale = 1f;
    public bool autoScaleWorldUi = true;
    public Vector2 worldUiReferenceResolution = new Vector2(1024f, 768f);
    [Range(0.45f, 1.2f)] public float worldUiMinAutoScale = 0.7f;
    [Range(1f, 2f)] public float worldUiMaxAutoScale = 1.35f;

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
    private bool finalizingSession;
    private static Material circuitLineMaterial;
    private static Sprite titleFrameSprite;
    private TextMesh titleFrameTimerText;
    private TextMesh titleFrameRoundText;
    private TextMesh nodeStatusCounterText;
    private Transform nodeStatusFillTransform;
    private SpriteRenderer[] systemPowerBars;

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
        roundCount = 3;
        roundDuration = 60f;
        requiredCaptures = 20;
        currentRound = 1;
        capturedCount = 0;
        missedCount = 0;
        sessionEnded = false;
        sessionStarted = !waitForMediaPipeBeforeStarting;
        sessionStarting = false;
        roundTransitionActive = false;
        finalizingSession = false;

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
            if (!finalizingSession)
            {
                StartCoroutine(EndSessionRoutine());
            }
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
        capturedCount = Mathf.Min(capturedCount + 1, requiredCaptures);
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

    private IEnumerator EndSessionRoutine()
    {
        finalizingSession = true;
        roundTransitionActive = true;
        ClearActiveNodes();

        if (capturedCount >= requiredCaptures)
        {
            yield return PlayRoundPowerUpRoutine();
        }

        EndSession();
        roundTransitionActive = false;
        finalizingSession = false;
    }

    private IEnumerator RoundTransitionRoutine(int nextRound)
    {
        roundTransitionActive = true;
        ClearActiveNodes();

        if (capturedCount >= requiredCaptures)
        {
            yield return PlayRoundPowerUpRoutine();
        }

        capturedCount = 0;
        missedCount = 0;
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
            if (titleFrameTimerText != null)
            {
                titleFrameTimerText.text = timerText.text;
            }
        }

        if (counterText != null)
        {
            counterText.text = "NODES  " + Mathf.Min(capturedCount, requiredCaptures).ToString("00") + " / " + requiredCaptures.ToString("00");
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
        UpdateNodeStatusPanel(charge01);
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
            roundText.text = "ROUND " + currentRound + " / " + roundCount;
            if (titleFrameRoundText != null)
            {
                titleFrameRoundText.text = currentRound + "/" + roundCount;
            }
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
        EnsureTopTitleFrame();
        EnsureNodeStatusPanel();

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

    private void EnsureTopTitleFrame()
    {
        GameObject oldCanvasFrame = FindSceneObject("MicroNode_TopTitleFrame");
        if (oldCanvasFrame != null && oldCanvasFrame.GetComponent<RectTransform>() != null)
        {
            oldCanvasFrame.SetActive(false);
        }

        GameObject frame = FindSceneObject("MicroNode_TopTitleFrame_World");
        if (frame == null)
        {
            frame = new GameObject("MicroNode_TopTitleFrame_World");
        }

        frame.transform.position = GetTitleFrameWorldPosition() + new Vector3(titleFrameOffset.x, titleFrameOffset.y, 0f);
        frame.transform.rotation = Quaternion.identity;
        frame.transform.localScale = Vector3.one * titleFrameScale * GetWorldUiAutoScale();

        SpriteRenderer frameRenderer = frame.GetComponent<SpriteRenderer>();
        if (frameRenderer != null)
        {
            frameRenderer.enabled = false;
        }
        EnsureWorldBlock(frame.transform, "TitleFrame_Backplate", Vector3.zero, new Vector3(5.2f, 1.24f, 1f), new Color32(8, 9, 14, 215), 220);

        EnsureWorldFrameText(frame.transform, "Text_MicroNodeFrameTitle", "MICRO NODE ACTIVATION", new Vector3(0f, 0.36f, -0.02f), 0.038f * titleFontScale, TextAnchor.MiddleCenter, new Color32(230, 232, 240, 255), 230);
        EnsureWorldFrameText(frame.transform, "Text_MicroNodeFrameSubtitle", "Precision System (Fingers)", new Vector3(0f, 0.12f, -0.02f), 0.021f * titleFontScale, TextAnchor.MiddleCenter, new Color32(185, 190, 206, 255), 230);
        EnsureWorldFrameText(frame.transform, "Text_MicroNodeFrameTimeLabel", "TIME REMAINING", new Vector3(-1.7f, -0.22f, -0.02f), 0.017f * titleFontScale, TextAnchor.MiddleLeft, new Color32(150, 156, 174, 255), 230);
        EnsureWorldFrameText(frame.transform, "Text_MicroNodeFrameRoundLabel", "ROUND", new Vector3(1.74f, -0.22f, -0.02f), 0.017f * titleFontScale, TextAnchor.MiddleLeft, new Color32(150, 156, 174, 255), 230);

        titleFrameTimerText = EnsureWorldFrameText(frame.transform, "Text_MicroNodeFrameTimer", "00:00", new Vector3(-1.7f, -0.46f, -0.02f), 0.036f * titleFontScale, TextAnchor.MiddleLeft, new Color32(238, 239, 246, 255), 231);
        titleFrameRoundText = EnsureWorldFrameText(frame.transform, "Text_MicroNodeFrameRound", "1/3", new Vector3(1.74f, -0.46f, -0.02f), 0.036f * titleFontScale, TextAnchor.MiddleLeft, new Color32(238, 239, 246, 255), 231);
    }

    private void EnsureNodeStatusPanel()
    {
        GameObject panel = FindSceneObject("MicroNode_NodeStatusPanel_World");
        if (panel == null)
        {
            panel = new GameObject("MicroNode_NodeStatusPanel_World");
        }

        panel.transform.position = GetNodeStatusPanelWorldPosition() + new Vector3(nodeStatusPanelOffset.x, nodeStatusPanelOffset.y, 0f);
        panel.transform.rotation = Quaternion.identity;
        panel.transform.localScale = Vector3.one * nodeStatusPanelScale * GetWorldUiAutoScale();

        SpriteRenderer rootRenderer = panel.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }

        EnsureWorldBlock(panel.transform, "NodeStatus_Backplate", Vector3.zero, new Vector3(1.76f, 1.42f, 1f), new Color32(8, 9, 14, 218), 220);
        EnsureWorldFrameText(panel.transform, "Text_NodeStatusLabel", "NODES COLLECTED", new Vector3(0f, 0.49f, -0.03f), 0.016f * nodeStatusFontScale, TextAnchor.MiddleCenter, new Color32(137, 168, 192, 255), 231);
        nodeStatusCounterText = EnsureWorldFrameText(panel.transform, "Text_NodeStatusCounter", "0 / 20", new Vector3(0f, 0.27f, -0.03f), 0.036f * nodeStatusFontScale, TextAnchor.MiddleCenter, new Color32(235, 237, 244, 255), 232);

        EnsureWorldBlock(panel.transform, "NodeStatus_ProgressTrack", new Vector3(0f, 0.06f, -0.03f), new Vector3(1.2f, 0.12f, 1f), new Color32(17, 28, 42, 235), 231);
        nodeStatusFillTransform = EnsureWorldBlock(panel.transform, "NodeStatus_ProgressFill", new Vector3(-0.57f, 0.06f, -0.04f), new Vector3(0.02f, 0.12f, 1f), new Color32(116, 228, 76, 255), 232).transform;

        EnsureWorldFrameText(panel.transform, "Text_SystemPowerLabel", "SYSTEM POWER", new Vector3(0f, -0.26f, -0.03f), 0.016f * nodeStatusFontScale, TextAnchor.MiddleCenter, new Color32(137, 168, 192, 255), 231);

        const int barCount = 13;
        if (systemPowerBars == null || systemPowerBars.Length != barCount)
        {
            systemPowerBars = new SpriteRenderer[barCount];
        }

        float startX = -0.49f;
        for (int i = 0; i < barCount; i++)
        {
            SpriteRenderer bar = EnsureWorldBlock(
                panel.transform,
                "SystemPower_Bar_" + i.ToString("00"),
                new Vector3(startX + i * 0.082f, -0.49f, -0.04f),
                new Vector3(0.045f, 0.22f, 1f),
                new Color32(102, 186, 255, 255),
                232
            );
            systemPowerBars[i] = bar;
        }
    }

    private void UpdateNodeStatusPanel(float charge01)
    {
        int displayCount = Mathf.Min(capturedCount, requiredCaptures);
        if (nodeStatusCounterText != null)
        {
            nodeStatusCounterText.text = displayCount + " / " + requiredCaptures;
        }

        if (nodeStatusFillTransform != null)
        {
            float width = Mathf.Lerp(0.02f, 1.2f, charge01);
            nodeStatusFillTransform.localScale = new Vector3(width, 0.12f, 1f);
            nodeStatusFillTransform.localPosition = new Vector3(-0.6f + width * 0.5f, 0.06f, -0.04f);
        }

        if (systemPowerBars == null)
        {
            return;
        }

        int activeBars = Mathf.CeilToInt(charge01 * systemPowerBars.Length);
        for (int i = 0; i < systemPowerBars.Length; i++)
        {
            if (systemPowerBars[i] == null)
            {
                continue;
            }

            systemPowerBars[i].color = i < activeBars
                ? new Color32(108, 190, 255, 255)
                : new Color32(32, 65, 92, 180);
        }
    }

    private static Vector3 GetTitleFrameWorldPosition()
    {
        SpriteRenderer background = FindSceneComponent<SpriteRenderer>("CyberBackground_WORLD");
        if (background != null)
        {
            Bounds bounds = background.bounds;
            return new Vector3(bounds.center.x, bounds.max.y - 0.46f, -0.2f);
        }

        Camera camera = Camera.main;
        float top = camera != null && camera.orthographic
            ? camera.transform.position.y + camera.orthographicSize
            : 4.5f;
        return new Vector3(0f, top - 0.6f, -0.2f);
    }

    private static Vector3 GetNodeStatusPanelWorldPosition()
    {
        SpriteRenderer background = FindSceneComponent<SpriteRenderer>("CyberBackground_WORLD");
        if (background != null)
        {
            Bounds bounds = background.bounds;
            return new Vector3(bounds.max.x - 1.08f, bounds.max.y - 1.55f, -0.2f);
        }

        Camera camera = Camera.main;
        if (camera != null && camera.orthographic)
        {
            float right = camera.transform.position.x + camera.orthographicSize * camera.aspect;
            float top = camera.transform.position.y + camera.orthographicSize;
            return new Vector3(right - 1.15f, top - 1.65f, -0.2f);
        }

        return new Vector3(6.1f, 3f, -0.2f);
    }

    private float GetWorldUiAutoScale()
    {
        if (!autoScaleWorldUi)
        {
            return 1f;
        }

        float referenceWidth = Mathf.Max(1f, worldUiReferenceResolution.x);
        float referenceHeight = Mathf.Max(1f, worldUiReferenceResolution.y);
        float currentWidth = Screen.width > 0 ? Screen.width : referenceWidth;
        float currentHeight = Screen.height > 0 ? Screen.height : referenceHeight;
        float scale = Mathf.Min(currentWidth / referenceWidth, currentHeight / referenceHeight);
        return Mathf.Clamp(scale, worldUiMinAutoScale, worldUiMaxAutoScale);
    }

    private static SpriteRenderer EnsureWorldBlock(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, int sortingOrder)
    {
        Transform existing = parent.Find(name);
        GameObject blockObject = existing != null ? existing.gameObject : new GameObject(name);
        blockObject.transform.SetParent(parent, false);
        blockObject.transform.localPosition = localPosition;
        blockObject.transform.localRotation = Quaternion.identity;
        blockObject.transform.localScale = localScale;

        SpriteRenderer renderer = blockObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = blockObject.AddComponent<SpriteRenderer>();
        }

        renderer.enabled = true;
        renderer.sprite = GetTitleFrameSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static TextMesh EnsureWorldFrameText(Transform parent, string name, string value, Vector3 localPosition, float characterSize, TextAnchor alignment, Color color, int sortingOrder)
    {
        Transform existing = parent.Find(name);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(name);
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localPosition;
        textObject.transform.localRotation = Quaternion.identity;
        textObject.transform.localScale = Vector3.one;

        TextMesh text = textObject.GetComponent<TextMesh>();
        if (text == null)
        {
            text = textObject.AddComponent<TextMesh>();
        }
        text.text = value;
        text.fontSize = 64;
        text.characterSize = characterSize;
        text.fontStyle = FontStyle.Bold;
        text.anchor = alignment;
        text.alignment = GetTextAlignment(alignment);
        text.color = color;

        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        renderer.sortingOrder = sortingOrder;
        return text;
    }

    private static TextAlignment GetTextAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.LowerLeft:
            case TextAnchor.MiddleLeft:
            case TextAnchor.UpperLeft:
                return TextAlignment.Left;
            case TextAnchor.LowerRight:
            case TextAnchor.MiddleRight:
            case TextAnchor.UpperRight:
                return TextAlignment.Right;
            default:
                return TextAlignment.Center;
        }
    }

    private static Sprite GetTitleFrameSprite()
    {
        if (titleFrameSprite != null)
        {
            return titleFrameSprite;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        titleFrameSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        titleFrameSprite.name = "Runtime_MicroNodeTitleFrame";
        return titleFrameSprite;
    }

    private void EnsureCircuitField()
    {
        GameObject existing = FindSceneObject("MicroNode_CircuitField");
        if (existing != null)
        {
            return;
        }

        GameObject field = new GameObject("MicroNode_CircuitField");
        field.transform.position = new Vector3(0f, 0.35f, 0f);

        Vector3[][] paths =
        {
            new[] { new Vector3(-6.4f, 2.45f), new Vector3(-2.8f, 2.45f), new Vector3(-2.35f, 2f), new Vector3(1.7f, 2f), new Vector3(2.15f, 2.45f), new Vector3(6.4f, 2.45f) },
            new[] { new Vector3(-6.4f, 1.15f), new Vector3(-4.2f, 1.15f), new Vector3(-3.75f, 0.7f), new Vector3(3.3f, 0.7f), new Vector3(3.75f, 1.15f), new Vector3(6.4f, 1.15f) },
            new[] { new Vector3(-6.4f, -0.2f), new Vector3(-1.9f, -0.2f), new Vector3(-1.45f, 0.25f), new Vector3(1.5f, 0.25f), new Vector3(1.95f, -0.2f), new Vector3(6.4f, -0.2f) },
            new[] { new Vector3(-6.4f, -1.6f), new Vector3(-3.5f, -1.6f), new Vector3(-3.05f, -1.15f), new Vector3(3.8f, -1.15f), new Vector3(4.25f, -1.6f), new Vector3(6.4f, -1.6f) },
            new[] { new Vector3(-5.35f, -2.45f), new Vector3(-5.35f, -0.9f), new Vector3(-4.9f, -0.45f), new Vector3(-4.9f, 2.9f) },
            new[] { new Vector3(5.2f, -2.45f), new Vector3(5.2f, -0.75f), new Vector3(4.75f, -0.3f), new Vector3(4.75f, 2.9f) }
        };

        for (int i = 0; i < paths.Length; i++)
        {
            CreateCircuitLine(field.transform, "CircuitTrace_" + i, paths[i]);
        }
    }

    private static void CreateCircuitLine(Transform parent, string name, Vector3[] positions)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = positions.Length;
        line.SetPositions(positions);
        line.widthMultiplier = 0.025f;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.startColor = new Color(0f, 0.72f, 1f, 0.16f);
        line.endColor = new Color(0.2f, 1f, 0.82f, 0.16f);
        line.sortingOrder = 12;

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

    private IEnumerator PlayRoundPowerUpRoutine()
    {
        Transform robotTransform = GetActiveRobotTransform();
        GameObject robot = robotTransform != null ? robotTransform.gameObject : null;
        Animator animator = robot != null ? robot.GetComponent<Animator>() : null;
        if (robot == null || animator == null)
        {
            yield break;
        }

        bool isFemale = robot == femaleRobot;
        string powerState = FindFirstAnimatorState(animator, isFemale
            ? new[] { "Female_PoweredUp", "f_PoweredUp", "Female_PoweredUp_Anim" }
            : new[] { "Male_PoweredUp", "m_PoweredUp", "Male_PoweredUp_Anim" });

        if (string.IsNullOrEmpty(powerState))
        {
            Debug.LogWarning("MicroNodeActivationManager: active robot controller has no PoweredUp state.");
            yield break;
        }

        string idleState = FindFirstAnimatorState(animator, isFemale
            ? new[] { "Female_Idle", "Female_Idle_Anim", "F_idle" }
            : new[] { "Male_Idle", "Male_Idle_Anim", "M_idle" });

        Vector3 originalScale = robot.transform.localScale;
        animator.speed = 1f;
        animator.CrossFade(powerState, 0.06f, 0, 0f);

        float duration = Mathf.Max(2.35f, GetAnimationClipLength(animator, powerState) + 0.08f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            robot.transform.localScale = originalScale * (1f + pulse * 0.055f);
            yield return null;
        }

        robot.transform.localScale = originalScale;
        if (!string.IsNullOrEmpty(idleState))
        {
            animator.CrossFade(idleState, 0.08f, 0, 0f);
        }
    }

    private static string FindFirstAnimatorState(Animator animator, string[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            if (HasAnimatorState(animator, candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }

    private static float GetAnimationClipLength(Animator animator, string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(clipName))
        {
            return 0f;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name == clipName)
            {
                return clip.length;
            }
        }

        return 0f;
    }

    private static bool HasAnimatorState(Animator animator, string stateName)
    {
        return animator != null && !string.IsNullOrEmpty(stateName) && animator.HasState(0, Animator.StringToHash(stateName));
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
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - radius) / 3f);
                float cross = x > size / 2 - 1 && x < size / 2 + 2 || y > size / 2 - 1 && y < size / 2 + 2
                    ? 0.7f
                    : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(ring, cross)));
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
