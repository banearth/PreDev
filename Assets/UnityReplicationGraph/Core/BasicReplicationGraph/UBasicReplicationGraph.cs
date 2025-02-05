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

        // 默认类型 - 中等距离
        RegisterReplicationType("Default", new FClassReplicationInfo()
            .SetReplicationPeriodFrame(60)    // 1秒更新一次
            .SetCullDistance(40));          // 适中的裁剪距离

        // 角色 - 需要较大的可见距离，因为是主要交互对象
        RegisterReplicationType("Character", new FClassReplicationInfo()
            .SetReplicationPeriodFrame(30)    // 0.5秒更新一次，更频繁
            .SetCullDistance(60)           // 较大的可见距离
            .SetDistancePriorityScale(2.0f)); // 更高的距离优先级

        // 物品 - 中等距离，因为需要让玩家看到可以拾取的物品
        RegisterReplicationType("Item", new FClassReplicationInfo()
            .SetReplicationPeriodFrame(45)    // 0.75秒更新一次
            .SetCullDistance(30));          // 中等可见距离

        // 投射物 - 较小的距离，因为移动快且生命周期短
        RegisterReplicationType("Projectile", new FClassReplicationInfo()
            .SetReplicationPeriodFrame(20)    // 0.33秒更新一次，需要较高频率
            .SetCullDistance(20));          // 较小的可见距离
    }

    private void RegisterReplicationType(string replicationType, FClassReplicationInfo classInfo)
    {
        GlobalActorReplicationInfoMap.SetClassInfo(replicationType, classInfo);
    }

    protected override void InitGlobalGraphNodes()
    {
        // 创建空间网格节点
        GridNode = CreateNewNode<UReplicationGraphNode_GridSpatialization2D>();
        GridNode.CellSize = 10f;
        GridNode.SpatialBias = new Vector2(-100f, -100f);
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
			// 注意 UReplicationGraphNode_GridSpatialization2D 有3种添加Actor的方法，这取决于Actor的移动性(mobility)。
			// 由于 AActor 缺少这些移动性信息，我们将所有需要空间化的Actor都作为dormant(休眠)Actor添加：
			// 这意味着当它们不处于休眠状态时会被视为可能是动态的(可移动的)，
			// 而当它们处于休眠状态时会被视为静态的(不可移动的)。
			GridNode.AddActor_Dormancy(actorInfo, globalInfo);
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