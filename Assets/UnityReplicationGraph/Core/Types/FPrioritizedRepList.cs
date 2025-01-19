using System;
using System.Collections.Generic;

public static class PrioritizationConstants
{
	// 优先级距离缩放最大值，超过此距离的优先级值为相同或“封顶”
	public static float MaxDistanceScaling = 60000f * 60000f;
	// 自上次同步以来的时间，优先级缩放最大值
	public static uint MaxFramesSinceLastRep = 20;
}

/// <summary>
/// 用于复制的优先级Actor列表。这是我们实际用于复制Actor的数据结构。
/// </summary>
public class FPrioritizedRepList
{
    public FPrioritizedRepList() { }

    /// <summary>
    /// 优先级列表中的单个条目
    /// </summary>
    public class FItem
    {
        public FItem(float priority, FActorRepListType actor, FGlobalActorReplicationInfo globalData, FConnectionReplicationActorInfo connectionData)
        {
            Priority = priority;
            Actor = actor;
            GlobalData = globalData;
            ConnectionData = connectionData;
        }

        // 优先级值
        public float Priority;
        // Actor引用
        public FActorRepListType Actor;
        // 全局复制数据
        public FGlobalActorReplicationInfo GlobalData;
        // 连接特定的复制数据
        public FConnectionReplicationActorInfo ConnectionData;

        public static bool operator <(FItem a, FItem b) => a.Priority < b.Priority;
        public static bool operator >(FItem a, FItem b) => a.Priority > b.Priority;
    }

    // 优先级排序的Actor列表
    public List<FItem> Items = new();

    // 重置列表
    public void Reset()
    {
        Items.Clear();
    }
}