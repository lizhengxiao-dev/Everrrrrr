using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public class MicroNodeMediaPipeLauncher : MonoBehaviour
{
    public bool launchOnStart = true;
    public bool stopTrackerOnQuit = true;
    public bool openTerminalOnMac = true;
    public string scriptRelativePath = "Tools/MediaPipe/run_micro_node_tracker.command";

    private Process trackerProcess;

    private void Start()
    {
        if (launchOnStart)
        {
            LaunchTracker();
        }
    }

    public void LaunchTracker()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            UnityEngine.Debug.LogWarning("MicroNodeMediaPipeLauncher: could not find project root.");
            return;
        }

        string scriptPath = Path.Combine(projectRoot, scriptRelativePath);
        if (!File.Exists(scriptPath))
        {
            UnityEngine.Debug.LogWarning("MicroNodeMediaPipeLauncher: tracker script not found at " + scriptPath);
            return;
        }

        try
        {
            StopExistingTrackers();

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if (openTerminalOnMac)
            {
                trackerProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "/usr/bin/open",
                    Arguments = Quote(scriptPath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            else
#endif
            {
                trackerProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/zsh",
                    Arguments = Quote(scriptPath),
                    WorkingDirectory = Path.GetDirectoryName(scriptPath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }

            UnityEngine.Debug.Log("MicroNodeMediaPipeLauncher: launched " + scriptPath);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning("MicroNodeMediaPipeLauncher failed to launch tracker: " + exception.Message);
        }
    }

    private void OnApplicationQuit()
    {
        if (stopTrackerOnQuit)
        {
            StopExistingTrackers();
        }
    }

    private void OnDestroy()
    {
        if (stopTrackerOnQuit)
        {
            StopExistingTrackers();
        }
    }

    private static void StopExistingTrackers()
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/zsh",
                Arguments = "-lc \"pkill -f 'Tools/MediaPipe/.*micro_node_tracker.py' || true\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception)
        {
            // Best effort cleanup only.
        }
#endif
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
