using System;
using System.Collections.Generic;
using UnityEngine;

public class UReplicationGraphNode_ActorListFrequencyBuckets : UReplicationGraphNode
{
    // 节点设置
    public class FSettings
    {
        public int NumBuckets = 3;
        public int ListSize = 12;
        public bool EnableFastPath = false; // 是否在"非活动"帧中返回FastPath列表
        public int FastPathFrameModulo = 1; // 只在帧号%此值=0时执行fast path

        // 基于节点中Actor数量动态平衡桶的阈值
        public class FBucketThresholds
        {
            public int MaxActors;  // 当Actor数量 <= MaxActors时
            public int NumBuckets; // 使用这个桶数量

            public FBucketThresholds(int maxActors, int numBuckets)
            {
                MaxActors = maxActors;
                NumBuckets = numBuckets;
            }
        }

        public List<FBucketThresholds> BucketThresholds = new List<FBucketThresholds>();
    }

    // 所有节点的默认设置
    public static FSettings DefaultSettings = new FSettings();
    
    // 此节点的特定设置
    private FSettings settings;
    
    // 非流关卡Actor的总数
    protected int TotalNumNonStreamingActors = 0;

    // 非流关卡Actor列表集合
    protected List<FActorRepListRefView> NonStreamingCollection = new List<FActorRepListRefView>();

    // 流关卡Actor列表集合
    protected FStreamingLevelActorListCollection StreamingLevelCollection = new FStreamingLevelActorListCollection();

    public UReplicationGraphNode_ActorListFrequencyBuckets()
    {
        settings = DefaultSettings;
        SetNonStreamingCollectionSize(settings.NumBuckets);
    }

    public override void NotifyAddNetworkActor(FNewReplicatedActorInfo actorInfo)
    {
        if (actorInfo.StreamingLevelName == "None")
        {
            // 添加到最小的桶中
            FActorRepListRefView bestList = null;
            int leastNum = int.MaxValue;
            
            foreach (var list in NonStreamingCollection)
            {
                if (list.Num() < leastNum)
                {
                    bestList = list;
                    leastNum = list.Num();
                }
            }

            bestList?.Add(actorInfo.Actor);
            TotalNumNonStreamingActors++;
            CheckRebalance();
        }
        else
        {
            StreamingLevelCollection.AddActor(actorInfo);
        }
    }

    public override void GatherActorListsForConnection(FConnectionGatherActorListParameters parameters)
    {
        if (settings.EnableFastPath)
        {
            // 一个列表走默认路径，其他走FastShared
            int defaultReplicationIdx = (int)(parameters.ReplicationFrameNum % NonStreamingCollection.Count);
            
            for (int idx = 0; idx < NonStreamingCollection.Count; idx++)
            {
                if (defaultReplicationIdx == idx)
                {
                    // 默认复制路径
                    parameters.OutGatheredReplicationLists.AddReplicationActorList(
                        NonStreamingCollection[idx], 
                        EActorRepListTypeFlags.Default);
                }
                else
                {
                    // 只在特定帧执行FastShared
                    if (parameters.ReplicationFrameNum % settings.FastPathFrameModulo == 0)
                    {
                        parameters.OutGatheredReplicationLists.AddReplicationActorList(
                            NonStreamingCollection[idx],
                            EActorRepListTypeFlags.FastShared);
                    }
                }
            }
        }
        else
        {
            // 只走默认路径
            int idx = (int)(parameters.ReplicationFrameNum % NonStreamingCollection.Count);
            parameters.OutGatheredReplicationLists.AddReplicationActorList(
                NonStreamingCollection[idx]);
        }

        StreamingLevelCollection.Gather(parameters);
    }

    protected void CheckRebalance()
    {
        int currentNumBuckets = NonStreamingCollection.Count;
        int desiredNumBuckets = currentNumBuckets;

        foreach (var threshold in settings.BucketThresholds)
        {
            if (TotalNumNonStreamingActors <= threshold.MaxActors)
            {
                desiredNumBuckets = threshold.NumBuckets;
                break;
            }
        }

        if (desiredNumBuckets != currentNumBuckets)
        {
            SetNonStreamingCollectionSize(desiredNumBuckets);
        }
    }

    protected void SetNonStreamingCollectionSize(int newSize)
    {
        // 保存所有Actor
        var fullList = new List<FActorRepListType>();
        foreach (var list in NonStreamingCollection)
        {
			list.AppendToTArray(fullList);
        }
        // 重置集合
        NonStreamingCollection.Clear();
		for (int i = 0; i < newSize; i++)
        {
            var newList = new FActorRepListRefView();
			newList.Reset(settings.ListSize);
			NonStreamingCollection.Add(newList);
        }
        // 重新分配Actor
        for (int idx = 0; idx < fullList.Count; idx++)
        {
            NonStreamingCollection[idx % newSize].Add(fullList[idx]);
        }
    }
}