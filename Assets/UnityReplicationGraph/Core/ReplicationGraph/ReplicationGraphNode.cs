using UnityEngine;
using System.Collections.Generic;

// 基础节点类
public class ReplicationGraphNode
{
    protected List<ReplicationGraphNode> AllChildNodes = new List<ReplicationGraphNode>();
    protected bool bRequiresPrepareForReplicationCall = false;
    protected ReplicationGraph Graph;
    private List<ReplicatedActorInfo> _actorList = new List<ReplicatedActorInfo>();

    // 暂时不实现GraphGlobals，因为我们还不清楚FReplicationGraphGlobalData的具体用途
    
    public virtual void Initialize(/*等我们理解了GraphGlobals再添加参数*/)
    {
    }

    // 核心方法 - 收集需要复制的Actor列表
    public virtual void GatherActorListsForConnection(ConnectionGatherActorListParameters parameters)
    {
        // 基类实现 - 遍历子节点
        foreach (var childNode in AllChildNodes)
        {
            childNode.GatherActorListsForConnection(parameters);
        }
    }

    // Actor通知方法
    public virtual void NotifyAddNetworkActor(ReplicatedActorInfo actorInfo)
    {
        _actorList.Add(actorInfo);
        foreach (var childNode in AllChildNodes)
        {
            childNode.NotifyAddNetworkActor(actorInfo);
        }
    }

    public virtual void NotifyRemoveNetworkActor(ReplicatedActorInfo actorInfo)
    {
        _actorList.Remove(actorInfo);
        foreach (var childNode in AllChildNodes)
        {
            childNode.NotifyRemoveNetworkActor(actorInfo);
        }
    }

    // 准备复制
    public virtual void PrepareForReplication()
    {
        if (bRequiresPrepareForReplicationCall)
        {
            foreach (var childNode in AllChildNodes)
            {
                childNode.PrepareForReplication();
            }
        }
    }

    // 节点管理
    public virtual void AddChildNode(ReplicationGraphNode node)
    {
        node.Initialize();
        AllChildNodes.Add(node);
    }
}

// Actor列表节点 - 基础节点类型
public class ActorListGraphNode : ReplicationGraphNode
{
    protected List<ReplicatedActorInfo> ActorList = new List<ReplicatedActorInfo>();

    public override void NotifyAddNetworkActor(ReplicatedActorInfo actorInfo)
    {
        ActorList.Add(actorInfo);
        base.NotifyAddNetworkActor(actorInfo);
    }

    public override void NotifyRemoveNetworkActor(ReplicatedActorInfo actorInfo)
    {
        ActorList.Remove(actorInfo);
        base.NotifyRemoveNetworkActor(actorInfo);
    }

    public override void GatherActorListsForConnection(ConnectionGatherActorListParameters parameters)
    {
        foreach (var actorInfo in ActorList)
        {
            if (ShouldReplicateToConnection(actorInfo, parameters))
            {
                parameters.OutGatheredReplicationLists.Add(actorInfo);
            }
        }

        base.GatherActorListsForConnection(parameters);
    }

    protected virtual bool ShouldReplicateToConnection(ReplicatedActorInfo actorInfo, ConnectionGatherActorListParameters parameters)
    {
        return true;
    }
}

public class ViewerInfo
{
    public Vector3 ViewLocation { get; set; }
    public float ViewRadius { get; set; }
    public NetworkConnection Connection { get; set; }
}