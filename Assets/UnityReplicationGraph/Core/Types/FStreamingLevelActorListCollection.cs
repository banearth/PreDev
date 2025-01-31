using System.Collections.Generic;

/// <summary>
/// 按StreamingLevel分组的Actor列表集合
/// </summary>
public class FStreamingLevelActorListCollection
{
    /// <summary>
    /// 单个StreamingLevel的Actor列表
    /// </summary>
    public class FStreamingLevelActors
    {
        public string StreamingLevelName { get; private set; }
        public FActorRepListRefView ReplicationActorList { get; private set; }

        public FStreamingLevelActors(string inName)
        {
            StreamingLevelName = inName;
            ReplicationActorList = new FActorRepListRefView();
        }
    }

    // StreamingLevel列表,使用List模拟UE的TArray
    public List<FStreamingLevelActors> StreamingLevelLists = new List<FStreamingLevelActors>();

    /// <summary>
    /// 添加Actor到对应的StreamingLevel列表
    /// </summary>
    public void AddActor(FNewReplicatedActorInfo ActorInfo)
    {
        var item = StreamingLevelLists.Find(x => x.StreamingLevelName == ActorInfo.StreamingLevelName);
        if (item == null)
        {
            item = new FStreamingLevelActors(ActorInfo.StreamingLevelName);
            StreamingLevelLists.Add(item);
        }
        if (ReplicationGraphDebugger.CVar_RepGraph_Verify)
        {
            ReplicationGraphDebugger.EnsureMsg(!item.ReplicationActorList.Contains(ActorInfo.Actor), $"AddActor failed: {ActorInfo.Actor} already exists in {ActorInfo.StreamingLevelName}");
        }
        item.ReplicationActorList.Add(ActorInfo.Actor);
    }

    /// <summary>
    /// 从列表中移除Actor(保持列表顺序)
    /// </summary>
    public bool RemoveActor(FNewReplicatedActorInfo ActorInfo, bool bWarnIfNotFound)
    {
        bool bRemovedSomething = false;
        foreach (var streamingList in StreamingLevelLists)
        {
            if (streamingList.StreamingLevelName == ActorInfo.StreamingLevelName)
            {
                bRemovedSomething = streamingList.ReplicationActorList.RemoveSlow(ActorInfo.Actor);
                
                if (!bRemovedSomething && bWarnIfNotFound)
                {
                    ReplicationGraphDebugger.LogWarning(
                        $"Attempted to remove {ActorInfo.Actor} but it was not found. " +
                        $"(StreamingLevelName == {ActorInfo.StreamingLevelName})");
                }

                if (ReplicationGraphDebugger.CVar_RepGraph_Verify)
                {
                    ReplicationGraphDebugger.EnsureMsg(!streamingList.ReplicationActorList.Contains(ActorInfo.Actor), 
                        $"Actor {ActorInfo.Actor} is still in list after removal");
                }
                break;
            }
        }
        return bRemovedSomething;
    }

    /// <summary>
    /// 快速移除Actor(不保持列表顺序)
    /// </summary>
    public bool RemoveActorFast(FNewReplicatedActorInfo ActorInfo, UObject Outer = null)
    {
        bool bRemovedSomething = false;
        foreach (var streamingList in StreamingLevelLists)
        {
            if (streamingList.StreamingLevelName == ActorInfo.StreamingLevelName)
            {
                bRemovedSomething = streamingList.ReplicationActorList.RemoveFast(ActorInfo.Actor);
                break;
            }
        }
        return bRemovedSomething;
    }

    /// <summary>
    /// 从指定Level中快速移除Actor
    /// </summary>
    public bool RemoveActorFromLevelFast(FActorRepListType Actor, string LevelName)
    {
        bool bRemovedSomething = false;
        foreach (var streamingList in StreamingLevelLists)
        {
            if (streamingList.StreamingLevelName == LevelName)
            {
                bRemovedSomething = streamingList.ReplicationActorList.RemoveFast(Actor);
                break;
            }
        }
        return bRemovedSomething;
    }

    /// <summary>
    /// 重置所有列表
    /// </summary>
    public void Reset()
    {
        foreach (var streamingList in StreamingLevelLists)
        {
            streamingList.ReplicationActorList.Reset();
        }
    }

    /// <summary>
    /// 收集对此连接可见的Actor列表
    /// </summary>
    public void Gather(FConnectionGatherActorListParameters Params)
    {
        foreach (var streamingList in StreamingLevelLists)
        {
            if (Params.CheckClientVisibilityForLevel(streamingList.StreamingLevelName))
            {
                Params.OutGatheredReplicationLists.AddReplicationActorList(streamingList.ReplicationActorList);
            }
            else
            {
                ReplicationGraphDebugger.LogInfo($"Level Not Loaded {streamingList.StreamingLevelName}. " +
                    $"(Client has {Params.ClientVisibleLevelNamesRef.Count} levels loaded)");
            }
        }
    }

    /// <summary>
    /// 收集对此连接可见的Actor列表
    /// </summary>
    public void Gather(UNetReplicationGraphConnection ConnectionManager, FGatheredReplicationActorLists OutGatheredList)
    {
        var ClientLevelNames = ConnectionManager.GetCachedClientVisibleLevelNames();

        foreach (var streamingList in StreamingLevelLists)
        {
            if (ClientLevelNames.Contains(streamingList.StreamingLevelName))
            {
                OutGatheredList.AddReplicationActorList(streamingList.ReplicationActorList);
            }
        }
    }

    /// <summary>
    /// 添加所有列表到输出列表
    /// </summary>
    public void AppendAllLists(FGatheredReplicationActorLists OutGatheredList)
    {
        foreach (var streamingList in StreamingLevelLists)
        {
            OutGatheredList.AddReplicationActorList(streamingList.ReplicationActorList);
        }
    }

    /// <summary>
    /// 检查是否包含指定Actor
    /// </summary>
    public bool Contains(FNewReplicatedActorInfo ActorInfo)
    {
        foreach (var streamingList in StreamingLevelLists)
        {
            if (streamingList.StreamingLevelName == ActorInfo.StreamingLevelName)
            {
                return streamingList.ReplicationActorList.Contains(ActorInfo.Actor);
            }
        }
        return false;
    }

    public void DeepCopyFrom(FStreamingLevelActorListCollection source)
    {
        StreamingLevelLists.Clear(); // 相当于UE的Reset()
        foreach (var streamingLevel in source.StreamingLevelLists)
        {
            if (streamingLevel.ReplicationActorList.Num() > 0)
            {
                var newStreamingLevel = new FStreamingLevelActors(streamingLevel.StreamingLevelName);
                StreamingLevelLists.Add(newStreamingLevel);
                
                newStreamingLevel.ReplicationActorList.CopyContentsFrom(streamingLevel.ReplicationActorList);
                
                // 确保复制后列表大小一致
                ReplicationGraphDebugger.Ensure(newStreamingLevel.ReplicationActorList.Num() == streamingLevel.ReplicationActorList.Num());
            }
        }
    }

    public void GetAll_Debug(List<FActorRepListType> OutArray)
    {
        foreach (var streamingLevel in StreamingLevelLists)
        {
            streamingLevel.ReplicationActorList.AppendToTArray(OutArray);
        }
    }

    public void Log(FReplicationGraphDebugInfo DebugInfo)
    {
		foreach (var StreamingLevelList in StreamingLevelLists)
	    {
			ReplicationGraphDebugger.LogActorRepList(DebugInfo, StreamingLevelList.StreamingLevelName.ToString(), StreamingLevelList.ReplicationActorList);
		}
	}

	/// <summary>
	/// 获取Level数量
	/// </summary>
	public int NumLevels()
    {
        return StreamingLevelLists.Count;
    }
}