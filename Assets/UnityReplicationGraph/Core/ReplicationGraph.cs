using UnityEngine;
using System.Collections.Generic;
using System;

public abstract class ReplicationGraph : ReplicationDriver
{
    protected List<ReplicationGraphNode> GlobalGraphNodes = new List<ReplicationGraphNode>();
    protected Dictionary<Type, ClassReplicationInfo> GlobalClassInfoMap = new Dictionary<Type, ClassReplicationInfo>();

    public override void InitForNetDriver(NetworkDriver driver)
    {
        base.InitForNetDriver(driver);
        InitGlobalActorClassSettings();
        InitGlobalGraphNodes();
    }

    public ReplicatedActorInfo CreateReplicatedActorInfo(ReplicatedActor actor)
    {
        return new ReplicatedActorInfo(actor);
    }

    protected void AddGlobalGraphNode(ReplicationGraphNode node)
    {
        GlobalGraphNodes.Add(node);
    }

    protected void RegisterActorClass<T>(ClassReplicationInfo classInfo) where T : ReplicatedActor
    {
        GlobalClassInfoMap[typeof(T)] = classInfo;
    }

    public override void ServerReplicateActors(float deltaTime)
    {
        // 实现具体的复制逻辑
    }

    public abstract void RouteAddNetworkActorToNodes(ReplicatedActorInfo actorInfo);

    protected abstract void InitGlobalActorClassSettings();
    protected abstract void InitGlobalGraphNodes();
    public abstract void RouteRemoveNetworkActorToNodes(ReplicatedActorInfo actorInfo);

    protected int GetReplicationPeriodFrameForFrequency(float frequency)
    {
        const float ServerMaxTickRate = 60.0f;
        return Mathf.Max(1, Mathf.RoundToInt(ServerMaxTickRate / frequency));
    }

    // ... 其他ReplicationGraph特有的功能 ...
}