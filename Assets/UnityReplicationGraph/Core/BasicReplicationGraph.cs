using UnityEngine;

public class BasicReplicationGraph : ReplicationGraph
{
    // 参考 BasicReplicationGraph.h 第59-63行
    public GridSpatialization2D GridNode { get; private set; }
    public ActorListGraphNode AlwaysRelevantNode { get; private set; }

    protected override void InitGlobalGraphNodes()
    {
        // 参考 BasicReplicationGraph.cpp 第62-79行
        GridNode = new GridSpatialization2D();
        GridNode.CellSize = 10000f;
        GridNode.SpatialBias = new Vector2(-100000f, -100000f);

        AddGlobalGraphNode(GridNode);

        AlwaysRelevantNode = new ActorListGraphNode();
        AddGlobalGraphNode(AlwaysRelevantNode);
    }

    protected override void InitGlobalActorClassSettings()
    {
        // 参考 BasicReplicationGraph.cpp 第19-60行
        base.InitGlobalActorClassSettings();

        // 这里可以添加特定的Actor类设置
    }

    public override void RouteAddNetworkActorToNodes(ReplicatedActorInfo actorInfo, GlobalActorReplicationInfo globalInfo)
    {
        // 参考 BasicReplicationGraph.cpp 第91-100行
        GridNode.NotifyAddNetworkActor(actorInfo);
    }

    public override void RouteRemoveNetworkActorToNodes(ReplicatedActorInfo actorInfo)
    {
        GridNode.NotifyRemoveNetworkActor(actorInfo);
        AlwaysRelevantNode?.NotifyRemoveNetworkActor(actorInfo);
    }
}