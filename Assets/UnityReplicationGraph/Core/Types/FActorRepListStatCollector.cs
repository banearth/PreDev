using System.Collections.Generic;

/// <summary>
/// 此结构将传递给复制图中的所有节点并收集每个ActorRepList的信息
/// </summary>
public class FActorRepListStatCollector
{
    private class FRepListStats
    {
        public uint NumLists = 0;      // 列表数量
        public uint NumActors = 0;     // Actor总数
        public uint MaxListSize = 0;    // 最大列表大小
        public uint NumSlack = 0;      // 空闲空间数量
        public ulong NumBytes = 0;      // 占用字节数
    }

    // 按类名统计的数据
    private Dictionary<string, FRepListStats> PerClassStats = new();
    
    // 按流关卡统计的数据
    private Dictionary<string, FRepListStats> PerStreamingLevelStats = new();
    
    // 记录已访问的节点,防止同一节点被多次访问(因为同一节点可能在多个连接间共享)
    private Dictionary<UReplicationGraphNode, bool> VisitedNodes = new();

    /// <summary>
    /// 收集单个FActorRepList的统计信息
    /// </summary>
    public void VisitRepList(UReplicationGraphNode nodeToVisit, FActorRepListRefView repList)
    {
        if (WasNodeVisited(nodeToVisit))
        {
            return;
        }
        var stats = PerClassStats.GetOrAdd(nodeToVisit.GetType().Name, () => new FRepListStats());
        stats.NumLists++;
        stats.NumActors += (uint)repList.Num();
        stats.MaxListSize = System.Math.Max(stats.MaxListSize, (uint)repList.Num());
		stats.NumSlack += (uint)repList.RepList.GetSlack();
		stats.NumBytes += (ulong)repList.RepList.GetAllocatedSize();
    }

    /// <summary>
    /// 收集FActorRepLists集合的统计信息
    /// </summary>
    public void VisitStreamingLevelCollection(UReplicationGraphNode nodeToVisit, FStreamingLevelActorListCollection streamingLevelList)
    {
        if (WasNodeVisited(nodeToVisit))
        {
            return;
        }
        foreach (var item in streamingLevelList.StreamingLevelLists)
        {
            var stats = PerStreamingLevelStats.GetOrAdd(item.StreamingLevelName.ToString(), () => new FRepListStats());
            stats.NumLists++;
            stats.NumActors += (uint)item.ReplicationActorList.Num();
            stats.MaxListSize = System.Math.Max(stats.MaxListSize, (uint)item.ReplicationActorList.Num());
            stats.NumSlack += (uint)item.ReplicationActorList.RepList.GetSlack();
            stats.NumBytes += (ulong)item.ReplicationActorList.RepList.GetAllocatedSize();
        }
    }

    /// <summary>
    /// 收集不由节点持有的FActorRepLists的统计信息
    /// </summary>
    public void VisitExplicitStreamingLevelList(string listOwnerName, string streamLevelName, FActorRepListRefView repList)
    {
        var stats = PerStreamingLevelStats.GetOrAdd(streamLevelName, () => new FRepListStats());
        
        stats.NumLists++;
        stats.NumActors += (uint)repList.Num();
        stats.MaxListSize = System.Math.Max(stats.MaxListSize, (uint)repList.Num());
        stats.NumSlack += (uint)repList.RepList.GetSlack();
        stats.NumBytes += (ulong)repList.RepList.GetAllocatedSize();
    }

    /// <summary>
    /// 标记节点已被访问,防止在多个连接间共享时重复访问
    /// </summary>
    public void FlagNodeVisited(UReplicationGraphNode nodeToVisit)
    {
        VisitedNodes[nodeToVisit] = true;
    }

    /// <summary>
    /// 打印之前收集的统计数据
    /// </summary>
    public void PrintCollectedData()
    {
        ReplicationGraphDebugger.LogInfo("Per Class Stats:");
        foreach (var kvp in PerClassStats)
        {
            ReplicationGraphDebugger.LogInfo($"  {kvp.Key}: Lists:{kvp.Value.NumLists} Actors:{kvp.Value.NumActors} MaxSize:{kvp.Value.MaxListSize} Slack:{kvp.Value.NumSlack} Bytes:{kvp.Value.NumBytes}");
        }

        ReplicationGraphDebugger.LogInfo("Per Streaming Level Stats:");
        foreach (var kvp in PerStreamingLevelStats)
        {
            ReplicationGraphDebugger.LogInfo($"  {kvp.Key}: Lists:{kvp.Value.NumLists} Actors:{kvp.Value.NumActors} MaxSize:{kvp.Value.MaxListSize} Slack:{kvp.Value.NumSlack} Bytes:{kvp.Value.NumBytes}");
        }
    }

    private bool WasNodeVisited(UReplicationGraphNode nodeToVisit)
    {
        return VisitedNodes.ContainsKey(nodeToVisit);
    }
}