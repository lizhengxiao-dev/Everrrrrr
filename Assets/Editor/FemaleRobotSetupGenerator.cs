using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FemaleRobotSetupGenerator
{
    private const string FemaleRoot = "Assets/FemaleRobot";
    private const string ControllerPath = FemaleRoot + "/Female0001.controller";
    private const string IdleClipPath = FemaleRoot + "/Female_Idle_Anim.anim";
    private const string LeftClipPath = FemaleRoot + "/Female_PointLeft_Anim.anim";
    private const string RightClipPath = FemaleRoot + "/Female_PointRight_Anim.anim";
    private const string UpClipPath = FemaleRoot + "/Female_ArmRaise_Anim.anim";

    private static readonly Vector3 GameplayRobotPosition = new Vector3(0.1f, 0.05f, 0f);
    private static readonly Vector3 GameplayRobotScale = new Vector3(0.7f, 0.435f, 1f);

    [MenuItem("EverMotion/Female Robot - Build And Install")]
    public static void BuildAndInstall()
    {
        EnsureFolder(FemaleRoot);

        AnimationClip idleClip = CreateSpriteAnimation(
            IdleClipPath,
            "Female_Idle_Anim",
            12f,
            true,
            GetSprites(FemaleRoot + "/Female_Idle_Sprites")
        );

        AnimationClip leftClip = CreateSpriteAnimation(
            LeftClipPath,
            "Female_PointLeft_Anim",
            60f,
            false,
            GetSprites(
                FemaleRoot + "/Female_PointingLeft_Sprites/1. Opening",
                FemaleRoot + "/Female_PointingLeft_Sprites/2. Main (looping)"
            )
        );

        AnimationClip rightClip = CreateSpriteAnimation(
            RightClipPath,
            "Female_PointRight_Anim",
            60f,
            false,
            GetSprites(
                FemaleRoot + "/Female_PointingRight_Sprites/1. Opening",
                FemaleRoot + "/Female_PointingRight_Sprites/2. Main"
            )
        );

        AnimationClip upClip = CreateSpriteAnimation(
            UpClipPath,
            "Female_ArmRaise_Anim",
            60f,
            false,
            GetSprites(
                FemaleRoot + "/Female_ArmRaise_Sprites/1. Opening",
                FemaleRoot + "/Female_ArmRaise_Sprites/2. Main (looping)"
            )
        );

        AnimatorController controller = CreateController(idleClip, leftClip, rightClip, upClip);
        InstallFemaleRobot(controller, idleClip);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Female robot generated and installed: Female0001 is now the gameplay robot.");
    }

    [MenuItem("EverMotion/Robot Select - Use Female")]
    public static void UseFemaleRobot()
    {
        SetActiveRobot("Female0001", "Male0001");
    }

    [MenuItem("EverMotion/Robot Select - Use Male")]
    public static void UseMaleRobot()
    {
        SetActiveRobot("Male0001", "Female0001");
    }

    private static AnimationClip CreateSpriteAnimation(string assetPath, string clipName, float frameRate, bool loop, List<Sprite> sprites)
    {
        if (sprites.Count == 0)
        {
            throw new InvalidOperationException("No sprites found for " + clipName);
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, assetPath);
        }
        else
        {
            clip.ClearCurves();
        }

        clip.name = clipName;
        clip.frameRate = frameRate;

        ObjectReferenceKeyframe[] frames = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            frames[i] = new ObjectReferenceKeyframe
            {
                time = i / frameRate,
                value = sprites[i]
            };
        }

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, frames);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateController(AnimationClip idleClip, AnimationClip leftClip, AnimationClip rightClip, AnimationClip upClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
        {
            controller.RemoveParameter(parameter);
        }
        controller.AddParameter("DefenseDirection", AnimatorControllerParameterType.Int);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        AnimatorState idle = AddState(stateMachine, "Female_Idle_Anim", idleClip, new Vector3(200f, 0f, 0f));
        AnimatorState left = AddState(stateMachine, "Female_PointLeft_Anim", leftClip, new Vector3(80f, 120f, 0f));
        AnimatorState right = AddState(stateMachine, "Female_PointRight_Anim", rightClip, new Vector3(320f, 120f, 0f));
        AnimatorState up = AddState(stateMachine, "Female_ArmRaise_Anim", upClip, new Vector3(200f, 180f, 0f));
        stateMachine.defaultState = idle;

        AddTransition(idle, left, 1);
        AddTransition(idle, right, 2);
        AddTransition(idle, up, 3);
        AddTransition(left, idle, 0);
        AddTransition(right, idle, 0);
        AddTransition(up, idle, 0);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = stateMachine.AddState(stateName, position);
        state.motion = clip;
        state.writeDefaultValues = true;
        return state;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, int direction)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.AddCondition(AnimatorConditionMode.Equals, direction, "DefenseDirection");
    }

    private static void InstallFemaleRobot(AnimatorController controller, AnimationClip idleClip)
    {
        GameObject rehabTarget = FindSceneObjectIncludingInactive("RehabTarget");
        GameObject maleRobot = FindSceneObjectIncludingInactive("Male0001");
        GameObject femaleRobot = FindSceneObjectIncludingInactive("Female0001");

        if (femaleRobot != null)
        {
            UnityEngine.Object.DestroyImmediate(femaleRobot);
        }

        femaleRobot = new GameObject("Female0001");
        femaleRobot.tag = "Player";
        femaleRobot.transform.position = GameplayRobotPosition;
        femaleRobot.transform.localScale = GameplayRobotScale;

        SpriteRenderer renderer = femaleRobot.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSprites(FemaleRoot + "/Female_Idle_Sprites").FirstOrDefault();
        renderer.sortingOrder = 0;

        Animator animator = femaleRobot.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        RobotAnimationController robotController = femaleRobot.AddComponent<RobotAnimationController>();
        robotController.playerHandTarget = rehabTarget != null ? rehabTarget.transform : null;
        robotController.topThreshold = 1.5f;
        robotController.sideThreshold = 2f;
        robotController.holdPoseTime = 0.5f;
        robotController.idleStateName = "Female_Idle_Anim";
        robotController.leftStateName = "Female_PointLeft_Anim";
        robotController.rightStateName = "Female_PointRight_Anim";
        robotController.upStateName = "Female_ArmRaise_Anim";

        ShieldManager shieldManager = femaleRobot.AddComponent<ShieldManager>();
        shieldManager.defenseDirectionParameter = "DefenseDirection";

        CapsuleCollider2D collider = femaleRobot.AddComponent<CapsuleCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.25f, 2.15f);
        collider.offset = new Vector2(0f, 0.45f);

        if (maleRobot != null)
        {
            maleRobot.SetActive(false);
        }

        RebindGameplayReferences(femaleRobot.transform);

        EditorUtility.SetDirty(femaleRobot);
        if (idleClip != null)
        {
            EditorUtility.SetDirty(idleClip);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static void RebindGameplayReferences(Transform robotTarget)
    {
        GameObject gameManagerObject = FindSceneObjectIncludingInactive("GameManager");
        if (gameManagerObject == null)
        {
            return;
        }

        LaserSpawner laserSpawner = gameManagerObject.GetComponent<LaserSpawner>();
        if (laserSpawner != null)
        {
            laserSpawner.robotTarget = robotTarget;
            EditorUtility.SetDirty(laserSpawner);
        }

        EverMotionGameManager gameManager = gameManagerObject.GetComponent<EverMotionGameManager>();
        if (gameManager != null && laserSpawner != null)
        {
            gameManager.laserSpawner = laserSpawner;
            EditorUtility.SetDirty(gameManager);
        }
    }

    private static void SetActiveRobot(string activeRobotName, string inactiveRobotName)
    {
        GameObject activeRobot = FindSceneObjectIncludingInactive(activeRobotName);
        GameObject inactiveRobot = FindSceneObjectIncludingInactive(inactiveRobotName);

        if (activeRobot == null)
        {
            Debug.LogError("Could not find " + activeRobotName + " in the scene.");
            return;
        }

        activeRobot.SetActive(true);
        activeRobot.tag = "Player";

        if (inactiveRobot != null)
        {
            inactiveRobot.SetActive(false);
        }

        RobotAnimationController controller = activeRobot.GetComponent<RobotAnimationController>();
        if (controller != null)
        {
            GameObject rehabTarget = FindSceneObjectIncludingInactive("RehabTarget");
            controller.playerHandTarget = rehabTarget != null ? rehabTarget.transform : null;

            bool isFemale = activeRobotName.StartsWith("Female", StringComparison.OrdinalIgnoreCase);
            controller.idleStateName = isFemale ? "Female_Idle_Anim" : "Male_Idle_Anim";
            controller.leftStateName = isFemale ? "Female_PointLeft_Anim" : "Male_PointLeft_Anim";
            controller.rightStateName = isFemale ? "Female_PointRight_Anim" : "Male_PointRight_Anim";
            controller.upStateName = isFemale ? "Female_ArmRaise_Anim" : "Male_ArmRaise_Anim";
            EditorUtility.SetDirty(controller);
        }

        RebindGameplayReferences(activeRobot.transform);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Active gameplay robot is now " + activeRobotName + ".");
    }

    private static GameObject FindSceneObjectIncludingInactive(string objectName)
    {
        GameObject[] sceneObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }

    private static List<Sprite> GetSprites(params string[] folders)
    {
        List<Sprite> sprites = new List<Sprite>();

        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("Missing sprite folder: " + folder);
                continue;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            IEnumerable<string> paths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(GetTrailingNumber)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                EnsureSpriteImporter(path);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }
        }

        return sprites;
    }

    private static int GetTrailingNumber(string path)
    {
        Match match = Regex.Match(Path.GetFileNameWithoutExtension(path), @"(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
    }

    private static void EnsureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || importer.textureType == TextureImporterType.Sprite)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
        string name = Path.GetFileName(folder);
        AssetDatabase.CreateFolder(parent, name);
    }
}
