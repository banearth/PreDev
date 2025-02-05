using System;
using System.Collections.Generic;

// 连接键包装类，需要实现IComparable以支持排序
public class FRepGraphConnectionKey : IComparable<FRepGraphConnectionKey>
{
	public UNetConnection ConnectionManager;

	public FRepGraphConnectionKey(UNetConnection manager)
	{
		ConnectionManager = manager;
	}

	// 实现比较方法
	public int CompareTo(FRepGraphConnectionKey other)
	{
		if (other == null) return 1;
		// 使用ConnectionManager的某个唯一标识符进行比较
		// 这里假设ConnectionManager有一个Id属性
		return ConnectionManager.ConnectionId.CompareTo(other.ConnectionManager.ConnectionId);
	}

	// 重写Equals和GetHashCode
	public override bool Equals(object obj)
	{
		if (obj is FRepGraphConnectionKey other)
		{
			return ConnectionManager == other.ConnectionManager;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return ConnectionManager?.GetHashCode() ?? 0;
	}
}

public class UReplicationGraphNode_DormancyNode : UReplicationGraphNode_ActorList
{
    // 连接节点映射
    private SortedDictionary<FRepGraphConnectionKey, UReplicationGraphNode_ConnectionDormancyNode> ConnectionNodes = 
        new SortedDictionary<FRepGraphConnectionKey, UReplicationGraphNode_ConnectionDormancyNode>();

    // 最大Z轴高度限制
    public static float MaxZForConnection = float.MaxValue;

    public void AddDormantActor(FNewReplicatedActorInfo actorInfo, FGlobalActorReplicationInfo globalInfo)
    {
        // 调用基类方法，将Actor添加到ReplicationActorList
        base.NotifyAddNetworkActor(actorInfo);
        
        // 通知所有连接节点
        foreach (var connectionNode in ConnectionNodes.Values)
        {
            connectionNode.NotifyAddNetworkActor(actorInfo);   
        }
        
        // 绑定休眠刷新事件
        globalInfo.Events.DormancyFlush -= OnActorDormancyFlush;
        globalInfo.Events.DormancyFlush += OnActorDormancyFlush;
    }

    public void RemoveDormantActor(FNewReplicatedActorInfo actorInfo, FGlobalActorReplicationInfo actorRepInfo)
    {
        // 从基类的ReplicationActorList中移除
        base.RemoveNetworkActorFast(actorInfo);

        // 解绑休眠刷新事件
        actorRepInfo.Events.DormancyFlush -= OnActorDormancyFlush;

        // 通知所有连接节点
        foreach (var connectionNode in ConnectionNodes.Values)
        {
            connectionNode.NotifyRemoveNetworkActor(actorInfo, false);
        }
    }

    public override void NotifyResetAllNetworkActors()
    {
        base.NotifyResetAllNetworkActors();
        foreach (var node in ConnectionNodes.Values)
        {
            node.NotifyResetAllNetworkActors();
        }
    }

    private void OnActorDormancyFlush(FActorRepListType actor, FGlobalActorReplicationInfo globalInfo)
    {
        // 检查Actor是否在基类的列表中
        if (ReplicationActorList.Contains(actor))
        {
            foreach (var connectionNode in ConnectionNodes.Values)
            {
                connectionNode.NotifyActorDormancyFlush(actor);   
            }
        }
    }

    public UReplicationGraphNode_ConnectionDormancyNode GetExistingConnectionNode(FConnectionGatherActorListParameters parameters)
    {
        foreach (var pair in ConnectionNodes)
        {
            var key = pair.Key;
            if (key.ConnectionManager.ConnectionId == parameters.ConnectionManager.NetConnection.ConnectionId)
            {
                return pair.Value;
            }
        }
        return null;
    }

    public UReplicationGraphNode_ConnectionDormancyNode GetConnectionNode(FConnectionGatherActorListParameters parameters)
    {
        var node = GetExistingConnectionNode(parameters);
        if (node == null)
        {
            node = CreateConnectionNode(parameters);
        }
        return node;
    }

    private UReplicationGraphNode_ConnectionDormancyNode CreateConnectionNode(FConnectionGatherActorListParameters parameters)
    {
        var repGraphConnection = new FRepGraphConnectionKey(parameters.ConnectionManager.NetConnection);
        
        // 创建新的连接节点作为子节点
        var connectionNode = CreateChildNode<UReplicationGraphNode_ConnectionDormancyNode>();
        ConnectionNodes.Add(repGraphConnection, connectionNode);
        
        // 从当前节点深拷贝Actor列表到新的连接节点
        connectionNode.DeepCopyActorListsFrom(this);
        
        // 初始化连接节点
        connectionNode.InitConnectionNode(repGraphConnection, parameters.ReplicationFrameNum);
        
        return connectionNode;
    }

    public override void GatherActorListsForConnection(FConnectionGatherActorListParameters parameters)
    {
        // 获取或创建连接节点
        var connectionNode = GetConnectionNode(parameters);
        if (connectionNode != null)
        {
            // 使用连接节点收集Actor列表
            connectionNode.GatherActorListsForConnection(parameters);
        }
    }

    public void ConditionalGatherDormantDynamicActors(FActorRepListRefView RepList, 
        FConnectionGatherActorListParameters Params, FActorRepListRefView RemovedList = null, 
        bool bEnforceReplistUniqueness = false, FActorRepListRefView RemoveFromList = null)
    {
        void GatherDormantDynamicActorsForList(FActorRepListRefView InReplicationActorList)
        {
            foreach (var Actor in InReplicationActorList)
            {
                // if (Actor != null && !Actor.IsNetStartupActor())
				if (Actor != null)
                {
                    var Info = Params.ConnectionManager.ActorInfoMap.Find(Actor);
                    if (Info != null)
                    {
                        // 需要在这里获取即将休眠的Actor，即使它们可能还没有完全休眠
                        if (Info.bDormantOnConnection || Info.Channel != null)
                        {
                            if (RemovedList != null && RemovedList.Contains(Actor))
                            {
                                continue;
                            }

                            // 从RemoveFromList中移除时保持分配，这样可以在移除多个项目时节省时间
                            if (RemoveFromList != null && RemoveFromList.RemoveFast(Actor))
                            {
                                Info.bGridSpatilization_AlreadyDormant = false;
                            }

                            // 如果我们已经添加了Actor，则防止再次添加，这可以节省增长操作
                            if (bEnforceReplistUniqueness)
                            {
                                if (Info.bGridSpatilization_AlreadyDormant)
                                {
                                    continue;
                                }
                                else
                                {
                                    Info.bGridSpatilization_AlreadyDormant = true;
                                }
                            }

                            RepList.ConditionalAdd(Actor);
                        }
                    }
                }
            }
        }

        // 处理非流关卡Actor
        GatherDormantDynamicActorsForList(ReplicationActorList);

        // 处理流关卡Actor
        foreach (var StreamingList in StreamingLevelCollection.StreamingLevelLists)
        {
            if (Params.CheckClientVisibilityForLevel(StreamingList.StreamingLevelName))
            {
                GatherDormantDynamicActorsForList(StreamingList.ReplicationActorList);
            }
        }
    }
}