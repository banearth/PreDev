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
    // 休眠的Actor列表
    private List<FActorRepListType> DormantActors = new List<FActorRepListType>();
    
    // 休眠Actor的全局信息映射
    private Dictionary<FActorRepListType, FGlobalActorReplicationInfo> DormantActorsGlobalInfo = 
        new Dictionary<FActorRepListType, FGlobalActorReplicationInfo>();

    // 连接节点列表
    private SortedDictionary<FRepGraphConnectionKey, UReplicationGraphNode_ConnectionDormancyNode> ConnectionNodes = 
        new SortedDictionary<FRepGraphConnectionKey, UReplicationGraphNode_ConnectionDormancyNode>();

    public void AddDormantActor(FNewReplicatedActorInfo actorInfo, FGlobalActorReplicationInfo globalInfo)
    {
        // 调用基类方法，它会处理Actor列表的管理
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
        // 调用基类移除方法，它会处理Actor列表的管理
        base.RemoveNetworkActorFast(actorInfo);

        // 解绑休眠刷新事件
        actorRepInfo.Events.DormancyFlush -= OnActorDormancyFlush;

		// 通知所有连接节点
		foreach (var connectionNode in ConnectionNodes.Values)
        {
			connectionNode.NotifyRemoveNetworkActor(actorInfo, false);
		}
    }

    public void RenameDormantActor(FRenamedReplicatedActorInfo actorInfo)
    {
        int idx = DormantActors.IndexOf(actorInfo.OldActorInfo.Actor);
        if (idx != -1)
        {
            DormantActors[idx] = actorInfo.NewActorInfo.Actor;
            
            if (DormantActorsGlobalInfo.TryGetValue(actorInfo.OldActorInfo.Actor, out var globalInfo))
            {
                DormantActorsGlobalInfo.Remove(actorInfo.OldActorInfo.Actor);
                DormantActorsGlobalInfo[actorInfo.NewActorInfo.Actor] = globalInfo;
            }
        }
    }

	public void ConditionalGatherDormantDynamicActors(
		 FActorRepListRefView repList,
		 FConnectionGatherActorListParameters parameters,
		 FActorRepListRefView removedList = null,
		 bool bEnforceReplistUniqueness = false,
		 FActorRepListRefView removeFromList = null)
	{
		// 定义收集休眠动态Actor的委托
		void GatherDormantDynamicActorsForList(FActorRepListRefView inReplicationActorList)
		{
			foreach (var actor in inReplicationActorList)
			{
				// 跳过关卡放置的Actor（只处理动态生成的）
				if (actor == null)
					continue;

				// 获取连接上的Actor信息
				var info = parameters.ConnectionManager.ActorInfoMap.Find(actor);
				if (info == null)
					continue;

				// 检查Actor是否已经休眠或有有效通道
				if (!info.bDormantOnConnection && info.Channel == null)
					continue;

				// 如果Actor在移除列表中，跳过
				if (removedList != null && removedList.Contains(actor))
					continue;

				// 如果需要从特定列表中移除
				if (removeFromList != null && removeFromList.RemoveFast(actor))
				{
					info.bGridSpatilization_AlreadyDormant = false;
				}

				// 如果需要确保唯一性
				if (bEnforceReplistUniqueness)
				{
					// 已经在列表中，跳过
					if (info.bGridSpatilization_AlreadyDormant)
						continue;

					// 标记为已添加
					info.bGridSpatilization_AlreadyDormant = true;
				}

				// 添加到复制列表
				repList.ConditionalAdd(actor);
			}
		}

		// 处理主要的复制列表
		GatherDormantDynamicActorsForList(ReplicationActorList);

		// 处理流关卡中的Actor列表
		foreach (var streamingList in StreamingLevelCollection.StreamingLevelLists)
		{
			// 检查客户端是否可见该关卡
			if (parameters.CheckClientVisibilityForLevel(streamingList.StreamingLevelName))
			{
				GatherDormantDynamicActorsForList(streamingList.ReplicationActorList);
			}
		}
	}

    public override void GetAllActorsInNode_Debugging(List<FActorRepListType> outArray)
    {
        outArray.AddRange(DormantActors);
    }

    public override void NotifyResetAllNetworkActors()
    {
        DormantActors.Clear();
        DormantActorsGlobalInfo.Clear();
        base.NotifyResetAllNetworkActors();
    }

    private void OnActorDormancyFlush(FActorRepListType actor, FGlobalActorReplicationInfo globalInfo)
    {
        // 处理Actor的休眠刷新
        if (DormantActors.Contains(actor))
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
			var node = pair.Value;
			if (key.ConnectionManager.ConnectionId == parameters.ConnectionManager.NetConnection.ConnectionId)
            {
                return node;
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
        // 获取已存在的连接节点
        var connectionNode = GetExistingConnectionNode(parameters);

        if (connectionNode != null)
        {
            // 使用现有节点收集Actor列表
            connectionNode.GatherActorListsForConnection(parameters);
        }
        else
        {
            // 如果节点不存在，创建新节点并收集
            connectionNode = CreateConnectionNode(parameters);
            connectionNode.GatherActorListsForConnection(parameters);
        }
	}
}