using System.Collections.Generic;

/// <summary>
/// 收集阶段传递的参数结构
/// </summary>
public class FConnectionGatherActorListParameters
{
    /// <summary>
    /// 观察者列表
    /// </summary>
    public List<FNetViewer> Viewers { get; private set; }

    /// <summary>
    /// 连接管理器
    /// </summary>
    public UNetReplicationGraphConnection ConnectionManager { get; private set; }

    /// <summary>
    /// 复制帧号
    /// </summary>
    public uint ReplicationFrameNum { get; private set; }

    /// <summary>
    /// 输出：节点要添加的数据
    /// </summary>
    public FGatheredReplicationActorLists OutGatheredReplicationLists { get; private set; }

    /// <summary>
    /// 客户端可见关卡名称引用(用于快速查找关卡可见性)
    /// </summary>
    public HashSet<string> ClientVisibleLevelNamesRef { get; private set; }

    /// <summary>
    /// 是否被选中进行重计算
    /// </summary>
    public bool bIsSelectedForHeavyComputation { get; private set; }

    // 缓存上次检查的可见关卡名称
    private string LastCheckedVisibleLevelName;

    public FConnectionGatherActorListParameters(
        List<FNetViewer> inViewers,
        UNetReplicationGraphConnection inConnectionManager,
        HashSet<string> inClientVisibleLevelNamesRef,
        uint inReplicationFrameNum,
        FGatheredReplicationActorLists inOutGatheredReplicationLists,
        bool bInSelectedForHeavyComputation)
    {
        Viewers = inViewers;
        ConnectionManager = inConnectionManager;
        ReplicationFrameNum = inReplicationFrameNum;
        OutGatheredReplicationLists = inOutGatheredReplicationLists;
        ClientVisibleLevelNamesRef = inClientVisibleLevelNamesRef;
        bIsSelectedForHeavyComputation = bInSelectedForHeavyComputation;
    }

    /// <summary>
    /// 检查客户端是否可见指定关卡
    /// </summary>
    public bool CheckClientVisibilityForLevel(string StreamingLevelName)
    {
        if (StreamingLevelName == LastCheckedVisibleLevelName)
        {
            return true;
        }

        bool bVisible = ClientVisibleLevelNamesRef.Contains(StreamingLevelName);
        if (bVisible)
        {
            LastCheckedVisibleLevelName = StreamingLevelName;
        }
        return bVisible;
    }
}