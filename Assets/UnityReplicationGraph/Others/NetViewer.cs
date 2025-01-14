using UnityEngine;
using System.Collections.Generic;

public class NetViewer
{
    public NetworkConnection Connection { get; private set; }
    public Vector3 ViewLocation { get; set; }
    
    // 简化版本的构造函数
    public NetViewer(NetworkConnection inConnection)
    {
        Connection = inConnection;
        ViewLocation = Vector3.zero;  // 初始化位置
    }
}

// 用于存储viewer数组
public class NetViewerArray : List<NetViewer>
{
}

// 修改连接参数结构，使其与UE一致
public class ConnectionGatherActorListParameters
{
    // 参考 FConnectionGatherActorListParameters
    public NetworkConnection ConnectionManager;  // 改为 ConnectionManager 而不是 Connection
    public NetViewerArray Viewers;
    public GatheredReplicationLists OutGatheredReplicationLists;
    public float CurrentTimeSeconds;
}

public class GatheredReplicationLists
{
    private List<ReplicatedActorInfo> ActorLists = new List<ReplicatedActorInfo>();

    public void Add(ReplicatedActorInfo actorInfo)
    {
        ActorLists.Add(actorInfo);
    }

    public void Clear()
    {
        ActorLists.Clear();
    }
} 