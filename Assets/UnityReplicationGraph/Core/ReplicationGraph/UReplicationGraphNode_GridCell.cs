using System.Collections.Generic;
using UnityEngine;

public class UReplicationGraphNode_GridCell : UReplicationGraphNode_ActorList
{
    
    private int coordX;
    private int coordY;

	public UReplicationGraphNode_GridCell(int x, int y)
	{
        coordX = x;
        coordY = y;
		nodeName = string.Format("GridCell({0},{1})", coordX.ToString(), coordY.ToString());
	}

	// 动态Actor节点
	private UReplicationGraphNode_ActorListFrequencyBuckets DynamicNode;

    // 休眠Actor节点
    private UReplicationGraphNode_DormancyNode DormancyNode;

    public override void NotifyAddNetworkActor(FNewReplicatedActorInfo actorInfo)
    {
        ReplicationGraphDebugger.LogError("UReplicationGraphNode_GridCell::NotifyAddNetworkActor should not be called directly");
    }

    public override bool NotifyRemoveNetworkActor(FNewReplicatedActorInfo actorInfo)
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
		if (ReplicationGraphDebugger.CVar_Track_AddOrRemoveNode)
		{
			Debug.Log(string.Format("{0}::AddDynamicActor Name:{1}", nodeName, actorInfo.Actor.Name));			
		}
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
		if (ReplicationGraphDebugger.CVar_Track_AddOrRemoveNode)
		{
			Debug.Log(string.Format("{0}::RemoveDynamicActor Name:{1}", nodeName, actorInfo.Actor.Name));			
		}
		GetDynamicNode().NotifyRemoveNetworkActor(actorInfo);
    }

    private UReplicationGraphNode_ActorListFrequencyBuckets GetDynamicNode()
    {
        if (DynamicNode == null)
        {
            DynamicNode = CreateChildNode<UReplicationGraphNode_ActorListFrequencyBuckets>();   
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

    public override int GetActorCount()
    {
        int dynamicCount = DynamicNode != null ? DynamicNode.GetActorCount() : 0;
        int dormantCount = DormancyNode != null ? DormancyNode.GetActorCount() : 0;
        return dynamicCount + dormantCount;
    }

	public override string GetDebugString()
	{
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        // 添加格子基本信息
        sb.AppendLine($"Grid Cell ({coordX},{coordY})");
        
        //// 添加静态Actor信息
        //var staticActors = GetActors();
        //if (staticActors.Count > 0)
        //{
        //    sb.AppendLine("Static Actors:");
        //    foreach (var actor in staticActors)
        //    {
        //        sb.AppendLine($"  - {actor.Name}");
        //    }
        //}

        // 添加动态Actor信息
        if (DynamicNode != null)
        {
            var dynamicActors = new List<FActorRepListType>();
            DynamicNode.GetAllActorsInNode_Debugging(dynamicActors);
            if (dynamicActors.Count > 0)
            {
                sb.AppendLine("Dynamic Actors:");
                foreach (var actor in dynamicActors)
                {
                    sb.AppendLine($"  - {actor.Name}");
                }
            }
        }

        // 添加休眠Actor信息
        if (DormancyNode != null)
        {
            var dormantActors = new List<FActorRepListType>();
            DormancyNode.GetAllActorsInNode_Debugging(dormantActors);
            if (dormantActors.Count > 0)
            {
                sb.AppendLine("Dormant Actors:");
                foreach (var actor in dormantActors)
                {
                    sb.AppendLine($"  - {actor.Name}");
                }
            }
        }

        // 添加总计信息
        int totalCount = GetActorCount();
        sb.AppendLine($"Total Actors: {totalCount}");

        return sb.ToString();
	}

}