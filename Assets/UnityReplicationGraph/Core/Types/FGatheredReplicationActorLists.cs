using System;
using System.Collections.Generic;

/// <summary>
/// Actor列表类型标记
/// </summary>
public enum EActorRepListTypeFlags : uint
{
    Default = 0,
	FastShared = 1,
	Max
}

/// <summary>
/// 这表示"已收集的Actor列表"。这是我们推送到复制图的内容,节点将添加它们的复制列表或推送/弹出列表类别
/// </summary>
public class FGatheredReplicationActorLists
{
    /// <summary>
    /// 不同类型的Actor列表集合
    /// </summary>
    private List<FActorRepListType>[] ReplicationLists;

    public FGatheredReplicationActorLists()
    {
        // 初始化固定大小的数组,对应EActorRepListTypeFlags的数量
        ReplicationLists = new List<FActorRepListType>[(int)EActorRepListTypeFlags.Max];
        for (int i = 0; i < (int)EActorRepListTypeFlags.Max; i++)
        {
            ReplicationLists[i] = new List<FActorRepListType>();
        }
    }

    /// <summary>
    /// 添加一个Actor列表到收集器中
    /// </summary>
    public void AddReplicationActorList(FActorRepListRefView List, EActorRepListTypeFlags Flags = EActorRepListTypeFlags.Default)
    {
#if DEBUG
        // TODO: 添加验证逻辑
        // if (CVar_RepGraph_Verify)
        //     List.VerifyContents_Slow();
#endif
        List.AppendToTArray(ReplicationLists[(int)Flags]);
    }

    /// <summary>
    /// 重置所有列表
    /// </summary>
    public void Reset()
    {
        for (int i = (int)EActorRepListTypeFlags.Default; i < (int)EActorRepListTypeFlags.Max; ++i)
        {
            ReplicationLists[i].Clear();
        }
    }

    /// <summary>
    /// 获取列表数量
    /// </summary>
    public int NumLists()
    {
        return ReplicationLists.Length;
    }

    /// <summary>
    /// 查看指定类型的Actor列表
    /// </summary>
    public IReadOnlyList<FActorRepListType> ViewActors(EActorRepListTypeFlags ListFlags)
    {
        return ReplicationLists[(int)ListFlags];
    }

    /// <summary>
    /// 检查指定类型是否包含列表
    /// </summary>
    public bool ContainsLists(EActorRepListTypeFlags Flags)
    {
        return ReplicationLists[(int)Flags].Count > 0;
    }
}