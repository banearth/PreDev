using UnityEngine;
using System.Collections.Generic;
using System;

public abstract class ReplicationGraph
{
    // 全局节点列表
    protected List<ReplicationGraphNode> GlobalNodes = new List<ReplicationGraphNode>();
    
    // 连接管理
    protected Dictionary<NetworkConnection, ConnectionData> ConnectionMap = new Dictionary<NetworkConnection, ConnectionData>();
    
    // 全局Actor信息映射
    protected Dictionary<ReplicatedActor, GlobalActorReplicationInfo> GlobalActorReplicationMap = new Dictionary<ReplicatedActor, GlobalActorReplicationInfo>();

    // 帧计数器 - 用于追踪更新
    protected int ReplicationFrameNum;
    
    // 是否已初始化
    protected bool bInitialized;

    // 连接数据类
    protected class ConnectionData
    {
        public List<ReplicationGraphNode> ConnectionNodes = new List<ReplicationGraphNode>();
        public GatheredReplicationLists ReplicationLists = new GatheredReplicationLists();
        public float LastGatherTime;
        public NetViewerArray ViewerInfos = new NetViewerArray();
    }

    // 全局Actor类信息映射
    protected Dictionary<Type, ClassReplicationInfo> GlobalClassInfoMap = new Dictionary<Type, ClassReplicationInfo>();

    protected NetworkDriver _driver;

    // 初始化
    public virtual void InitForNetDriver(NetworkDriver driver)
    {
        _driver = driver;
        if (bInitialized)
            return;

        InitGlobalActorClassSettings();
        InitGlobalGraphNodes();

        foreach (var connection in driver.ClientConnections)
        {
            AddClientConnection(connection);
        }

        bInitialized = true;
    }

    // 核心虚方法，参考 ReplicationGraph.h
    protected virtual void InitGlobalActorClassSettings()
    {
        // 注册需要复制的Actor类型
        RegisterActorClass<TestActor>(new ClassReplicationInfo 
        {
            ReplicationPeriodFrame = GetReplicationPeriodFrameForFrequency(30), // 30Hz更新
            CullDistanceSquared = 100 * 100,  // 100米裁剪距离
            AlwaysRelevant = false,
            OnlyRelevantToOwner = false
        });
        
        // 可以继续注册其他Actor类型...
    }

    protected void RegisterActorClass<T>(ClassReplicationInfo classInfo) where T : ReplicatedActor
    {
        GlobalClassInfoMap[typeof(T)] = classInfo;
    }

    protected int GetReplicationPeriodFrameForFrequency(float frequency)
    {
        // 假设服务器60帧
        const float ServerMaxTickRate = 60.0f;
        return Mathf.Max(1, Mathf.RoundToInt(ServerMaxTickRate / frequency));
    }

    protected virtual void InitGlobalGraphNodes() { }
    protected virtual void InitConnectionGraphNodes(NetworkConnection connection, ConnectionData connectionData) { }

    // 路由方法，参考 BasicReplicationGraph.h
    public virtual void RouteAddNetworkActorToNodes(ReplicatedActorInfo actorInfo, GlobalActorReplicationInfo globalInfo) { }
    public virtual void RouteRemoveNetworkActorToNodes(ReplicatedActorInfo actorInfo) { }

    // 节点管理
    protected void AddGlobalGraphNode(ReplicationGraphNode GraphNode)
    {
        // 简化版本，只需要添加到GlobalNodes列表
        GlobalNodes.Add(GraphNode);
        GraphNode.Initialize();  // 调用节点的初始化
    }

    // 连接管理
    public virtual void AddClientConnection(NetworkConnection connection)
    {
        var connectionData = new ConnectionData();
        ConnectionMap[connection] = connectionData;
        InitConnectionGraphNodes(connection, connectionData);
    }

    public virtual void RemoveClientConnection(NetworkConnection connection)
    {
        ConnectionMap.Remove(connection);
    }

    // Actor管理
    public virtual void AddNetworkActor(ReplicatedActor actor)
    {
        var actorInfo = CreateReplicatedActorInfo(actor);
        
        if (!GlobalActorReplicationMap.TryGetValue(actor, out var globalInfo))
        {
            globalInfo = new GlobalActorReplicationInfo();
            GlobalActorReplicationMap[actor] = globalInfo;
        }
        
        RouteAddNetworkActorToNodes(actorInfo, globalInfo);
    }

    public virtual void RemoveNetworkActor(ReplicatedActor actor)
    {
        var actorInfo = CreateReplicatedActorInfo(actor);
        RouteRemoveNetworkActorToNodes(actorInfo);
        GlobalActorReplicationMap.Remove(actor);
    }

    // 复制更新
    public virtual void ServerReplicateActors(float deltaSeconds)
    {
        if (!bInitialized)
            return;

        foreach (var kvp in ConnectionMap)
        {
            var connectionManager = kvp.Key;
            var connectionData = kvp.Value;

            var parameters = new ConnectionGatherActorListParameters
            {
                ConnectionManager = connectionManager,
                Viewers = connectionData.ViewerInfos,
                OutGatheredReplicationLists = connectionData.ReplicationLists,
                CurrentTimeSeconds = Time.time
            };

            // 从所有节点收集Actor
            foreach (var node in GlobalNodes)
            {
                node.GatherActorListsForConnection(parameters);
            }
        }
    }

    protected virtual ReplicatedActorInfo CreateReplicatedActorInfo(ReplicatedActor actor)
    {
        return new ReplicatedActorInfo
        {
            Actor = actor,
            Location = actor.Position,
            CullDistance = actor.NetCullDistanceSquared
        };
    }

    protected bool IsActorRelevantForConnection(ReplicatedActor actor, NetworkConnection connection)
    {
        if (actor == null || connection == null)
            return false;

        // 1. 检查是否总是相关
        if (actor.bAlwaysRelevant)
            return true;

        // 2. 检查是否只对所有者相关
        if (actor.bOnlyRelevantToOwner)
            return actor.Owner == connection;

        // 3. 检查距离 - 使用NetCullDistanceSquared而不是GetCullDistance
        var viewer = _driver.GetViewer(connection);
        if (viewer != null)
        {
            float distanceSquared = (actor.Position - viewer.ViewLocation).sqrMagnitude;
            return distanceSquared <= actor.NetCullDistanceSquared;
        }

        return false;
    }
}

// 全局Actor复制信息
public class GlobalActorReplicationInfo
{
    public Vector3 WorldLocation { get; set; }
    public float LastUpdateTime { get; set; }
    public HashSet<NetworkConnection> RelevantConnections = new HashSet<NetworkConnection>();
}