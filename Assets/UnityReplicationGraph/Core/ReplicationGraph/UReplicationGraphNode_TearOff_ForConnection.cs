using System.Collections.Generic;

/// <summary>
/// 管理需要Tear Off的Actor。我们会尝试为每个连接最后一次复制这些Actor。
/// </summary>
public class UReplicationGraphNode_TearOff_ForConnection : UReplicationGraphNode
{
    /// <summary>
    /// Tear Off的Actor信息列表
    /// </summary>
    public List<FTearOffActorInfo> TearOffActors = new List<FTearOffActorInfo>();

    /// <summary>
    /// 复制Actor列表视图
    /// </summary>
    private FActorRepListRefView ReplicationActorList = new FActorRepListRefView();

    public override void NotifyAddNetworkActor(FNewReplicatedActorInfo actorInfo) { }

    public override bool NotifyRemoveNetworkActor(FNewReplicatedActorInfo actorInfo, bool bWarnIfNotFound = true) 
    { 
        return false; 
    }

    public override bool NotifyActorRenamed(FRenamedReplicatedActorInfo actor, bool bWarnIfNotFound = true) 
    { 
        return false; 
    }

    public override void NotifyResetAllNetworkActors()
    {
        TearOffActors.Clear();
    }

    public override void GatherActorListsForConnection(FConnectionGatherActorListParameters parameters)
    {
        if (TearOffActors.Count > 0)
        {
            ReplicationActorList.Reset();
            var actorInfoMap = parameters.ConnectionManager.ActorInfoMap;

            for (int idx = TearOffActors.Count - 1; idx >= 0; --idx)
            {
                var tearOffInfo = TearOffActors[idx];
                var actor = tearOffInfo.Actor;
                uint tearOffFrameNum = tearOffInfo.TearOffFrameNum;

				// 检查Actor是否仍然有效
				if (actor != null && ReplicationGraphUtils.IsActorValidForReplication(actor))
                {
                    // 检查Actor是否在变为tear off后复制过
                    var actorInfo = actorInfoMap.Find(actor);
                    if (actorInfo != null)
                    {
                        // 继续添加到输出列表，直到至少复制一次
                        // 由于饱和可能会阻止它在任何给定帧上发生
                        if (actorInfo.LastRepFrameNum <= tearOffFrameNum && 
                            !(actorInfo.LastRepFrameNum <= 0 && tearOffInfo.HasReppedOnce))
                        {
                            ReplicationActorList.Add(actor);
                            tearOffInfo.HasReppedOnce = true;
                            continue;
                        }
                    }
                }

                // 如果没有添加到列表中，移除这个Actor
                TearOffActors.RemoveAtSwap(idx);
            }

            if (ReplicationActorList.Num() > 0)
            {
                parameters.OutGatheredReplicationLists.AddReplicationActorList(ReplicationActorList);
            }
        }
    }

    public override void LogNode(FReplicationGraphDebugInfo debugInfo, string nodeName)
    {
		ReplicationGraphDebugger.LogInfo(nodeName);
		ReplicationGraphDebugger.LogActorRepList(debugInfo, "TearOff", ReplicationActorList);
	}

    protected override void VerifyActorReferencesInternal()
    {
        base.VerifyActorReferencesInternal();
		foreach (var actor in ReplicationActorList)
		{
			VerifyActorReference(actor);
		}
	}

    /// <summary>
    /// 通知Actor需要Tear Off
    /// </summary>
    public void NotifyTearOffActor(FActorRepListType actor, uint frameNum)
    {
        TearOffActors.Add(new FTearOffActorInfo(actor, frameNum));
    }

    protected override void OnCollectActorRepListStats(FActorRepListStatCollector statsCollector)
    {
		// 实现收集Actor复制统计信息的逻辑
		statsCollector.VisitRepList(this, ReplicationActorList);
		base.OnCollectActorRepListStats(statsCollector);
	}
}

/// <summary>
/// Tear Off Actor的信息
/// </summary>
public class FTearOffActorInfo
{
    /// <summary>
    /// Actor变为Tear Off时的帧号
    /// </summary>
    public uint TearOffFrameNum { get; private set; }

    /// <summary>
    /// Actor引用
    /// </summary>
    public FActorRepListType Actor { get; private set; }

    /// <summary>
    /// 是否已经至少复制过一次
    /// </summary>
    public bool HasReppedOnce { get; set; }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public FTearOffActorInfo()
    {
        TearOffFrameNum = 0;
        Actor = null;
        HasReppedOnce = false;
    }

    /// <summary>
    /// 带参数的构造函数
    /// </summary>
    public FTearOffActorInfo(FActorRepListType actor, uint tearOffFrameNum)
    {
        Actor = actor;
        TearOffFrameNum = tearOffFrameNum;
        HasReppedOnce = false;
    }
}