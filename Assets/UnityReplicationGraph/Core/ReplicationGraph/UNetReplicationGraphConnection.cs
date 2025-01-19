using System;
using System.Collections.Generic;
using UnityEngine;

public struct FVisibleCellInfo
{
    // 使用Unity的Vector2Int替代FIntPoint
    public Vector2Int Location;
    public int Lifetime;

    public FVisibleCellInfo(Vector2Int location, int lifetime = 0)
    {
        Location = location;
        Lifetime = lifetime;
    }

    // 重载相等运算符用于查找
    public static bool operator ==(FVisibleCellInfo cell, Vector2Int point)
    {
        return cell.Location == point;
    }

    public static bool operator !=(FVisibleCellInfo cell, Vector2Int point)
    {
        return !(cell == point);
    }

    public override bool Equals(object obj)
    {
        if (obj is Vector2Int point)
        {
            return this == point;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return Location.GetHashCode();
    }
}

/// <summary>
/// 复制图连接管理器
/// </summary>
public class UNetReplicationGraphConnection : UObject
{

	/// <summary>
	/// 网络连接
	/// </summary>
	public UNetConnection NetConnection { get; private set; }

    /// <summary>
    /// 每个Actor的连接数据映射
    /// </summary>
    public FPerConnectionActorInfoMap ActorInfoMap { get; private set; }

    /// <summary>
    /// 连接在全局列表中的索引
    /// </summary>
    public int ConnectionOrderNum { get; set; }

    /// <summary>
    /// Actor通道创建时的排队比特数
    /// </summary>
    public int QueuedBitsForActorDiscovery { get; set; }

    /// <summary>
    /// 最后一次收集的位置信息
    /// </summary>
    public List<FLastLocationGatherInfo> LastGatherLocations = new List<FLastLocationGatherInfo>();

    /// <summary>
    /// 连接图节点
    /// </summary>
    public List<UReplicationGraphNode> ConnectionGraphNodes = new List<UReplicationGraphNode>();

    /// <summary>
    /// 断开连接节点
    /// </summary>
    public UReplicationGraphNode_TearOff_ForConnection TearOffNode;

    /// <summary>
    /// 缓存的客户端可见关卡
    /// </summary>
    private HashSet<string> CachedVisibleLevels = new HashSet<string>();

    /// <summary>
    /// 每个节点的上一个休眠Actor列表
    /// </summary>
    private Dictionary<UReplicationGraphNode, FActorRepListRefView> PrevDormantActorListPerNode = 
        new Dictionary<UReplicationGraphNode, FActorRepListRefView>();

    /// <summary>
    /// 每个图节点的可见单元格历史记录
    /// </summary>
    public Dictionary<UReplicationGraphNode, List<FVisibleCellInfo>> NodesVisibleCells = 
        new Dictionary<UReplicationGraphNode, List<FVisibleCellInfo>>();

    /// <summary>
    /// 超出范围的已销毁Actor列表
    /// </summary>
    private List<FCachedDestructInfo> OutOfRangeDestroyedActors = new List<FCachedDestructInfo>();

    /// <summary>
    /// 待处理的销毁信息列表
    /// </summary>
    private List<FCachedDestructInfo> PendingDestructInfoList = new List<FCachedDestructInfo>();

	/** List of dormant actors that should be removed from the client */
	List<FCachedDormantDestructInfo> PendingDormantDestructList;

	/** Set used to guard against double adds into PendingDormantDestructList */
	private HashSet<uint> TrackedDormantDestructionInfos;

	public UNetReplicationGraphConnection()
    {
        ActorInfoMap = new FPerConnectionActorInfoMap();
    }

    /// <summary>
    /// 初始化图关联
    /// </summary>
    public void InitForGraph(UReplicationGraph graph)
    {
        // 设置World作为Outer
        SetOuter(graph.GetWorld());

        var globals = graph.GetGraphGlobals();
        if (globals != null)
        {
            ActorInfoMap.SetGlobalMap(globals.GlobalActorReplicationInfoMap);
        }
    }

    /// <summary>
    /// 初始化连接关联
    /// </summary>
    public void InitForConnection(UNetConnection InConnection)
    {
        NetConnection = InConnection;
        InConnection.SetReplicationConnectionDriver(this);

        #if ENABLE_REPGRAPH_DEBUG_ACTOR
        var graph = GetGraph();
        var debugActor = graph.CreateDebugActor();
        if (debugActor != null)
        {
            debugActor.ConnectionManager = this;
            debugActor.ReplicationGraph = graph;
        }
        #endif
    }

    /// <summary>
    /// 准备复制
    /// </summary>
    public bool PrepareForReplication()
    {
        // 检查世界是否正确
        var currentWorld = GetWorld();
        // 构建可见关卡列表
        BuildVisibleLevels();
        // 处理动态休眠销毁的生命周期
        if (NodesVisibleCells.Count > 0)
        {
			// 第一遍:减少生命周期
			foreach (var nodePair in NodesVisibleCells)
			{
				var cells = nodePair.Value;
				for (int i = 0; i < cells.Count; i++)
				{
					// 使用索引器访问和修改
					var cellInfo = cells[i];
					if (cellInfo.Lifetime > 0)
					{
						cellInfo.Lifetime--;
						cells[i] = cellInfo; // 更新回列表
					}
				}
			}

			// 第二遍:移除已死亡的节点
			var deadNodes = new List<UReplicationGraphNode>();
            foreach (var pair in NodesVisibleCells)
            {
                if (pair.Value.Count == 0)
                {
                    deadNodes.Add(pair.Key);
                }
            }

            // 清理死亡节点
            foreach (var node in deadNodes)
            {
                NodesVisibleCells.Remove(node);
            }
        }

		// 检查连接状态和ViewTarget
		return !NetConnection.IsClosingOrClosed() && NetConnection.ViewTarget != null;
    }

    /// <summary>
    /// 获取连接图节点
    /// </summary>
    public IReadOnlyList<UReplicationGraphNode> GetConnectionGraphNodes()
    {
        return ConnectionGraphNodes;
    }

    /// <summary>
    /// 添加连接图节点
    /// </summary>
    public void AddConnectionGraphNode(UReplicationGraphNode node)
    {
        ConnectionGraphNodes.Add(node);
    }

    /// <summary>
    /// 移除连接图节点
    /// </summary>
    public void RemoveConnectionGraphNode(UReplicationGraphNode node)
    {
        ConnectionGraphNodes.Remove(node);
    }

    /// <summary>
    /// 获取缓存的客户端可见关卡名称
    /// </summary>
    public HashSet<string> GetCachedClientVisibleLevelNames()
    {
        return CachedVisibleLevels;
    }

    /// <summary>
    /// 获取指定节点的上一个休眠Actor列表
    /// </summary>
    public FActorRepListRefView GetPrevDormantActorListForNode(UReplicationGraphNode gridNode)
    {
        if (!PrevDormantActorListPerNode.TryGetValue(gridNode, out var list))
        {
            list = new FActorRepListRefView();
            PrevDormantActorListPerNode[gridNode] = list;
        }
        return list;
    }

    /// <summary>
    /// 从所有上一个休眠Actor列表中移除Actor
    /// </summary>
    public void RemoveActorFromAllPrevDormantActorLists(FActorRepListType actor)
    {
        foreach (var list in PrevDormantActorListPerNode.Values)
        {
            list.RemoveFast(actor);
        }
    }

    /// <summary>
    /// 清理节点缓存
    /// </summary>
    public void CleanupNodeCaches(UReplicationGraphNode node)
    {
        PrevDormantActorListPerNode.Remove(node);
        NodesVisibleCells.Remove(node);
    }

    /// <summary>
    /// 更新客户端可见关卡列表
    /// </summary>
    public void BuildVisibleLevels()
    {
        CachedVisibleLevels.Clear();
        if (NetConnection != null)
        {
            foreach (var levelName in NetConnection.ClientVisibleLevelNames)
            {
                CachedVisibleLevels.Add(levelName);
            }
        }
    }

	/// <summary>
	/// 更新连接的位置收集信息
	/// </summary>
	public void UpdateGatherLocationsForConnection(List<FNetViewer> connectionViewers,
		FReplicationGraphDestructionSettings destructionSettings)
	{
		// 遍历所有观察者
		foreach (var curViewer in connectionViewers)
		{
			if (curViewer.Connection != null)
			{
				// 尝试找到此观察者的上一次位置信息
				var lastInfoForViewer = LastGatherLocations.Find(
					info => info.Connection == curViewer.Connection);
				if (lastInfoForViewer != null)
				{
					// 更新已存在观察者的位置
					OnUpdateViewerLocation(lastInfoForViewer, curViewer, destructionSettings);
				}
				else
				{
					// 添加新观察者到上一次收集位置列表
					LastGatherLocations.Add(new FLastLocationGatherInfo(
						curViewer.Connection,
						curViewer.ViewLocation));
				}
			}
		}
		// 清理已失效的条目
		LastGatherLocations.RemoveAll(gatherInfo => gatherInfo.Connection == null);
	}

	/// <summary>
	/// 更新观察者位置
	/// </summary>
	private void OnUpdateViewerLocation(FLastLocationGatherInfo lastInfo, 
        FNetViewer currentViewer,
        FReplicationGraphDestructionSettings destructionSettings)
    {
        // 是否忽略距离检查
        bool bIgnoreDistanceCheck = destructionSettings.OutOfRangeDistanceCheckThresholdSquared == 0.0f;

        // 计算与上次检查位置的距离平方
        float outOfRangeDistanceSquared = Vector3.SqrMagnitude(
            (Vector3)(currentViewer.ViewLocation - lastInfo.LastOutOfRangeLocationCheck));

        // 只有当观察者移动足够远,或忽略距离检查时,才测试累积的超出范围Actor
        if (bIgnoreDistanceCheck || 
            outOfRangeDistanceSquared > destructionSettings.OutOfRangeDistanceCheckThresholdSquared)
        {
            // 更新最后检查位置
            lastInfo.LastOutOfRangeLocationCheck = currentViewer.ViewLocation;

            // 检查所有超出范围的已销毁Actor
            for (int index = OutOfRangeDestroyedActors.Count - 1; index >= 0; --index)
            {
                var cachedInfo = OutOfRangeDestroyedActors[index];

                // 计算Actor与观察者的距离平方
                float actorDistSquared = Vector3.SqrMagnitude(
                    (Vector3)(cachedInfo.CachedPosition - currentViewer.ViewLocation));

                // 如果Actor在最大待处理列表距离内
                if (actorDistSquared <= destructionSettings.MaxPendingListDistanceSquared)
                {
                    // 将信息移动到待处理列表以进行复制
                    PendingDestructInfoList.Add(cachedInfo);
                    OutOfRangeDestroyedActors.RemoveAt(index);
                }
            }
        }

        // 更新最后位置
        lastInfo.LastLocation = currentViewer.ViewLocation;
	}

	public void NotifyAddDormantDestructionInfo(FActorRepListType actor)
	{
		// 基础验证
		if (actor == null || NetConnection == null ||
			NetConnection.Driver == null)
		{
			return;
		}

		// 获取Actor所在的关卡
		var level = actor.GetLevel();
		if (level != null && !level.IsPersistentLevel())
		{
			// 获取流关卡名称
			string streamingLevelName = level.GetStreamingLevelName();
			// 检查客户端是否能看到这个关卡
			if (!NetConnection.ClientVisibleLevelNames.Contains(streamingLevelName))
			{
				ReplicationGraphDebugger.LogInfo($"NotifyAddDormantDestructionInfo 跳过actor [{ReplicationGraphDebugger.GetActorRepListTypeDebugString(actor)}] " +
					$"因为流关卡 [{streamingLevelName}] 不再可见。");
				return;
			}
		}

		// 使用NetId作为唯一标识
		uint netId = actor.NetId;
		if (netId != 0) // 假设0是无效ID
		{
			// 检查是否已经在追踪列表中
			if (TrackedDormantDestructionInfos.Contains(netId))
			{
				return;
			}

			// 添加到追踪列表
			TrackedDormantDestructionInfos.Add(netId);

			// 创建并添加休眠销毁信息
			var info = new FCachedDormantDestructInfo
			{
				NetId = netId,
				Level = level,
				ObjOuter = actor.GetOuter(),
				PathName = actor.Name
			};
			PendingDormantDestructList.Add(info);
			// 将Actor标记为活跃
			// 后面还有一步，我暂时不处理了
		}
	}

	/// <summary>
	/// 缓存的销毁信息
	/// </summary>
	private class FCachedDestructInfo
    {
        public Vector3 CachedPosition { get; set; }
        // 其他需要的销毁信息...
    }

	public class FCachedDormantDestructInfo
	{
		public uint NetId { get; set; }
		public ULevel Level { get; set; }
		public UObject ObjOuter { get; set; }
		public string PathName { get; set; }
	}

	public List<FVisibleCellInfo> GetVisibleCellsForNode(UReplicationGraphNode GridNode)
	{
		return NodesVisibleCells.GetOrAdd(GridNode, () => new List<FVisibleCellInfo>());
	}

}