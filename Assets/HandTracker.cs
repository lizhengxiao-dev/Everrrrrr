using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receives EverMotion UDP control messages and moves RehabTarget.
/// Preferred action format: "LEFT,x,y", "RIGHT,x,y", "UP,x,y", or "IDLE,x,y".
/// Precision format: "PINCH,x,y", "OPEN,x,y", or "MOVE,x,y".
/// Fallback format: "x,y" normalized coordinates in the range 0..1.
/// </summary>
public class HandTracker : MonoBehaviour
{
    [Header("UDP")]
    public int listenPort = 5052;

    [Header("Action World Targets")]
    public Vector2 idleWorldPosition = new Vector2(0f, 0f);
    public Vector2 leftWorldPosition = new Vector2(-4.5f, 0f);
    public Vector2 rightWorldPosition = new Vector2(4.5f, 0f);
    public Vector2 upWorldPosition = new Vector2(0f, 4f);

    [Header("Normalized Fallback Mapping")]
    public float worldMinX = -8f;
    public float worldMaxX = 8f;
    public float worldMinY = -5f;
    public float worldMaxY = 5f;
    public bool mirrorX = true;
    public bool invertY = true;

    [Header("Smoothing")]
    public float smoothSpeed = 18f;
    public float fixedZ = 0f;

    [Header("Debug")]
    public bool logReceivedMessages = true;
    [Tooltip("Seconds between live UDP debug lines in the Unity Console.")]
    public float debugLogInterval = 0.2f;
    public string currentActionDebug = "IDLE";
    public Vector2 currentWorldTargetDebug = Vector2.zero;
    public string currentRawMessageDebug = "";

    private readonly object latestLock = new object();
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning;
    private Vector2 latestWorldTarget;
    private string latestAction = "IDLE";
    private string latestRawMessage = "";
    private string latestRemoteEndpoint = "";
    private string latestParseWarning = "";
    private long latestReceivedUtcTicks;
    private float nextDebugLogTime;

    private void Awake()
    {
        latestWorldTarget = idleWorldPosition;
    }

    private void Start()
    {
        StartUdpReceiver();
    }

    private void Update()
    {
        Vector2 worldTarget;
        string actionForDebug;
        string rawForDebug;
        string endpointForDebug;
        string parseWarningForDebug;

        lock (latestLock)
        {
            worldTarget = latestWorldTarget;
            actionForDebug = latestAction;
            rawForDebug = latestRawMessage;
            endpointForDebug = latestRemoteEndpoint;
            parseWarningForDebug = latestParseWarning;
        }

        Vector3 targetPosition = new Vector3(worldTarget.x, worldTarget.y, fixedZ);
        float lerpAmount = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, lerpAmount);

        if (logReceivedMessages && Time.unscaledTime >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.unscaledTime + Mathf.Max(0.05f, debugLogInterval);

            if (!string.IsNullOrEmpty(parseWarningForDebug))
            {
                Debug.LogWarning(parseWarningForDebug);
            }
            else if (!string.IsNullOrEmpty(rawForDebug))
            {
                Debug.Log(
                    "[UDP RECEIVE] "
                    + endpointForDebug
                    + " | raw=" + rawForDebug
                    + " | action=" + actionForDebug
                    + " | worldTarget=" + worldTarget.ToString("F2")
                    + " | RehabTargetNow=" + transform.position.ToString("F2")
                );
            }
        }
    }

    private void StartUdpReceiver()
    {
        try
        {
            StopUdpReceiver();

            udpClient = new UdpClient(listenPort);
            isRunning = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log("HandTracker listening for EverMotion UDP on port " + listenPort);
        }
        catch (Exception e)
        {
            Debug.LogError("HandTracker failed to start UDP receiver: " + e.Message);
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string message = Encoding.UTF8.GetString(data);
                string trimmedMessage = message.Trim();

                if (TryParseMessage(message, out Vector2 worldTarget, out string parsedAction))
                {
                    lock (latestLock)
                    {
                        latestWorldTarget = worldTarget;
                        latestAction = parsedAction;
                        latestRawMessage = trimmedMessage;
                        latestRemoteEndpoint = remoteEndPoint.Address + ":" + remoteEndPoint.Port;
                        latestParseWarning = "";
                        latestReceivedUtcTicks = DateTime.UtcNow.Ticks;
                        currentActionDebug = parsedAction;
                        currentWorldTargetDebug = worldTarget;
                        currentRawMessageDebug = trimmedMessage;
                    }
                }
                else
                {
                    lock (latestLock)
                    {
                        latestRawMessage = trimmedMessage;
                        latestRemoteEndpoint = remoteEndPoint.Address + ":" + remoteEndPoint.Port;
                        latestParseWarning =
                        "[UDP RECEIVE - PARSE FAILED] " + latestRemoteEndpoint
                        + " | raw: " + trimmedMessage
                        + " | expected: PINCH,x,y / OPEN,x,y / LEFT,x,y / RIGHT,x,y / UP,x,y / IDLE,x,y";
                        currentRawMessageDebug = trimmedMessage;
                    }
                }
            }
            catch (SocketException)
            {
                if (isRunning)
                {
                    Debug.LogWarning("HandTracker UDP receive interrupted.");
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected when stopping Play mode.
            }
            catch (Exception e)
            {
                if (isRunning)
                {
                    Debug.LogWarning("HandTracker UDP receive failed: " + e.Message);
                }
            }
        }
    }

    public string GetLatestAction()
    {
        lock (latestLock)
        {
            return latestAction;
        }
    }

    public bool IsPinching()
    {
        lock (latestLock)
        {
            return latestAction == "PINCH";
        }
    }

    public bool HasRecentMessage(float maxAgeSeconds = 1f)
    {
        long receivedTicks;

        lock (latestLock)
        {
            receivedTicks = latestReceivedUtcTicks;
        }

        if (receivedTicks <= 0)
        {
            return false;
        }

        double ageSeconds = (DateTime.UtcNow.Ticks - receivedTicks) / (double)TimeSpan.TicksPerSecond;
        return ageSeconds <= Mathf.Max(0.05f, maxAgeSeconds);
    }

    private bool TryParseMessage(string message, out Vector2 worldTarget, out string parsedAction)
    {
        worldTarget = idleWorldPosition;
        parsedAction = "IDLE";

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string[] parts = message.Trim().Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        string action = parts[0].ToUpperInvariant();

        if (IsPrecisionAction(action) && parts.Length >= 3)
        {
            bool parsedPrecisionX = float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float precisionX);
            bool parsedPrecisionY = float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float precisionY);

            if (!parsedPrecisionX || !parsedPrecisionY)
            {
                return false;
            }

            worldTarget = MapNormalizedToWorld(Clamp01(precisionX), Clamp01(precisionY));
            parsedAction = action;
            return true;
        }

        if (TryGetActionTarget(action, out worldTarget))
        {
            parsedAction = action;
            return true;
        }

        if (parts.Length < 2)
        {
            return false;
        }

        bool parsedX = float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float normalizedX);
        bool parsedY = float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float normalizedY);

        if (!parsedX || !parsedY)
        {
            return false;
        }

        worldTarget = MapNormalizedToWorld(Clamp01(normalizedX), Clamp01(normalizedY));
        parsedAction = "COORDINATE";
        return true;
    }

    private static bool IsPrecisionAction(string action)
    {
        return action == "PINCH" || action == "OPEN" || action == "MOVE";
    }

    private bool TryGetActionTarget(string action, out Vector2 worldTarget)
    {
        switch (action)
        {
            case "LEFT":
                worldTarget = leftWorldPosition;
                return true;
            case "RIGHT":
                worldTarget = rightWorldPosition;
                return true;
            case "UP":
            case "ARMRAISE":
                worldTarget = upWorldPosition;
                return true;
            case "IDLE":
            case "CENTER":
                worldTarget = idleWorldPosition;
                return true;
            default:
                worldTarget = idleWorldPosition;
                return false;
        }
    }

    private Vector2 MapNormalizedToWorld(float normalizedX, float normalizedY)
    {
        if (mirrorX)
        {
            normalizedX = 1f - normalizedX;
        }

        if (invertY)
        {
            normalizedY = 1f - normalizedY;
        }

        return new Vector2(
            Mathf.Lerp(worldMinX, worldMaxX, normalizedX),
            Mathf.Lerp(worldMinY, worldMaxY, normalizedY)
        );
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
        {
            return 0f;
        }

        if (value > 1f)
        {
            return 1f;
        }

        return value;
    }

    private void OnDestroy()
    {
        StopUdpReceiver();
    }

    private void OnApplicationQuit()
    {
        StopUdpReceiver();
    }

    private void StopUdpReceiver()
    {
        isRunning = false;

        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(100);
        }

        receiveThread = null;
    }
}
