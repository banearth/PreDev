using System.Collections.Generic;

/// <summary>
/// 为连接添加始终相关的Actor。此引擎版本仅添加PlayerController和ViewTarget(通常是pawn)
/// </summary>
public class UReplicationGraphNode_AlwaysRelevant_ForConnection : UReplicationGraphNode_ActorList
{

    /// <summary>
    /// 每个连接之前(或当前如果上一tick没有改变)的焦点Actor数据映射
    /// </summary>
    protected Dictionary<UNetConnection, FCachedAlwaysRelevantActorInfo> PastRelevantActorMap = new();

    public override void GatherActorListsForConnection(FConnectionGatherActorListParameters Params)
    {
        base.GatherActorListsForConnection(Params);
        ReplicationActorList.Reset();
        foreach (var curViewer in Params.Viewers)
        {
            if (curViewer.Connection == null)
            {
                continue;
            }
			var lastData = PastRelevantActorMap.GetOrAdd(curViewer.Connection, () => new FCachedAlwaysRelevantActorInfo());
			AddCachedRelevantActor(Params, curViewer.InViewer, ref lastData.LastViewer);
            AddCachedRelevantActor(Params, curViewer.ViewTarget, ref lastData.LastViewTarget);
        }

        CleanupCachedRelevantActors(PastRelevantActorMap);

        if (ReplicationActorList.Num() > 0)
        {
            Params.OutGatheredReplicationLists.AddReplicationActorList(ReplicationActorList);
        }
    }

    public override void TearDown()
    {
        base.TearDown();
        ReplicationActorList.TearDown();
    }

    protected override void OnCollectActorRepListStats(FActorRepListStatCollector statsCollector)
    {
        statsCollector.VisitRepList(this, ReplicationActorList);
        base.OnCollectActorRepListStats(statsCollector);
    }

    protected void AddCachedRelevantActor(FConnectionGatherActorListParameters parameters,
		FActorRepListType newActor, ref FActorRepListType lastActor)
    {
        UpdateCachedRelevantActor(parameters, newActor, ref lastActor);
        if (newActor != null && !ReplicationActorList.Contains(newActor))
        {
            ReplicationActorList.Add(newActor);
        }
    }

    protected void UpdateCachedRelevantActor(FConnectionGatherActorListParameters parameters,
		FActorRepListType newActor, ref FActorRepListType lastActor)
    {
		FActorRepListType prevActor = lastActor;
        if (newActor != prevActor)
        {
            if (newActor != null)
            {
                // 将新Actor的剔除距离设为0
                parameters.ConnectionManager.ActorInfoMap
                    .FindOrAdd(newActor)
                    .SetCullDistanceSquared(0f);
            }

            if (prevActor != null)
            {
                // 重置前一个Actor的剔除距离
                var actorInfo = parameters.ConnectionManager.ActorInfoMap.FindOrAdd(prevActor);
                actorInfo.SetCullDistanceSquared(
                    GraphGlobals.GlobalActorReplicationInfoMap.Get(prevActor).Settings.GetCullDistanceSquared());
            }

            if (newActor != null)
            {
                lastActor = newActor;
            }
            else
            {
                lastActor = null;
            }
        }
    }

    protected static void CleanupCachedRelevantActors<TKey, TValue>(
        Dictionary<TKey, TValue> actorMap) where TKey : class
    {
        var keysToRemove = new List<TKey>();
        foreach (var kvp in actorMap)
        {
            if (kvp.Key == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            actorMap.Remove(key);
        }
    }
}

public class FCachedAlwaysRelevantActorInfo
{
    public FActorRepListType LastViewer;
    public FActorRepListType LastViewTarget;
}