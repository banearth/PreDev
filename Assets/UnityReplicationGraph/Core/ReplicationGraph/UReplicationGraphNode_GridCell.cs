using System.Collections.Generic;

public class UReplicationGraphNode_GridCell : UReplicationGraphNode_ActorList
{
    // 动态Actor节点
    private UReplicationGraphNode DynamicNode;

    // 休眠Actor节点
    private UReplicationGraphNode_DormancyNode DormancyNode;

    // 允许图表重写创建动态节点的函数
    public System.Func<UReplicationGraphNode_GridCell, UReplicationGraphNode> CreateDynamicNodeOverride;

    public override void NotifyAddNetworkActor(FNewReplicatedActorInfo actorInfo)
    {
        ReplicationGraphDebugger.LogError("UReplicationGraphNode_GridCell::NotifyAddNetworkActor should not be called directly");
    }

    public override bool NotifyRemoveNetworkActor(FNewReplicatedActorInfo actorInfo, bool bWarnIfNotFound = true)
    {
        ReplicationGraphDebugger.LogError("UReplicationGraphNode_GridCell::NotifyRemoveNetworkActor should not be called directly");
        return false;
    }

    public override void LogNode(FReplicationGraphDebugInfo debugInfo, string nodeName)
    {
        debugInfo.Log(nodeName);
        
        // 记录动态节点
        if (DynamicNode != null)
        {
            debugInfo.PushIndent();
            DynamicNode.LogNode(debugInfo, "DynamicNode");
            debugInfo.PopIndent();
        }

        // 记录休眠节点
        if (DormancyNode != null)
        {
            debugInfo.PushIndent();
            DormancyNode.LogNode(debugInfo, "DormancyNode");
            debugInfo.PopIndent();
        }

        base.LogNode(debugInfo, nodeName);
    }

    public override void GetAllActorsInNode_Debugging(List<FActorRepListType> outArray)
    {
        base.GetAllActorsInNode_Debugging(outArray);
        
        if (DynamicNode != null)
        {
            DynamicNode.GetAllActorsInNode_Debugging(outArray);
        }
        
        if (DormancyNode != null)
        {
            DormancyNode.GetAllActorsInNode_Debugging(outArray);
        }
    }

    private void OnActorDormancyFlush(FActorRepListType actor, FGlobalActorReplicationInfo globalInfo, UReplicationGraphNode_DormancyNode dormancyNode)
    {
        if (dormancyNode == DormancyNode)
        {
            var actorInfo = new FNewReplicatedActorInfo(actor);
            dormancyNode.RemoveDormantActor(actorInfo, globalInfo);
            base.NotifyAddNetworkActor(actorInfo);
        }
    }

    private void ConditionalCopyDormantActors(FActorRepListRefView fromList, UReplicationGraphNode_DormancyNode toNode)
    {
        if(GraphGlobals != null)
        {
			foreach (var actor in fromList)
			{
				if (actor.NetDormancy > ENetDormancy.DORM_Awake)
				{
					var actorInfo = new FNewReplicatedActorInfo(actor);
					var globalInfo = GraphGlobals.GlobalActorReplicationInfoMap.Get(actor);
					toNode.AddDormantActor(actorInfo, globalInfo);
				}
			}
		}
    }

    public void AddStaticActor(FNewReplicatedActorInfo actorInfo, FGlobalActorReplicationInfo actorRepInfo, bool bParentNodeHandlesDormancyChange)
    {
        if (actorRepInfo.bWantsToBeDormant)
        {
            // 添加到休眠节点
            GetDormancyNode().AddDormantActor(actorInfo, actorRepInfo);
        }
        else
        {
            // 添加到非休眠列表
            base.NotifyAddNetworkActor(actorInfo);
        }

        // 如果父节点不处理休眠变化，我们需要监听休眠状态变化
        if (!bParentNodeHandlesDormancyChange)
        {
            actorRepInfo.Events.DormancyChange += OnStaticActorNetDormancyChange;
        }
    }

    public void AddDynamicActor(FNewReplicatedActorInfo actorInfo)
    {
        GetDynamicNode().NotifyAddNetworkActor(actorInfo);
    }

    public void RemoveStaticActor(FNewReplicatedActorInfo actorInfo, FGlobalActorReplicationInfo actorRepInfo, bool bWasAddedAsDormantActor)
    {
        if (bWasAddedAsDormantActor)
        {
            GetDormancyNode().RemoveDormantActor(actorInfo, actorRepInfo);
        }
        else
        {
            base.NotifyRemoveNetworkActor(actorInfo);
        }
        actorRepInfo.Events.DormancyChange -= OnStaticActorNetDormancyChange;
    }

    public void RemoveDynamicActor(FNewReplicatedActorInfo actorInfo)
    {
        GetDynamicNode().NotifyRemoveNetworkActor(actorInfo);
    }

    public void RenameStaticActor(FRenamedReplicatedActorInfo actorInfo, bool bWasAddedAsDormantActor)
    {
        if (bWasAddedAsDormantActor)
        {
            GetDormancyNode().RenameDormantActor(actorInfo);
        }
        else
        {
            base.NotifyRemoveNetworkActor(actorInfo.OldActorInfo);
            base.NotifyAddNetworkActor(actorInfo.NewActorInfo);
        }
    }

    public void RenameDynamicActor(FRenamedReplicatedActorInfo actorInfo)
    {
        GetDynamicNode().NotifyActorRenamed(actorInfo);
    }

    private UReplicationGraphNode GetDynamicNode()
    {
        if (DynamicNode == null)
        {
            if (CreateDynamicNodeOverride != null)
            {
                DynamicNode = CreateDynamicNodeOverride(this);
            }
            else
            {
                DynamicNode = CreateChildNode<UReplicationGraphNode_ActorListFrequencyBuckets>();
            }
        }
        return DynamicNode;
    }

    public UReplicationGraphNode_DormancyNode GetDormancyNode(bool bInCreateIfMissing = true)
    {
        if (DormancyNode == null && bInCreateIfMissing)
        {
            DormancyNode = CreateChildNode<UReplicationGraphNode_DormancyNode>();
        }
        return DormancyNode;
    }

    private void OnStaticActorNetDormancyChange(FActorRepListType actor, FGlobalActorReplicationInfo globalInfo, ENetDormancy newValue, ENetDormancy oldValue)
    {
        bool bCurrentDormant = newValue > ENetDormancy.DORM_Awake;
        bool bPreviousDormant = oldValue > ENetDormancy.DORM_Awake;

        if (!bCurrentDormant && bPreviousDormant)
        {
            // Actor现在醒来，从休眠节点移除并添加到非休眠列表
            var actorInfo = new FNewReplicatedActorInfo(actor);
            GetDormancyNode().RemoveDormantActor(actorInfo, globalInfo);
            base.NotifyAddNetworkActor(actorInfo);
        }
        else if (bCurrentDormant && !bPreviousDormant)
        {
            // Actor现在休眠，从非休眠列表移除并添加到休眠节点
            var actorInfo = new FNewReplicatedActorInfo(actor);
            base.NotifyRemoveNetworkActor(actorInfo);
            GetDormancyNode().AddDormantActor(actorInfo, globalInfo);
        }
    }
}