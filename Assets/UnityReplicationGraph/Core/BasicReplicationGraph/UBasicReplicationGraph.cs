using UnityEngine;
using System.Collections.Generic;
using UnityEditor.PackageManager;

public class UBasicReplicationGraph : UReplicationGraph
{
    // 空间网格节点
    protected UReplicationGraphNode_GridSpatialization2D GridNode { get; private set; }

    // 始终相关的Actor节点
    protected UReplicationGraphNode_ActorList AlwaysRelevantNode { get; private set; }

    // 每个连接的始终相关节点列表
    private List<FConnectionAlwaysRelevantNodePair> AlwaysRelevantForConnectionList = new List<FConnectionAlwaysRelevantNodePair>();

    // 没有网络连接的Actor列表
    private List<FActorRepListType> ActorsWithoutNetConnection = new List<FActorRepListType>();

    public UBasicReplicationGraph()
    {
    }

    public override void InitGlobalActorClassSettings()
    {
        base.InitGlobalActorClassSettings();
		// 注册各种类型的复制设置
		RegisterReplicationType("Default", new FClassReplicationInfo
		{
			ReplicationPeriodFrame = 60,
			CullDistanceSquared = 0f
		});
		RegisterReplicationType("Character", new FClassReplicationInfo
        {
            ReplicationPeriodFrame = 60,
            CullDistanceSquared = 0f
        });
        RegisterReplicationType("Item", new FClassReplicationInfo
        {
            ReplicationPeriodFrame = 20,
            CullDistanceSquared = 5000f * 5000f
        });
        RegisterReplicationType("Projectile", new FClassReplicationInfo
        {
            ReplicationPeriodFrame = 30,
            CullDistanceSquared = 2000f * 2000f
        });
    }

    private void RegisterReplicationType(string replicationType, FClassReplicationInfo classInfo)
    {
        GlobalActorReplicationInfoMap.SetClassInfo(replicationType, classInfo);
    }

    protected override void InitGlobalGraphNodes()
    {
        // 创建空间网格节点
        GridNode = CreateNewNode<UReplicationGraphNode_GridSpatialization2D>();
        GridNode.CellSize = 10000f;
        GridNode.SpatialBias = new Vector2(-100000f, -100000f);
        AddGlobalGraphNode(GridNode);

        // 创建始终相关节点
        AlwaysRelevantNode = CreateNewNode<UReplicationGraphNode_ActorList>();
        AddGlobalGraphNode(AlwaysRelevantNode);
    }

    protected override void InitConnectionGraphNodes(UNetReplicationGraphConnection repGraphConnection)
    {
        base.InitConnectionGraphNodes(repGraphConnection);

        // 为每个连接创建始终相关节点
        var alwaysRelevantNodeForConnection = CreateNewNode<UReplicationGraphNode_AlwaysRelevant_ForConnection>();
        AddConnectionGraphNode(alwaysRelevantNodeForConnection, repGraphConnection);
        
        AlwaysRelevantForConnectionList.Add(new FConnectionAlwaysRelevantNodePair(
            repGraphConnection.NetConnection, 
            alwaysRelevantNodeForConnection));
    }

    public override void RouteAddNetworkActorToNodes(FNewReplicatedActorInfo actorInfo, FGlobalActorReplicationInfo globalInfo)
    {
        if (actorInfo.Actor.bAlwaysRelevant)
        {
            AlwaysRelevantNode.NotifyAddNetworkActor(actorInfo);
        }
        else if (actorInfo.Actor.bOnlyRelevantToOwner)
        {
			ActorsWithoutNetConnection.Add(actorInfo.Actor);
		}
        else
        {
            GridNode.NotifyAddNetworkActor(actorInfo);
        }
    }

    public override void RouteRemoveNetworkActorToNodes(FNewReplicatedActorInfo actorInfo)
    {
        GridNode.NotifyRemoveNetworkActor(actorInfo);
        AlwaysRelevantNode.NotifyRemoveNetworkActor(actorInfo);
        ActorsWithoutNetConnection.Remove(actorInfo.Actor);

        foreach (var pair in AlwaysRelevantForConnectionList)
        {
            pair.Node.NotifyRemoveNetworkActor(actorInfo);
        }
    }

    protected UReplicationGraphNode_AlwaysRelevant_ForConnection GetAlwaysRelevantNodeForConnection(UNetConnection connection)
	{
        var pair = AlwaysRelevantForConnectionList.Find(x => x.NetConnection == connection);
        return pair?.Node;
    }

    public override int ServerReplicateActors(float deltaSeconds)
    {
        // 处理需要网络连接的Actor到适当的节点
        for (int idx = ActorsWithoutNetConnection.Count - 1; idx >= 0; --idx)
        {
            bool shouldRemove = false;
            var actor = ActorsWithoutNetConnection[idx];
            if (actor != null)
            {
                var connection = actor.GetNetConnection();
                if (connection != null)
                {
                    shouldRemove = true;
                    var node = GetAlwaysRelevantNodeForConnection(connection);
                    if (node != null)
                    {
                        node.NotifyAddNetworkActor(new FNewReplicatedActorInfo(actor));
                    }
                }
            }
            else
            {
                shouldRemove = true;
            }
            if (shouldRemove)
            {
                ActorsWithoutNetConnection.RemoveAtSwap(idx);
            }
        }
        return base.ServerReplicateActors(deltaSeconds);
    }
}