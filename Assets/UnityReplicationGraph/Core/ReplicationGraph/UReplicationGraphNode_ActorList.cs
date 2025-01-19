using System.Collections.Generic;

public class UReplicationGraphNode_ActorList : UReplicationGraphNode
{
    // 基础的Actor复制列表
    protected FActorRepListRefView ReplicationActorList = new();

    // 用于流关卡Actor的列表集合
    protected FStreamingLevelActorListCollection StreamingLevelCollection = new();

    public override void NotifyAddNetworkActor(FNewReplicatedActorInfo actorInfo)
    {
        if (actorInfo.StreamingLevelName == string.Empty)
        {
            ReplicationActorList.Add(actorInfo.Actor);
        }
        else
        {
            StreamingLevelCollection.AddActor(actorInfo);
        }
    }

    public override bool NotifyRemoveNetworkActor(FNewReplicatedActorInfo actorInfo, bool bWarnIfNotFound = true)
    {
        if (actorInfo.StreamingLevelName == string.Empty)
        {
            return ReplicationActorList.RemoveFast(actorInfo.Actor);
        }
        
        return StreamingLevelCollection.RemoveActorFast(actorInfo);
    }

    public override void NotifyResetAllNetworkActors()
    {
        ReplicationActorList.Reset();
        StreamingLevelCollection.Reset();
    }

	public override void GatherActorListsForConnection(FConnectionGatherActorListParameters Params)
	{
		Params.OutGatheredReplicationLists.AddReplicationActorList(ReplicationActorList);
		StreamingLevelCollection.Gather(Params);
		foreach (var ChildNode in AllChildNodes)
		{
			ChildNode.GatherActorListsForConnection(Params);
		}
	}

	public override void LogNode(FReplicationGraphDebugInfo debugInfo, string nodeName)
	{
		base.LogNode(debugInfo, nodeName);
		LogActorList(debugInfo);


		//haha
		//public override void LogNode(FReplicationGraphDebugInfo debugInfo, string nodeName)
		//{
		//	debugInfo.Log($"{nodeName} Dormant Actors: {DormantActors.Count}");
		//	if (debugInfo.DetailedOutput)
		//	{
		//		debugInfo.PushIndent();
		//		foreach (var actor in DormantActors)
		//		{
		//			debugInfo.Log(actor.GetDebugString());
		//		}
		//		debugInfo.PopIndent();
		//	}
		//}
	}

	public override void GetAllActorsInNode_Debugging(List<FActorRepListType> outArray)
    {
		ReplicationActorList.AppendToTArray(outArray);
		StreamingLevelCollection.GetAll_Debug(outArray);
		foreach(var ChildNode in AllChildNodes)
		{
			ChildNode.GetAllActorsInNode_Debugging(outArray);
		}
    }

	protected override void VerifyActorReferencesInternal()
	{
		base.VerifyActorReferencesInternal();
		foreach(var Actor in ReplicationActorList)
		{
			VerifyActorReference(Actor);
		}
		foreach (var Item in StreamingLevelCollection.StreamingLevelLists)
		{
			foreach(var Actor in Item.ReplicationActorList)
			{
				VerifyActorReference(Actor);
			}
		}
	}

	public override void TearDown()
    {
        base.TearDown();
        ReplicationActorList.Reset();
        StreamingLevelCollection.Reset();
    }

    protected bool RemoveNetworkActorFast(FNewReplicatedActorInfo actorInfo)
    {
        return NotifyRemoveNetworkActor(actorInfo, false);
    }

	public void DeepCopyActorListsFrom(UReplicationGraphNode_ActorList Source)
    {
        if(Source.ReplicationActorList.Num() > 0)
        {
            ReplicationActorList.CopyContentsFrom(Source.ReplicationActorList);
        }
        StreamingLevelCollection.DeepCopyFrom(Source.StreamingLevelCollection);
    }

    protected void LogActorList(FReplicationGraphDebugInfo DebugInfo)
    {
		ReplicationGraphDebugger.LogActorRepList(DebugInfo,"World", ReplicationActorList);
		StreamingLevelCollection.Log(DebugInfo);
    }

	protected override void OnCollectActorRepListStats(FActorRepListStatCollector StatsCollector)
    {
	    StatsCollector.VisitRepList(this, ReplicationActorList);
	    StatsCollector.VisitStreamingLevelCollection(this, StreamingLevelCollection);
	    base.OnCollectActorRepListStats(StatsCollector);
    }
}