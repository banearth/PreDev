using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UReplicationGraphNode_DynamicSpatialFrequency : UReplicationGraphNode_ActorList
{
    // 空间区域设置
    [System.Serializable]
    public struct FSpatializationZone
    {
        public float MinDotProduct;      // 必须有大于等于此值的点积才能在此区域
        public float MinDistPct;         // 最小距离(占连接剔除距离的百分比)
        public float MaxDistPct;         // 最大距离(占连接剔除距离的百分比)
        public uint MinRepPeriod;        // 最小复制周期
        public uint MaxRepPeriod;        // 最大复制周期
        public uint FastPath_MinRepPeriod; // 快速路径最小复制周期
        public uint FastPath_MaxRepPeriod; // 快速路径最大复制周期

        public FSpatializationZone(float inMinDotProduct, float inMinDistPct, float inMaxDistPct, 
            uint inMinRepPeriod, uint inMaxRepPeriod, uint inMinRepPeriodFast, uint inMaxRepPeriodFast)
        {
            MinDotProduct = inMinDotProduct;
            MinDistPct = inMinDistPct;
            MaxDistPct = inMaxDistPct;
            MinRepPeriod = inMinRepPeriod;
            MaxRepPeriod = inMaxRepPeriod;
            FastPath_MinRepPeriod = inMinRepPeriodFast;
            FastPath_MaxRepPeriod = inMaxRepPeriodFast;
        }

        // 基于目标帧率初始化
        public FSpatializationZone(float inMinDotProduct, float inMinDistPct, float inMaxDistPct,
            float inMinRepHz, float inMaxRepHz, float inMinRepHzFast, float inMaxRepHzFast, float tickRate)
        {
            MinDotProduct = inMinDotProduct;
            MinDistPct = inMinDistPct;
            MaxDistPct = inMaxDistPct;
            MinRepPeriod = HzToFrm(inMinRepHz, tickRate);
            MaxRepPeriod = HzToFrm(inMaxRepHz, tickRate);
            FastPath_MinRepPeriod = HzToFrm(inMinRepHzFast, tickRate);
            FastPath_MaxRepPeriod = HzToFrm(inMaxRepHzFast, tickRate);
        }

        private static uint HzToFrm(float hz, float targetFrameRate)
        {
            return hz > 0f ? (uint)Mathf.CeilToInt(targetFrameRate / hz) : 0;
        }
    }

    // 设置
    public class FSettings
    {
        public List<FSpatializationZone> ZoneSettings;
        public List<FSpatializationZone> ZoneSettings_NonFastShared; // 不支持FastShared复制的Actor的区域设置
        public long MaxBitsPerFrame;
        public int MaxNearestActors; // 只复制离连接最近的X个Actor。-1=无限制

        public FSettings(List<FSpatializationZone> zoneSettings, List<FSpatializationZone> nonFastSharedSettings, 
            long maxBitsPerFrame, int maxNearestActors = -1)
        {
            ZoneSettings = zoneSettings;
            ZoneSettings_NonFastShared = nonFastSharedSettings;
            MaxBitsPerFrame = maxBitsPerFrame;
            MaxNearestActors = maxNearestActors;
        }
    }

    // 默认设置
    public static FSettings DefaultSettings;
    private FSettings Settings;

    public FSettings GetSettings() => Settings ?? DefaultSettings;

    // 用于跟踪此节点的gather/prioritizing阶段的统计名称
    public string CSVStatName;

    // 排序的复制列表项
    protected struct FDynamicSpatialFrequency_SortedItem
    {
        public FActorRepListType Actor;
        public int FramesTillReplicate; // 注意:在处理FSettings::MaxNearestActors时也用作"Distance Sq"
        public bool EnableFastPath;
        public FGlobalActorReplicationInfo GlobalInfo;
        public FConnectionReplicationActorInfo ConnectionInfo;

        public FDynamicSpatialFrequency_SortedItem(FActorRepListType actor, int framesTillReplicate, 
            bool enableFastPath, FGlobalActorReplicationInfo globalInfo, FConnectionReplicationActorInfo connectionInfo)
        {
            Actor = actor;
            FramesTillReplicate = framesTillReplicate;
            EnableFastPath = enableFastPath;
            GlobalInfo = globalInfo;
            ConnectionInfo = connectionInfo;
        }
    }

    // 用于排序的复制列表。每帧为每个连接重置
    protected List<FDynamicSpatialFrequency_SortedItem> SortedReplicationList = new List<FDynamicSpatialFrequency_SortedItem>();

    // 用于自适应负载均衡的工作整数。不计算每帧复制的Actor
    protected int NumExpectedReplicationsThisFrame;
    protected int NumExpectedReplicationsNextFrame;

    public override void GatherActorListsForConnection(FConnectionGatherActorListParameters Params)
    {
        var repGraph = GraphGlobals.ReplicationGraph;
        var globalMap = GraphGlobals.GlobalActorReplicationInfoMap;
        var netConnection = Params.ConnectionManager.NetConnection;
        var connectionMap = Params.ConnectionManager.ActorInfoMap;
        var frameNum = Params.ReplicationFrameNum;
        var settings = GetSettings();
        
        // 重置状态
        SortedReplicationList.Clear();
        NumExpectedReplicationsThisFrame = 0;
        NumExpectedReplicationsNextFrame = 0;

        bool doFullGather = true;

        // 两阶段收集: 如果有最大Actor数量限制,先基于距离过滤
        if (settings.MaxNearestActors >= 0)
        {
			// 计算可能的总Actor数量
			int possibleNumActors = ReplicationActorList.Num();
			foreach (var streamingList in StreamingLevelCollection.StreamingLevelLists)
            {
                if (Params.CheckClientVisibilityForLevel(streamingList.StreamingLevelName))
                {
                    possibleNumActors += streamingList.ReplicationActorList.Num();
                }
            }

            // 如果超过限制,进行距离过滤
            if (possibleNumActors > settings.MaxNearestActors)
            {
                doFullGather = false;

                // 收集所有Actor的距离信息
                GatherActors_DistanceOnly(ReplicationActorList, globalMap, connectionMap, Params);

                // 处理流关卡Actor
                foreach (var streamingList in StreamingLevelCollection.StreamingLevelLists)
                {
                    if (Params.CheckClientVisibilityForLevel(streamingList.StreamingLevelName))
                    {
                        GatherActors_DistanceOnly(streamingList.ReplicationActorList, globalMap, connectionMap, Params);
                    }
                }

                // 排序并限制数量
                SortedReplicationList.Sort((a, b) => a.FramesTillReplicate.CompareTo(b.FramesTillReplicate));
                if (SortedReplicationList.Count > settings.MaxNearestActors)
                {
                    SortedReplicationList.RemoveRange(settings.MaxNearestActors, 
                        SortedReplicationList.Count - settings.MaxNearestActors);
                }

                // 对剩余Actor计算频率
                for (int idx = SortedReplicationList.Count - 1; idx >= 0; --idx)
                {
                    var item = SortedReplicationList[idx];
                    CalcFrequencyForActor(item.Actor, repGraph, Params.ConnectionManager,
                        item.GlobalInfo, connectionMap, settings, Params.Viewers, frameNum, idx);
                }

                SortedReplicationList.Sort((a, b) => a.FramesTillReplicate.CompareTo(b.FramesTillReplicate));
            }
        }

        // 单阶段收集: 直接计算频率
        if (doFullGather)
        {
            GatherActors(ReplicationActorList, globalMap, connectionMap, Params, netConnection);

            foreach (var streamingList in StreamingLevelCollection.StreamingLevelLists)
            {
                if (Params.CheckClientVisibilityForLevel(streamingList.StreamingLevelName))
                {
                    GatherActors(streamingList.ReplicationActorList, globalMap, connectionMap, Params, netConnection);
                }
            }

            SortedReplicationList.Sort((a, b) => a.FramesTillReplicate.CompareTo(b.FramesTillReplicate));
        }

        // 复制阶段
        long bitsWritten = 0;
        int opportunisticLoadBalanceQuota = (NumExpectedReplicationsThisFrame - NumExpectedReplicationsNextFrame) >> 1;

        foreach (var item in SortedReplicationList)
        {
            if (!ReplicationGraphUtils.IsActorValidForReplication(item.Actor))
                continue;

            var connectionInfo = item.ConnectionInfo;
            if (connectionInfo.bTearOff)
                continue;

            // 负载均衡
            if (opportunisticLoadBalanceQuota > 0 && item.FramesTillReplicate == 0 
                && !ReplicatesEveryFrame(connectionInfo, item.EnableFastPath))
            {
                opportunisticLoadBalanceQuota--;
                continue;
            }

            // 默认复制
            if (ReadyForNextReplication(connectionInfo, item.GlobalInfo, frameNum))
            {
                bitsWritten += repGraph.ReplicateSingleActor(item.Actor, connectionInfo, item.GlobalInfo, 
                    connectionMap, Params.ConnectionManager, frameNum);
                connectionInfo.FastPath_LastRepFrameNum = frameNum;
            }
            // FastPath复制
            else if (item.EnableFastPath && ReadyForNextReplication_FastPath(connectionInfo, item.GlobalInfo, frameNum))
            {
                int fastSharedBits = repGraph.ReplicateSingleActor_FastShared(item.Actor, connectionInfo, 
                    item.GlobalInfo, Params.ConnectionManager, frameNum);

				//netConnection.QueuedBits -= fastSharedBits;
                bitsWritten += fastSharedBits;
            }

            // 带宽限制
            //if (bitsWritten > settings.MaxBitsPerFrame)
            //{
                //Params.ConnectionManager.NotifyDSFNodeSaturated(this);
                //break;
            //}
        }
    }

    protected virtual void GatherActors(FActorRepListRefView RepList, FGlobalActorReplicationInfoMap GlobalMap,
        FPerConnectionActorInfoMap ConnectionMap, FConnectionGatherActorListParameters Params, UNetConnection NetConnection)
    {
        var settings = GetSettings();
        var frameNum = Params.ReplicationFrameNum;

        foreach (var actor in RepList)
        {
            // 跳过ViewTarget
            bool shouldSkip = false;
            foreach (var viewer in Params.Viewers)
            {
                if (actor == viewer.ViewTarget)
                {
                    shouldSkip = true;
                    break;
                }
            }
            if (shouldSkip) continue;

            var globalInfo = GlobalMap.Get(actor);
            CalcFrequencyForActor(actor, GraphGlobals.ReplicationGraph, Params.ConnectionManager,
                globalInfo, ConnectionMap, settings, Params.Viewers, frameNum, -1);
        }
    }

    protected void CalcFrequencyForActor(FActorRepListType Actor, UReplicationGraph RepGraph,
        UNetReplicationGraphConnection RepGraphConnection, FGlobalActorReplicationInfo GlobalInfo,
        FPerConnectionActorInfoMap ConnectionMap, FSettings MySettings, List<FNetViewer> Viewers,
        uint FrameNum, int ExistingItemIndex)
    {
        // 找到最近的观察者
        float shortestDistSq = float.MaxValue;
        FNetViewer? closestViewer = null;
        
        foreach (var viewer in Viewers)
        {
            float distSq = (GlobalInfo.WorldLocation - viewer.ViewLocation).sqrMagnitude;
            if (distSq < shortestDistSq)
            {
                shortestDistSq = distSq;
                closestViewer = viewer;
            }
        }

        if (closestViewer == null)
            return;

        // 计算朝向
        var dirToActor = (GlobalInfo.WorldLocation - closestViewer.Value.ViewLocation).normalized;
        float dotP = Vector3.Dot(dirToActor, closestViewer.Value.ViewDir);

        // 遍历区域设置
        foreach (var zone in MySettings.ZoneSettings)
        {
            if (dotP >= zone.MinDotProduct)
            {
                var connectionInfo = ConnectionMap.FindOrAdd(Actor);
                float cullDist = connectionInfo.GetCullDistance();
                float dist = Mathf.Sqrt(shortestDistSq);
                
                // 计算距离百分比
                float distPct = cullDist > 0 ? Mathf.Clamp01(dist / cullDist) : 1f;
                float finalPct = Mathf.Clamp01((distPct - zone.MinDistPct) / (zone.MaxDistPct - zone.MinDistPct));

                // 计算复制周期
                var repPeriod = (ushort)Mathf.Lerp(zone.MinRepPeriod, zone.MaxRepPeriod, finalPct);
                connectionInfo.ReplicationPeriodFrame = repPeriod;

                // 添加到排序列表
                bool enableFastPath = false; // 根据需要设置FastPath
                SortedReplicationList.Add(new FDynamicSpatialFrequency_SortedItem(
                    Actor, (int)(connectionInfo.NextReplicationFrameNum - FrameNum),
                    enableFastPath, GlobalInfo, connectionInfo));
                
                return;
            }
        }
    }

    private void GatherActors_DistanceOnly(FActorRepListRefView RepList, FGlobalActorReplicationInfoMap GlobalMap,
        FPerConnectionActorInfoMap ConnectionMap, FConnectionGatherActorListParameters Params)
    {
        foreach (var actor in RepList)
        {
            float smallestDistanceSq = float.MaxValue;
            foreach (var viewer in Params.Viewers)
            {
				float distSq = Vector3.SqrMagnitude(actor.Position - viewer.ViewLocation);
                smallestDistanceSq = Mathf.Min(distSq, smallestDistanceSq);
            }
            SortedReplicationList.Add(new FDynamicSpatialFrequency_SortedItem(actor, Mathf.FloorToInt(smallestDistanceSq), false, null, null));
        }
    }

    protected bool ReplicatesEveryFrame(FConnectionReplicationActorInfo ConnectionInfo, bool CheckFastPath)
    {
        return !(ConnectionInfo.ReplicationPeriodFrame > 1 && 
            (!CheckFastPath || ConnectionInfo.FastPath_ReplicationPeriodFrame > 1));
    }

    protected bool ReadyForNextReplication(FConnectionReplicationActorInfo ConnectionData, 
        FGlobalActorReplicationInfo GlobalData, uint FrameNum)
    {
        return (ConnectionData.NextReplicationFrameNum <= FrameNum || 
            GlobalData.ForceNetUpdateFrame > ConnectionData.LastRepFrameNum);
    }

    protected bool ReadyForNextReplication_FastPath(FConnectionReplicationActorInfo ConnectionData, 
        FGlobalActorReplicationInfo GlobalData, uint FrameNum)
    {
        return (ConnectionData.FastPath_NextReplicationFrameNum <= FrameNum || 
            GlobalData.ForceNetUpdateFrame > ConnectionData.FastPath_LastRepFrameNum);
    }
}