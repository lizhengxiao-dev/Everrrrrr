using UnityEngine;

public class RobotAnimationController : MonoBehaviour
{
    [Header("玩家的手部追踪红方块：")]
    public Transform playerHandTarget;

    [Header("触发动作的边界线 (可随时在面板调整)：")]
    public float topThreshold = 1.5f;  // 向上举手的高度线
    public float sideThreshold = 2.0f; // 向左/向右挥手的宽度线

    [Header("动作保持")]
    [Range(0f, 1f)]
    public float holdPoseTime = 0.5f;

    [Header("Animator 状态名")]
    public string idleStateName = "Male_Idle_Anim";
    public string leftStateName = "Male_PointLeft_Anim";
    public string rightStateName = "Male_PointRight_Anim";
    public string upStateName = "Male_ArmRaise_Anim";

    [Header("启用动作")]
    public bool enableLeftAction = true;
    public bool enableRightAction = true;
    public bool enableUpAction = true;

    private Animator myAnimator;
    private HandTracker handTracker;
    private int lastDefenseDirection = -1;

    void Start()
    {
        // 自动获取机器人身上的控制室
        myAnimator = GetComponent<Animator>();

        if (playerHandTarget != null)
        {
            handTracker = playerHandTarget.GetComponent<HandTracker>();
        }
    }

    void Update()
    {
        // 防报错：如果还没绑定红方块，就不执行
        if (playerHandTarget == null) return;

        if (handTracker == null)
        {
            handTracker = playerHandTarget.GetComponent<HandTracker>();
        }

        // 如果 RehabTarget 是由 MediaPipe 动作标签控制，优先直接使用动作标签。
        // 这样 UP 不再依赖坐标缩放、Y轴反转或插值延迟。
        if (handTracker != null)
        {
            string latestAction = handTracker.GetLatestAction();

            if (latestAction == "UP" || latestAction == "ARMRAISE")
            {
                ApplyDefenseDirection(enableUpAction ? 3 : 0);
                return;
            }

            if (latestAction == "LEFT")
            {
                ApplyDefenseDirection(enableLeftAction ? 1 : 0);
                return;
            }

            if (latestAction == "RIGHT")
            {
                ApplyDefenseDirection(enableRightAction ? 2 : 0);
                return;
            }

            if (latestAction == "IDLE" || latestAction == "CENTER")
            {
                ApplyDefenseDirection(0);
                return;
            }
        }

        // 获取红方块当前的 X (左右) 和 Y (上下) 坐标
        float currentX = playerHandTarget.position.x;
        float currentY = playerHandTarget.position.y;

        // 核心逻辑：0=待机, 1=左格挡, 2=右格挡, 3=上格挡
        
        // 优先判断向上 (如果手举得够高，就是上格挡)
        if (enableUpAction && currentY > topThreshold)
        {
            ApplyDefenseDirection(3);
        }
        // 判断向左 (X坐标越往左越小，是负数，所以用小于号)
        else if (enableLeftAction && currentX < -sideThreshold)
        {
            ApplyDefenseDirection(1);
        }
        // 判断向右 (X坐标越往右越大，是正数，所以用大于号)
        else if (enableRightAction && currentX > sideThreshold)
        {
            ApplyDefenseDirection(2);
        }
        // 如果手在中间区域，没越过任何边界，就乖乖待机
        else
        {
            ApplyDefenseDirection(0);
        }
    }

    private void ApplyDefenseDirection(int direction)
    {
        myAnimator.SetInteger("DefenseDirection", direction);

        if (direction == lastDefenseDirection)
        {
            HoldActionPoseIfNeeded(direction);
            return;
        }

        lastDefenseDirection = direction;
        myAnimator.speed = 1f;

        switch (direction)
        {
            case 1:
                myAnimator.CrossFade(leftStateName, 0.05f);
                break;
            case 2:
                myAnimator.CrossFade(rightStateName, 0.05f);
                break;
            case 3:
                myAnimator.CrossFade(upStateName, 0.05f);
                break;
            default:
                myAnimator.CrossFade(idleStateName, 0.05f);
                break;
        }
    }

    private void HoldActionPoseIfNeeded(int direction)
    {
        if (direction == 0)
        {
            myAnimator.speed = 1f;
            return;
        }

        string stateName = GetStateName(direction);
        AnimatorStateInfo stateInfo = myAnimator.GetCurrentAnimatorStateInfo(0);

        if (!stateInfo.IsName(stateName))
        {
            return;
        }

        if (stateInfo.normalizedTime >= holdPoseTime)
        {
            myAnimator.Play(stateName, 0, holdPoseTime);
            myAnimator.speed = 0f;
        }
    }

    private string GetStateName(int direction)
    {
        switch (direction)
        {
            case 1:
                return leftStateName;
            case 2:
                return rightStateName;
            case 3:
                return upStateName;
            default:
                return idleStateName;
        }
    }
}
