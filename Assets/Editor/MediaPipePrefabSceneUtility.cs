using UnityEditor;
using UnityEngine;

public static class MediaPipePrefabSceneUtility
{
    private const string PrefabFolder = "Assets/Prefabs";
    private static readonly string[] PreferredPrefabPaths =
    {
        "Assets/Prefabs/MediaPipe.prefab",
        "Assets/Prefabs/Mediapipe.prefab",
        "Assets/Prefabs/MediaPipeObject.prefab",
        "Assets/Prefabs/MediaPipe_Object.prefab",
        "Assets/Prefabs/RehabTarget.prefab"
    };

    public static GameObject EnsureMediaPipeObject(string sceneObjectName)
    {
        GameObject existing = FindSceneObject(sceneObjectName);
        GameObject prefab = LoadMediaPipePrefab();
        if (prefab == null)
        {
            return existing != null ? existing : new GameObject(sceneObjectName);
        }

        if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing) == prefab)
        {
            existing.name = sceneObjectName;
            return existing;
        }

        Vector3 position = existing != null ? existing.transform.position : Vector3.zero;
        Quaternion rotation = existing != null ? existing.transform.rotation : Quaternion.identity;
        Vector3 scale = existing != null ? existing.transform.localScale : Vector3.one;

        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = sceneObjectName;
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = scale;
        return instance;
    }

    private static GameObject LoadMediaPipePrefab()
    {
        foreach (string path in PreferredPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                return prefab;
            }
        }

        string[] guids = AssetDatabase.FindAssets("MediaPipe t:Prefab", new[] { PrefabFolder });
        if (guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("Mediapipe t:Prefab", new[] { PrefabFolder });
        }
        if (guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("RehabTarget t:Prefab", new[] { PrefabFolder });
        }

        if (guids.Length == 0)
        {
            return null;
        }

        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    }

    private static GameObject FindSceneObject(string name)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Transform item in transforms)
        {
            if (item.name == name && item.gameObject.scene.IsValid())
            {
                return item.gameObject;
            }
        }

        return null;
    }
}
