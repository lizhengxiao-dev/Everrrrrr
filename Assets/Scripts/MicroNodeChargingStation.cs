using System.Collections;
using UnityEngine;

public class MicroNodeChargingStation : MonoBehaviour
{
    public Transform robotTarget;
    public Vector3 stationOffset = new Vector3(3.15f, -0.65f, -0.05f);
    public Vector3 robotSocketOffset = new Vector3(1.05f, 0.25f, -0.05f);
    public int sortingOrder = 155;

    private Transform stationRoot;
    private Transform chargeFill;
    private SpriteRenderer chargeFillRenderer;
    private SpriteRenderer statusLightRenderer;
    private readonly SpriteRenderer[] stageRenderers = new SpriteRenderer[3];
    private LineRenderer cable;
    private Coroutine burstRoutine;
    private float displayedCharge;
    private float targetCharge;
    private static Sprite squareSprite;
    private static Sprite glowSprite;
    private static Material lineMaterial;

    private void Awake()
    {
        EnsureVisuals();
        SetCharge01(0f, true);
    }

    private void Update()
    {
        if (robotTarget == null || stationRoot == null)
        {
            return;
        }

        stationRoot.position = robotTarget.position + stationOffset;
        UpdateCable();

        displayedCharge = Mathf.MoveTowards(displayedCharge, targetCharge, Time.deltaTime * 0.42f);
        ApplyChargeVisuals(displayedCharge);

        if (statusLightRenderer != null)
        {
            float pulse = 0.72f + Mathf.Sin(Time.time * 5.5f) * 0.18f;
            Color color = GetChargeColor(displayedCharge);
            color.a = pulse;
            statusLightRenderer.color = color;
        }
    }

    public void SetRobotTarget(Transform target)
    {
        robotTarget = target;
        if (stationRoot != null)
        {
            stationRoot.gameObject.SetActive(target != null);
        }
        if (cable != null)
        {
            cable.enabled = target != null;
        }
    }

    public void SetCharge01(float charge01, bool immediate = false)
    {
        targetCharge = Mathf.Clamp01(charge01);
        if (immediate)
        {
            displayedCharge = targetCharge;
            ApplyChargeVisuals(displayedCharge);
        }
    }

    public void PulseFromNode(Vector3 nodeWorldPosition)
    {
        EnsureVisuals();
        if (burstRoutine != null)
        {
            StopCoroutine(burstRoutine);
        }

        burstRoutine = StartCoroutine(BurstRoutine(nodeWorldPosition));
    }

    private IEnumerator BurstRoutine(Vector3 start)
    {
        GameObject burst = new GameObject("ChargingStation_EnergyPacket");
        SpriteRenderer renderer = burst.AddComponent<SpriteRenderer>();
        renderer.sprite = GetGlowSprite();
        renderer.color = new Color(0.35f, 1f, 0.95f, 0.95f);
        renderer.sortingOrder = sortingOrder + 5;
        burst.transform.position = start;
        burst.transform.localScale = Vector3.one * 0.72f;

        float elapsed = 0f;
        const float duration = 0.34f;
        while (elapsed < duration && stationRoot != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            Vector3 end = stationRoot.position + new Vector3(0f, 0.45f, 0f);
            burst.transform.position = Vector3.Lerp(start, end, eased);
            burst.transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 0.28f, t);
            yield return null;
        }

        Destroy(burst);
        burstRoutine = null;
    }

    private void EnsureVisuals()
    {
        if (stationRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("MicroNode_ChargingStation");
        stationRoot = root.transform;

        CreateBlock(stationRoot, "StationShadow", new Vector3(0.08f, -0.05f, 0.03f), new Vector3(1.22f, 2.72f, 1f), new Color(0f, 0.05f, 0.08f, 0.72f), sortingOrder);
        CreateBlock(stationRoot, "StationBody", Vector3.zero, new Vector3(1.14f, 2.62f, 1f), new Color(0.035f, 0.14f, 0.21f, 0.98f), sortingOrder + 1);
        CreateBlock(stationRoot, "StationPanel", new Vector3(0f, 0.12f, -0.01f), new Vector3(0.72f, 1.7f, 1f), new Color(0.015f, 0.055f, 0.08f, 1f), sortingOrder + 2);

        GameObject fillObject = CreateBlock(
            stationRoot,
            "ChargeFill",
            new Vector3(0f, -0.69f, -0.02f),
            new Vector3(0.52f, 0.01f, 1f),
            GetChargeColor(0f),
            sortingOrder + 3
        );
        chargeFill = fillObject.transform;
        chargeFillRenderer = fillObject.GetComponent<SpriteRenderer>();

        GameObject lightObject = CreateBlock(
            stationRoot,
            "StatusLight",
            new Vector3(0f, 1.02f, -0.02f),
            new Vector3(0.42f, 0.16f, 1f),
            GetChargeColor(0f),
            sortingOrder + 4
        );
        statusLightRenderer = lightObject.GetComponent<SpriteRenderer>();

        for (int i = 0; i < 3; i++)
        {
            Color color = i == 0
                ? new Color(1f, 0.18f, 0.25f, 0.9f)
                : i == 1
                    ? new Color(1f, 0.78f, 0.1f, 0.35f)
                    : new Color(0.2f, 1f, 0.48f, 0.25f);
            GameObject stage = CreateBlock(
                stationRoot,
                "ChargeStage_" + i,
                new Vector3(-0.28f + i * 0.28f, -1.06f, -0.02f),
                new Vector3(0.16f, 0.08f, 1f),
                color,
                sortingOrder + 4
            );
            stageRenderers[i] = stage.GetComponent<SpriteRenderer>();
        }

        CreateBlock(stationRoot, "PlugHead", new Vector3(-0.72f, 0.55f, -0.02f), new Vector3(0.34f, 0.48f, 1f), new Color(0.12f, 0.88f, 1f, 1f), sortingOrder + 3);

        GameObject cableObject = new GameObject("ChargingCable");
        cable = cableObject.AddComponent<LineRenderer>();
        cable.positionCount = 4;
        cable.widthMultiplier = 0.08f;
        cable.numCornerVertices = 5;
        cable.numCapVertices = 5;
        cable.sortingOrder = sortingOrder + 2;
        cable.startColor = new Color(0.05f, 0.35f, 0.45f, 1f);
        cable.endColor = new Color(0.18f, 0.95f, 1f, 1f);
        cable.sharedMaterial = GetLineMaterial();
        cable.enabled = robotTarget != null;

        if (robotTarget == null)
        {
            root.SetActive(false);
        }
    }

    private void UpdateCable()
    {
        if (cable == null || robotTarget == null)
        {
            return;
        }

        Vector3 stationPort = stationRoot.position + new Vector3(-0.62f, 0.55f, 0f);
        Vector3 robotSocket = robotTarget.position + robotSocketOffset;
        float middleX = Mathf.Lerp(stationPort.x, robotSocket.x, 0.55f);
        cable.SetPosition(0, stationPort);
        cable.SetPosition(1, new Vector3(middleX, stationPort.y, stationPort.z));
        cable.SetPosition(2, new Vector3(middleX, robotSocket.y, robotSocket.z));
        cable.SetPosition(3, robotSocket);
    }

    private void ApplyChargeVisuals(float charge01)
    {
        if (chargeFill == null || chargeFillRenderer == null)
        {
            return;
        }

        float height = Mathf.Lerp(0.02f, 1.58f, charge01);
        chargeFill.localScale = new Vector3(0.52f, height, 1f);
        chargeFill.localPosition = new Vector3(0f, -0.69f + height * 0.5f, -0.02f);
        chargeFillRenderer.color = GetChargeColor(charge01);

        Color[] stageColors =
        {
            new Color(1f, 0.18f, 0.25f, charge01 < 0.34f ? 1f : 0.32f),
            new Color(1f, 0.78f, 0.1f, charge01 >= 0.34f && charge01 < 0.8f ? 1f : 0.28f),
            new Color(0.2f, 1f, 0.48f, charge01 >= 0.8f ? 1f : 0.24f)
        };
        for (int i = 0; i < stageRenderers.Length; i++)
        {
            if (stageRenderers[i] != null)
            {
                stageRenderers[i].color = stageColors[i];
            }
        }
    }

    private static Color GetChargeColor(float value)
    {
        if (value < 0.5f)
        {
            return Color.Lerp(new Color(1f, 0.12f, 0.2f, 0.95f), new Color(1f, 0.78f, 0.08f, 0.95f), value / 0.5f);
        }

        return Color.Lerp(new Color(1f, 0.78f, 0.08f, 0.95f), new Color(0.16f, 1f, 0.42f, 0.98f), (value - 0.5f) / 0.5f);
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

    private static Sprite GetSquareSprite()
    {
        if (squareSprite == null)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
            squareSprite.name = "Runtime_ChargingBlock";
        }

        return squareSprite;
    }

    private static Sprite GetGlowSprite()
    {
        if (glowSprite == null)
        {
            const int size = 48;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2f);
                    texture.SetPixel(x, y, new Color(0.55f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            glowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            glowSprite.name = "Runtime_ChargingGlow";
        }

        return glowSprite;
    }

    private static Material GetLineMaterial()
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lineMaterial = new Material(shader)
                {
                    name = "Runtime_ChargingCable"
                };
            }
        }

        return lineMaterial;
    }
}
