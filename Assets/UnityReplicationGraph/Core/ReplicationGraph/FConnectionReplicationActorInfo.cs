using UnityEngine;

public class FConnectionReplicationActorInfo
{
    /// <summary>
    /// Actor通道
    /// </summary>
    public ActorChannel Channel { get; set; }

    private float CullDistance = 0f;
    private float CullDistanceSquared = 0f;

    /// <summary>
    /// 默认复制 - 下一次允许复制的帧号
    /// </summary>
    public uint NextReplicationFrameNum { get; set; }

    /// <summary>
    /// 默认复制 - 最后一次复制的帧号
    /// </summary>
    public uint LastRepFrameNum { get; set; }

    /// <summary>
    /// 快速路径 - 下一次允许复制的帧号
    /// </summary>
    public uint FastPath_NextReplicationFrameNum { get; set; }

    /// <summary>
    /// 快速路径 - 最后一次复制的帧号
    /// </summary>
    public uint FastPath_LastRepFrameNum { get; set; }

    /// <summary>
    /// 两次ReplicateActor调用之间必须经过的最小帧数
    /// </summary>
    public ushort ReplicationPeriodFrame { get; set; } = 1;

    /// <summary>
    /// 快速路径的最小帧数间隔
    /// </summary>
    public ushort FastPath_ReplicationPeriodFrame { get; set; } = 1;

    /// <summary>
    /// Actor通道关闭的帧号
    /// </summary>
    public uint ActorChannelCloseFrameNum { get; set; }

    /// <summary>
    /// 在此连接上是否休眠
    /// </summary>
    public bool bDormantOnConnection { get; set; }

    /// <summary>
    /// 是否断开
    /// </summary>
    public bool bTearOff { get; set; }

    /// <summary>
    /// 2D网格空间化优化,防止在分屏评估中重复复制相同Actor的休眠状态
    /// </summary>
    public bool bGridSpatilization_AlreadyDormant { get; set; }

    /// <summary>
    /// 强制将裁剪距离设为0
    /// </summary>
    public bool bForceCullDistanceToZero { get; set; }

    public FConnectionReplicationActorInfo()
    {
        ResetFlags();
    }

    public FConnectionReplicationActorInfo(FGlobalActorReplicationInfo GlobalInfo)
    {
        ResetFlags();
        
        // 从全局Actor信息中获取数据
        ReplicationPeriodFrame = GlobalInfo.Settings.ReplicationPeriodFrame;
        FastPath_ReplicationPeriodFrame = GlobalInfo.Settings.FastPath_ReplicationPeriodFrame;
        SetCullDistanceSquared(GlobalInfo.Settings.GetCullDistanceSquared());
    }

    private void ResetFlags()
    {
        bDormantOnConnection = false;
        bTearOff = false;
        bGridSpatilization_AlreadyDormant = false;
        bForceCullDistanceToZero = false;
    }

    /// <summary>
    /// 重置帧计数器
    /// </summary>
    public void ResetFrameCounters()
    {
        Channel = null;
        NextReplicationFrameNum = 0;
        LastRepFrameNum = 0;
        ActorChannelCloseFrameNum = 0;

        FastPath_NextReplicationFrameNum = 0;
        FastPath_LastRepFrameNum = 0;
    }

    public void SetCullDistanceSquared(float InCullDistanceSquared)
    {
        CullDistanceSquared = InCullDistanceSquared;
        CullDistance = Mathf.Sqrt(CullDistanceSquared);
    }

    public float GetCullDistance()
    {
        return bForceCullDistanceToZero ? 0.0f : CullDistance;
    }

    public float GetCullDistanceSquared()
    {
        return bForceCullDistanceToZero ? 0.0f : CullDistanceSquared;
    }
}