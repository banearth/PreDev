/// <summary>
/// 复制图销毁设置
/// </summary>
public class FReplicationGraphDestructionSettings
{
    /// <summary>
    /// 销毁信息的最大距离平方
    /// </summary>
    public float DestructInfoMaxDistanceSquared { get; private set; }

    /// <summary>
    /// 超出范围距离检查阈值的平方
    /// </summary>
    public float OutOfRangeDistanceCheckThresholdSquared { get; private set; }

    /// <summary>
    /// 待处理列表的最大距离平方
    /// 等于 DestructInfoMaxDistanceSquared + OutOfRangeDistanceCheckThresholdSquared
    /// </summary>
    public float MaxPendingListDistanceSquared { get; private set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="destructInfoMaxDistanceSquared">销毁信息的最大距离平方</param>
    /// <param name="outOfRangeDistanceCheckThresholdSquared">超出范围距离检查阈值的平方</param>
    public FReplicationGraphDestructionSettings(float destructInfoMaxDistanceSquared, float outOfRangeDistanceCheckThresholdSquared)
    {
        DestructInfoMaxDistanceSquared = destructInfoMaxDistanceSquared;
        OutOfRangeDistanceCheckThresholdSquared = outOfRangeDistanceCheckThresholdSquared;
        MaxPendingListDistanceSquared = destructInfoMaxDistanceSquared + outOfRangeDistanceCheckThresholdSquared;
    }
}