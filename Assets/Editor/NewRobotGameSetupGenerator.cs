using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NewRobotGameSetupGenerator
{
    private const string Root = "Assets/NewRobotGame";
    private const string FemaleName = "Female0001";
    private const string MaleName = "Male0001";
    private const string DefenseParameter = "DefenseDirection";

    private static readonly Vector3 GameplayRobotPosition = new Vector3(0.1f, 0.05f, 0f);
    private static readonly Vector3 GameplayRobotScale = new Vector3(0.7f, 0.435f, 1f);

    [MenuItem("EverMotion/New Two-Action Game - Build And Install Female")]
    public static void BuildFemaleGame()
    {
        BuildAndInstall("Female", FemaleName, MaleName, true);
    }

    [MenuItem("EverMotion/New Two-Action Game - Build And Install Male")]
    public static void BuildMaleGame()
    {
        BuildAndInstall("Male", MaleName, FemaleName, true);
    }

    [MenuItem("EverMotion/New Two-Action Game - Use Female")]
    public static void UseFemale()
    {
        SetActiveRobot(FemaleName, MaleName);
    }

    [MenuItem("EverMotion/New Two-Action Game - Use Male")]
    public static void UseMale()
    {
        SetActiveRobot(MaleName, FemaleName);
    }

    private static void BuildAndInstall(string gender, string activeRobotName, string inactiveRobotName, bool makeActive)
    {
        string genderRoot = Root + "/" + gender;
        string idleState = gender + "_Idle";
        string pushState = gender + "_push";
        string leftPushState = gender + "_push_left";
        string poweredUpState = gender + "_PoweredUp";

        EnsureAllPngsAreSprites(genderRoot);

        AnimationClip idleClip = CreateSpriteAnimation(
            Root + "/" + idleState + ".anim",
            idleState,
            12f,
            true,
            false,
            GetSprites(genderRoot + "/Idle")
        );

        AnimationClip pushClip = CreateSpriteAnimation(
            Root + "/" + pushState + ".anim",
            pushState,
            60f,
            false,
            false,
            GetSprites(genderRoot + "/Push")
        );

        AnimationClip leftPushClip = CreateSpriteAnimation(
            Root + "/" + leftPushState + ".anim",
            leftPushState,
            60f,
            false,
            true,
            GetSprites(genderRoot + "/Push")
        );

        AnimationClip poweredUpClip = CreateSpriteAnimation(
            Root + "/" + poweredUpState + ".anim",
            poweredUpState,
            60f,
            false,
            false,
            GetSprites(genderRoot + "/PoweredUp")
        );

        AnimatorController controller = CreateController(
            Root + "/" + gender + "_TwoAction.controller",
            idleClip,
            leftPushClip,
            pushClip,
            poweredUpClip,
            idleState,
            leftPushState,
            pushState,
            poweredUpState
        );

        GameObject robot = CreateOrUpdateRobot(activeRobotName, gender, controller, idleState, leftPushState, pushState, poweredUpState);
        GameObject inactiveRobot = FindSceneObjectIncludingInactive(inactiveRobotName);

        if (makeActive)
        {
            robot.SetActive(true);
            if (inactiveRobot != null)
            {
                inactiveRobot.SetActive(false);
            }
            RebindGameplayReferences(robot.transform);
        }

        ConfigureTwoActionLevel();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("New push game installed for " + gender + ": LEFT uses mirrored Push, RIGHT uses Push, UP uses PoweredUp.");
    }

    private static GameObject CreateOrUpdateRobot(
        string robotName,
        string gender,
        AnimatorController controller,
        string idleState,
        string leftPushState,
        string pushState,
        string poweredUpState)
    {
        GameObject robot = FindSceneObjectIncludingInactive(robotName);
        if (robot == null)
        {
            robot = new GameObject(robotName);
        }

        robot.tag = "Player";
        robot.transform.position = GameplayRobotPosition;
        robot.transform.localScale = GameplayRobotScale;

        SpriteRenderer renderer = robot.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = robot.AddComponent<SpriteRenderer>();
        }
        renderer.sprite = GetSprites(Root + "/" + gender + "/Idle").FirstOrDefault();
        renderer.sortingOrder = 0;

        Animator animator = robot.GetComponent<Animator>();
        if (animator == null)
        {
            animator = robot.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = controller;
        animator.speed = 1f;

        RobotAnimationController robotController = robot.GetComponent<RobotAnimationController>();
        if (robotController == null)
        {
            robotController = robot.AddComponent<RobotAnimationController>();
        }
        GameObject rehabTarget = FindSceneObjectIncludingInactive("RehabTarget");
        robotController.playerHandTarget = rehabTarget != null ? rehabTarget.transform : null;
        robotController.topThreshold = 1.5f;
        robotController.sideThreshold = 2f;
        robotController.holdPoseTime = 0.5f;
        robotController.idleStateName = idleState;
        robotController.leftStateName = leftPushState;
        robotController.rightStateName = pushState;
        robotController.upStateName = poweredUpState;
        robotController.enableLeftAction = true;
        robotController.enableRightAction = true;
        robotController.enableUpAction = true;

        ShieldManager shieldManager = robot.GetComponent<ShieldManager>();
        if (shieldManager == null)
        {
            shieldManager = robot.AddComponent<ShieldManager>();
        }
        shieldManager.defenseDirectionParameter = DefenseParameter;

        CapsuleCollider2D collider = robot.GetComponent<CapsuleCollider2D>();
        if (collider == null)
        {
            collider = robot.AddComponent<CapsuleCollider2D>();
        }
        collider.isTrigger = true;
        collider.size = new Vector2(1.25f, 2.15f);
        collider.offset = new Vector2(0f, 0.45f);

        EditorUtility.SetDirty(robot);
        return robot;
    }

    private static AnimatorController CreateController(
        string controllerPath,
        AnimationClip idleClip,
        AnimationClip leftPushClip,
        AnimationClip pushClip,
        AnimationClip poweredUpClip,
        string idleStateName,
        string leftPushStateName,
        string pushStateName,
        string poweredUpStateName)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
        {
            controller.RemoveParameter(parameter);
        }
        controller.AddParameter(DefenseParameter, AnimatorControllerParameterType.Int);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        AnimatorState idle = AddState(stateMachine, idleStateName, idleClip, new Vector3(200f, 0f, 0f));
        AnimatorState leftPush = AddState(stateMachine, leftPushStateName, leftPushClip, new Vector3(80f, 120f, 0f));
        AnimatorState push = AddState(stateMachine, pushStateName, pushClip, new Vector3(320f, 120f, 0f));
        AnimatorState poweredUp = AddState(stateMachine, poweredUpStateName, poweredUpClip, new Vector3(200f, 180f, 0f));
        stateMachine.defaultState = idle;

        AddTransition(idle, leftPush, 1);
        AddTransition(idle, push, 2);
        AddTransition(idle, poweredUp, 3);
        AddTransition(leftPush, idle, 0);
        AddTransition(push, idle, 0);
        AddTransition(poweredUp, idle, 0);

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
        transition.AddCondition(AnimatorConditionMode.Equals, direction, DefenseParameter);
    }

    private static AnimationClip CreateSpriteAnimation(string assetPath, string clipName, float frameRate, bool loop, bool flipX, List<Sprite> sprites)
    {
        if (sprites.Count == 0)
        {
            throw new InvalidOperationException("No sprites found for " + clipName + ". Check the folder names under " + Root + ".");
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

        EditorCurveBinding flipBinding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_FlipX"
        };

        float endTime = Mathf.Max(0f, (sprites.Count - 1) / frameRate);
        AnimationUtility.SetEditorCurve(clip, flipBinding, AnimationCurve.Constant(0f, endTime, flipX ? 1f : 0f));

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void ConfigureTwoActionLevel()
    {
        GameObject leftCannon = FindSceneObjectIncludingInactive("Cannon_Left");
        if (leftCannon != null)
        {
            leftCannon.SetActive(true);
        }

        GameObject rightCannon = FindSceneObjectIncludingInactive("Cannon_Right");
        if (rightCannon != null)
        {
            rightCannon.SetActive(true);
        }

        GameObject topCannon = FindSceneObjectIncludingInactive("Cannon_Top");
        if (topCannon != null)
        {
            topCannon.SetActive(true);
        }

        GameObject gameManagerObject = FindSceneObjectIncludingInactive("GameManager");
        if (gameManagerObject == null)
        {
            return;
        }

        LaserSpawner spawner = gameManagerObject.GetComponent<LaserSpawner>();
        if (spawner != null)
        {
            spawner.useLeftCannon = true;
            spawner.useRightCannon = true;
            spawner.useTopCannon = true;
            EditorUtility.SetDirty(spawner);
        }
    }

    private static void RebindGameplayReferences(Transform robotTarget)
    {
        GameObject gameManagerObject = FindSceneObjectIncludingInactive("GameManager");
        if (gameManagerObject == null)
        {
            return;
        }

        LaserSpawner spawner = gameManagerObject.GetComponent<LaserSpawner>();
        if (spawner != null)
        {
            spawner.robotTarget = robotTarget;
            EditorUtility.SetDirty(spawner);
        }

        EverMotionGameManager gameManager = gameManagerObject.GetComponent<EverMotionGameManager>();
        if (gameManager != null && spawner != null)
        {
            gameManager.laserSpawner = spawner;
            EditorUtility.SetDirty(gameManager);
        }
    }

    private static void SetActiveRobot(string activeRobotName, string inactiveRobotName)
    {
        GameObject activeRobot = FindSceneObjectIncludingInactive(activeRobotName);
        if (activeRobot == null)
        {
            Debug.LogError("Build that robot first: " + activeRobotName);
            return;
        }

        GameObject inactiveRobot = FindSceneObjectIncludingInactive(inactiveRobotName);
        activeRobot.SetActive(true);
        activeRobot.tag = "Player";

        if (inactiveRobot != null)
        {
            inactiveRobot.SetActive(false);
        }

        RebindGameplayReferences(activeRobot.transform);
        ConfigureTwoActionLevel();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Active two-action robot is now " + activeRobotName + ".");
    }

    private static void EnsureAllPngsAreSprites(string rootFolder)
    {
        if (!AssetDatabase.IsValidFolder(rootFolder))
        {
            throw new InvalidOperationException("Missing folder: " + rootFolder);
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { rootFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                EnsureSpriteImporter(path);
            }
        }
    }

    private static List<Sprite> GetSprites(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning("Missing sprite folder: " + folder);
            return new List<Sprite>();
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        return guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(GetTrailingNumber)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                EnsureSpriteImporter(path);
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            })
            .Where(sprite => sprite != null)
            .ToList();
    }

    private static void EnsureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static int GetTrailingNumber(string path)
    {
        Match match = Regex.Match(Path.GetFileNameWithoutExtension(path), @"(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
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
}
