using UnityEngine;

/// <summary>
/// RehabTarget：康复训练目标点。
/// 红色手碰到这个目标点时，分数加 1，然后目标点随机移动到新位置。
/// </summary>
public class RehabTarget : MonoBehaviour
{
    [Header("玩家设置")]
    [Tooltip("红色手物体的名字。你的红色方块叫 PlayerHand，所以这里默认写 PlayerHand")]
    public string handObjectName = "PlayerHand";

    [Header("随机位置范围")]
    [Tooltip("目标点随机出现的最小 X 坐标")]
    public float minX = -3.5f;

    [Tooltip("目标点随机出现的最大 X 坐标")]
    public float maxX = 3.5f;

    [Tooltip("目标点随机出现的最小 Y 坐标")]
    public float minY = -2.0f;

    [Tooltip("目标点随机出现的最大 Y 坐标")]
    public float maxY = 2.0f;

    [Header("训练数据")]
    [Tooltip("当前得分。每碰到一次目标就加 1")]
    public int score = 0;

    private void Start()
    {
        // 游戏开始时，先把目标放到一个随机位置。
        MoveToRandomPosition();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 如果碰到目标的物体不是红色手，就不处理。
        if (other.gameObject.name != handObjectName)
        {
            return;
        }

        // 红色手碰到目标，分数加 1。
        score = score + 1;

        // 在 Unity Console 里打印当前分数，方便我们先测试逻辑是否成功。
        Debug.Log("碰到目标！当前得分：" + score);

        // 目标被碰到后，移动到新的随机位置。
        MoveToRandomPosition();
    }

    private void MoveToRandomPosition()
    {
        // 在设置好的 X 范围内随机取一个数。
        float randomX = Random.Range(minX, maxX);

        // 在设置好的 Y 范围内随机取一个数。
        float randomY = Random.Range(minY, maxY);

        // 2D 游戏里 Z 一般保持为 0。
        transform.position = new Vector3(randomX, randomY, 0f);
    }
}
