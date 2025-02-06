using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UReplicationGraphNode_ConnectionDormancyNode : UReplicationGraphNode_ActorList
{
    private static uint NumFramesUntilObsolete;
    private FRepGraphConnectionKey ConnectionOwner;
    private uint LastGatheredFrame = 0;
    private int TrickleStartCounter = 10;
    private FStreamingLevelActorListCollection RemovedStreamingLevelActorListCollection;

    public void InitConnectionNode(FRepGraphConnectionKey connectionOwner, uint currentFrame)
    {
        ConnectionOwner = connectionOwner;
        LastGatheredFrame = currentFrame;
    }

    public override void TearDown()
    {
        base.TearDown();
        RemovedStreamingLevelActorListCollection.Reset();
    }

    public override void GatherActorListsForConnection(FConnectionGatherActorListParameters Params)
    {
        LastGatheredFrame = Params.ReplicationFrameNum;
		// 收集主要的休眠Actor列表
		ConditionalGatherDormantActorsForConnection(ReplicationActorList, Params, null);
        // 处理流关卡集合
        for (int idx = StreamingLevelCollection.StreamingLevelLists.Count - 1; idx >= 0; --idx)
        {
            var streamingList = StreamingLevelCollection.StreamingLevelLists[idx];
            if (streamingList.ReplicationActorList.Num() <= 0)
            {
                StreamingLevelCollection.StreamingLevelLists.RemoveAtSwap(idx);
                continue;
            }
            // 检查客户端是否可见此关卡
            if (Params.CheckClientVisibilityForLevel(streamingList.StreamingLevelName))
            {
				// 查找或创建移除列表
				FStreamingLevelActorListCollection.FStreamingLevelActors removeList = null;
                int removeIdx = RemovedStreamingLevelActorListCollection.StreamingLevelLists
                    .FindIndex(x => x.StreamingLevelName == streamingList.StreamingLevelName);

                if (removeIdx == -1)
                {
                    removeList = new FStreamingLevelActorListCollection.FStreamingLevelActors(streamingList.StreamingLevelName);
                    RemovedStreamingLevelActorListCollection.StreamingLevelLists.Add(removeList);
                }
                else
                {
                    removeList = RemovedStreamingLevelActorListCollection.StreamingLevelLists[removeIdx];
                }

                // 收集此关卡的休眠Actor
                ConditionalGatherDormantActorsForConnection(
                    streamingList.ReplicationActorList, 
                    Params, 
                    removeList.ReplicationActorList);
            }
            else
            {
                ReplicationGraphDebugger.LogInfo($"Level Not Loaded {streamingList.StreamingLevelName}. " +
					$"(Client has {Params.ClientVisibleLevelNamesRef.Count} levels loaded)");
            }
        }

        // 如果有Actor要复制，添加到输出列表
        if (ReplicationActorList.Num() > 0)
        {
            Params.OutGatheredReplicationLists.AddReplicationActorList(ReplicationActorList);
        }
    }

    public override bool NotifyRemoveNetworkActor(FNewReplicatedActorInfo actorInfo, bool warnIfNotFound)
    {
        return false;
    }

    public override void NotifyResetAllNetworkActors()
    {
        ReplicationActorList.Reset();
    }

    protected override void VerifyActorReferencesInternal()
    {
        base.VerifyActorReferencesInternal();
        foreach (var actor in ReplicationActorList)
        {
            VerifyActorReference(actor);
        }
    }

    public void OnClientVisibleLevelNameAdd(string LevelName, UWorld world)
    {
		var RemoveList = RemovedStreamingLevelActorListCollection.StreamingLevelLists.Find(temp => temp.StreamingLevelName == LevelName);
		if (RemoveList == null)
		{
            ReplicationGraphDebugger.LogWarning("OnClientVisibleLevelNameAdd called but there is no RemoveList");
			return;
		}
		var AddList = StreamingLevelCollection.StreamingLevelLists.Find(temp => temp.StreamingLevelName == LevelName);
		if (AddList == null)
		{
            AddList = new FStreamingLevelActorListCollection.FStreamingLevelActors(LevelName);
			StreamingLevelCollection.StreamingLevelLists.Add(AddList);
		}
		AddList.ReplicationActorList.AppendContentsFrom(RemoveList.ReplicationActorList);
		RemoveList.ReplicationActorList.Reset();
	}

    public bool IsNodeObsolete(uint currentFrame)
    {
        return (currentFrame - LastGatheredFrame) > NumFramesUntilObsolete;
    }

    private void ConditionalGatherDormantActorsForConnection(FActorRepListRefView connectionList, 
        FConnectionGatherActorListParameters parameters, FActorRepListRefView removedList)
    {
        var connectionActorInfoMap = parameters.ConnectionManager.ActorInfoMap;
        var globalActorReplicationInfoMap = GraphGlobals.GlobalActorReplicationInfoMap;

        // 我们可以在TrickleStartCounter为0时进行trickle
        // (只是尝试给它几帧时间来稳定)
        bool shouldTrickle = TrickleStartCounter == 0;

        for (int idx = connectionList.Num() - 1; idx >= 0; --idx)
        {
            var actor = connectionList[idx];
			if(!ReplicationGraphDebugger.EnsureMsg(ReplicationGraphUtils.IsActorValidForReplication(actor), "Actor not valid for replication"))
			{
				continue;
			}
            var connectionActorInfo = connectionActorInfoMap.FindOrAdd(actor);
            if (connectionActorInfo.bDormantOnConnection)
            {
                // 如果我们trickle了这个actor，它不再需要始终相关
                connectionActorInfo.bForceCullDistanceToZero = false;

                // 可以移除它
                connectionList.RemoveAtSwap(idx);
                if (removedList != null)
                {
                    removedList.Add(actor);
                }

                if (ReplicationGraphDebugger.CVar_RepGraph_LogNetDormancyDetails > 0)
                {
					ReplicationGraphDebugger.LogInfo($"GRAPH_DORMANCY: Actor {actor.Name} is Dormant on {nodeName}. " +
                        $"Removing from list. ({connectionList.Num()} elements left)");
                }

                shouldTrickle = false; // 这一帧不要trickle，因为我们仍在遇到休眠的actor
            }
            else if (ReplicationGraphDebugger.CVar_RepGraph_TrickleDistCullOnDormancyNodes > 0 && 
                    shouldTrickle && 
                    connectionActorInfo.GetCullDistanceSquared() > 0.0f)
            {
                connectionActorInfo.bForceCullDistanceToZero = true;
                shouldTrickle = false; // 每帧trickle一个actor
            }
        }

        if (connectionList.Num() > 0)
        {
            parameters.OutGatheredReplicationLists.AddReplicationActorList(connectionList);
            
            if (TrickleStartCounter > 0)
            {
                TrickleStartCounter--;
            }
        }
    }

    public static void SetNumFramesUntilObsolete(uint numFrames)
    {
        NumFramesUntilObsolete = numFrames;
    }

	public void NotifyActorDormancyFlush(FActorRepListType actor)
	{
		var actorInfo = new FNewReplicatedActorInfo(actor);
		// 处理非流关卡Actor
		if (ReplicationGraphUtils.IsLevelNameNone(actorInfo.StreamingLevelName))
		{
			// 避免重复添加
			if (!ReplicationActorList.Contains(actor))
			{
				ReplicationActorList.Add(actor);
			}
		}
		// 处理流关卡Actor
		else
		{
			// 尝试找到对应的流关卡列表
			var levelActors = StreamingLevelCollection.StreamingLevelLists
				.FirstOrDefault(x => x.StreamingLevelName == actorInfo.StreamingLevelName);

			if (levelActors == null)
			{
				// 如果不存在，创建新的流关卡列表
				levelActors = new FStreamingLevelActorListCollection.FStreamingLevelActors(actorInfo.StreamingLevelName);
				StreamingLevelCollection.StreamingLevelLists.Add(levelActors);
				levelActors.ReplicationActorList.Add(actor);
			}
			else if (!levelActors.ReplicationActorList.Contains(actor))
			{
				// 如果存在但Actor不在列表中，添加它
				levelActors.ReplicationActorList.Add(actor);
			}

			// 从移除列表中清除该Actor
			var removeList = RemovedStreamingLevelActorListCollection.StreamingLevelLists
				.FirstOrDefault(x => x.StreamingLevelName == actorInfo.StreamingLevelName);
			if (removeList != null)
			{
				removeList.ReplicationActorList.Remove(actor);
			}
		}
	}

}