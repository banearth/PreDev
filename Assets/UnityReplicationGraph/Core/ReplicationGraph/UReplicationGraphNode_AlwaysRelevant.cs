using System.Collections.Generic;

public class UReplicationGraphNode_AlwaysRelevant : UReplicationGraphNode
{
    // 子节点，用于实际存储Actor
    protected UReplicationGraphNode ChildNode;

    // 始终相关的类型列表
    protected List<System.Type> AlwaysRelevantClasses = new List<System.Type>();

    public UReplicationGraphNode_AlwaysRelevant()
    {
        // 创建一个ActorList节点作为子节点
        ChildNode = CreateChildNode<UReplicationGraphNode_ActorList>();
    }

    // 添加始终相关的类型
    public void AddAlwaysRelevantClass(System.Type classType)
    {
        if (!AlwaysRelevantClasses.Contains(classType))
        {
            AlwaysRelevantClasses.Add(classType);
        }
    }

    public override void NotifyAddNetworkActor(FNewReplicatedActorInfo ActorInfo)
    {
        // 空实现，因为我们不直接管理Actor
    }

    public override bool NotifyRemoveNetworkActor(FNewReplicatedActorInfo ActorInfo)
    {
        // 空实现
        return false;
    }

    public override void NotifyResetAllNetworkActors()
    {
        // 空实现
    }

    public override void PrepareForReplication()
    {
        if (ChildNode == null)
        {
            ChildNode = CreateChildNode<UReplicationGraphNode_ActorList>();
        }

        // 重置子节点的Actor列表
        ChildNode.NotifyResetAllNetworkActors();

        // 对每个始终相关的类型
        foreach (var actorClass in AlwaysRelevantClasses)
        {
            // 从World中获取该类型的所有Actor
            var actors = GetWorld().GetAllActorsOfType(actorClass);
            foreach (var actor in actors)
            {
                // 检查Actor是否有效
                if (ReplicationGraphUtils.IsActorValidForReplication(actor))
                {
                    ChildNode.NotifyAddNetworkActor(new FNewReplicatedActorInfo(actor));
                }
            }
        }
    }

    public override void GatherActorListsForConnection(FConnectionGatherActorListParameters Params)
    {
        // 直接将子节点的所有Actor添加到结果中
        if (ChildNode != null)
        {
            ChildNode.GatherActorListsForConnection(Params);
        }
    }
}
