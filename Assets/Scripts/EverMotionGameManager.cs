using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the official EverMotion session rules:
/// 120 second countdown, 12 perfect blocks to full energy, victory/failure state.
/// </summary>
public class EverMotionGameManager : MonoBehaviour
{
    public static EverMotionGameManager Instance { get; private set; }

    [Header("Official 120s / 12 Blocks Rules")]
    public float timeRemaining = 120f;
    public int blocksCaught = 0;
    public int targetBlocks = 12;
    public bool isGameOver = false;

    [Header("Compatibility Settings")]
    [Tooltip("Legacy name kept so old Inspector bindings still work.")]
    public float sessionDuration = 120f;

    [Tooltip("Legacy name kept so old Inspector bindings still work.")]
    public int requiredPerfectBlocks = 12;

    [Header("HUD References")]
    public Text timerText;

    [Tooltip("Preferred counter text: displays 0 / 12.")]
    public Text counterText;

    [Tooltip("Legacy counter reference from earlier phases.")]
    public Text targetCounterText;

    [Tooltip("Preferred energy fill Image. Set Image Type to Filled if using this.")]
    public Image energyBarFill;

    [Tooltip("Legacy Slider energy bar from the generated HUD.")]
    public Slider energyBar;

    [Header("End State Panels")]
    public GameObject gameClearPanel;
    public GameObject gameOverPanel;

    [Header("Systems")]
    public LaserSpawner laserSpawner;

    [Header("Debug / Testing")]
    public KeyCode restartKey = KeyCode.R;

    private bool sessionEnded;
    private Coroutine scorePopRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SyncRuleAliases();
        AutoBindMissingReferences();
    }

    private void Start()
    {
        isGameOver = false;
        sessionEnded = false;
        timeRemaining = sessionDuration;
        blocksCaught = 0;

        EverMotionUIPolisher.ApplyPolish();
        EnsureEndPanelRestartButtons();
        SetEndPanelVisible(gameClearPanel, false);
        SetEndPanelVisible(gameOverPanel, false);
        UpdateHud();
    }

    private void Update()
    {
        if (sessionEnded)
        {
            if (WasRestartPressed())
            {
                RestartSession();
            }

            return;
        }

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateHud();

        if (timeRemaining <= 0f && blocksCaught < targetBlocks)
        {
            TriggerGameOver();
        }
    }

    /// <summary>
    /// Official scoring function requested for Phase 4.
    /// Called when a laser is blocked with the correct pose.
    /// </summary>
    public void AddBlock()
    {
        if (sessionEnded)
        {
            return;
        }

        blocksCaught = Mathf.Clamp(blocksCaught + 1, 0, targetBlocks);
        UpdateHud();
        PlayScorePop();
        Debug.Log("Energy charged: " + blocksCaught + " / " + targetBlocks);

        if (blocksCaught >= targetBlocks)
        {
            TriggerGameClear();
        }
    }

    public void RegisterPerfectBlock()
    {
        AddBlock();
    }

    public void RegisterBodyHit()
    {
        if (sessionEnded)
        {
            return;
        }

        Debug.Log("Body hit registered. Energy does not charge.");
    }

    public void RestartSession()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    private void TriggerGameClear()
    {
        if (sessionEnded)
        {
            return;
        }

        sessionEnded = true;
        isGameOver = true;
        StopLaserSpawner();
        ClearActiveLasers();
        SetEndPanelVisible(gameClearPanel, true);
        SetEndPanelVisible(gameOverPanel, false);
        Debug.Log("Game Clear! Energy fully charged. Press R to restart.");
    }

    private void TriggerGameOver()
    {
        if (sessionEnded)
        {
            return;
        }

        sessionEnded = true;
        isGameOver = true;
        StopLaserSpawner();
        ClearActiveLasers();
        SetEndPanelVisible(gameClearPanel, false);
        SetEndPanelVisible(gameOverPanel, true);
        Debug.Log("Game Over! Time ran out before energy reached 100%. Press R to restart.");
    }

    private void StopLaserSpawner()
    {
        if (laserSpawner != null)
        {
            laserSpawner.StopSpawning();
            laserSpawner.enabled = false;
        }
    }

    private void UpdateHud()
    {
        if (timerText != null)
        {
            int totalSeconds = Mathf.CeilToInt(timeRemaining);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        Text counter = counterText != null ? counterText : targetCounterText;
        if (counter != null)
        {
            counter.text = blocksCaught + " / " + targetBlocks;
        }

        float fill01 = targetBlocks <= 0 ? 0f : blocksCaught / (float)targetBlocks;

        if (energyBarFill != null)
        {
            energyBarFill.fillAmount = fill01;
        }

        if (energyBar != null)
        {
            energyBar.minValue = 0f;
            energyBar.maxValue = 100f;
            energyBar.value = fill01 * 100f;
        }
    }

    private void PlayScorePop()
    {
        Text scoreText = counterText != null ? counterText : targetCounterText;
        if (scoreText == null)
        {
            return;
        }

        if (scorePopRoutine != null)
        {
            StopCoroutine(scorePopRoutine);
        }

        scorePopRoutine = StartCoroutine(AnimateTextPop(scoreText.transform));
    }

    public IEnumerator AnimateTextPop(Transform targetText)
    {
        if (targetText == null)
        {
            yield break;
        }

        Vector3 normalScale = Vector3.one;
        Vector3 peakScale = Vector3.one * 1.4f;

        float elapsed = 0f;
        while (elapsed < 0.1f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.1f);
            targetText.localScale = Vector3.Lerp(normalScale, peakScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.2f));
            targetText.localScale = Vector3.Lerp(peakScale, normalScale, t);
            yield return null;
        }

        targetText.localScale = normalScale;
        scorePopRoutine = null;
    }

    private void SetEndPanelVisible(GameObject panel, bool visible)
    {
        if (panel != null)
        {
            panel.SetActive(visible);
        }
    }

    private void EnsureEndPanelRestartButtons()
    {
        EnsureRestartButton(gameClearPanel);
        EnsureRestartButton(gameOverPanel);
    }

    private void EnsureRestartButton(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        Transform existing = panel.transform.Find("Button_Restart");
        Button button;

        if (existing != null)
        {
            button = existing.GetComponent<Button>();
            if (button == null)
            {
                button = existing.gameObject.AddComponent<Button>();
            }
        }
        else
        {
            GameObject buttonObject = new GameObject("Button_Restart", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panel.transform, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -86f);
            rect.sizeDelta = new Vector2(230f, 50f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color32(0, 220, 230, 210);

            button = buttonObject.GetComponent<Button>();

            GameObject textObject = new GameObject("Text_Restart", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text label = textObject.GetComponent<Text>();
            label.text = "RESTART";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color32(5, 15, 28, 255);
        }

        button.onClick.RemoveListener(RestartSession);
        button.onClick.AddListener(RestartSession);
    }

    private void ClearActiveLasers()
    {
        LaserBehavior[] activeLasers = FindObjectsByType<LaserBehavior>(FindObjectsSortMode.None);
        foreach (LaserBehavior laser in activeLasers)
        {
            Destroy(laser.gameObject);
        }
    }

    private bool WasRestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(restartKey);
#endif
    }

    private void SyncRuleAliases()
    {
        sessionDuration = timeRemaining > 0f ? timeRemaining : 120f;
        requiredPerfectBlocks = targetBlocks > 0 ? targetBlocks : 12;
        timeRemaining = sessionDuration;
        targetBlocks = requiredPerfectBlocks;
    }

    private void AutoBindMissingReferences()
    {
        if (laserSpawner == null)
        {
            laserSpawner = FindFirstObjectByType<LaserSpawner>();
        }

        if (timerText == null)
        {
            timerText = FindTextByName("Text_Timer");
        }

        if (counterText == null && targetCounterText == null)
        {
            targetCounterText = FindTextByName("Text_TargetCounter");
        }

        if (gameClearPanel == null)
        {
            gameClearPanel = GameObject.Find("Panel_GameClear");
        }

        if (gameOverPanel == null)
        {
            gameOverPanel = GameObject.Find("Panel_GameOver");
        }
    }

    private Text FindTextByName(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.GetComponent<Text>() : null;
    }
}
