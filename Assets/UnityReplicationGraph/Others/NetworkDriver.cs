using System.Collections.Generic;
using UnityEngine;

public class NetworkDriver
{
    private ReplicationDriver _replicationDriver;
    private uint _nextConnectionId = 1;
    public List<NetworkConnection> ClientConnections { get; private set; }
    private Dictionary<NetworkConnection, NetViewer> _connectionViewers;

    public NetworkDriver()
    {
        ClientConnections = new List<NetworkConnection>();
        _connectionViewers = new Dictionary<NetworkConnection, NetViewer>();
    }

    public void InitForNetManager(NetworkManager manager)
    {
        // 初始化网络管理器相关的设置
    }

    public void AddClientConnection(NetworkConnection connection)
    {
        if (_replicationDriver is BasicReplicationGraph repGraph)
        {
            repGraph.AddClientConnection(connection);
        }
    }

    public void InitReplicationDriver(ReplicationDriver driver)
    {
        _replicationDriver = driver;
        _replicationDriver.InitForNetDriver(this);
    }

    public void TickFlush(float deltaTime)
    {
        if (_replicationDriver != null)
        {
            _replicationDriver.ServerReplicateActors(deltaTime);
        }
    }

    public NetworkConnection CreateClientConnection()
    {
        var connection = new NetworkConnection(_nextConnectionId++);
        ClientConnections.Add(connection);
        
        var viewer = new NetViewer(connection);
        viewer.ViewLocation = new Vector3(UnityEngine.Random.Range(-100, 100), 0, UnityEngine.Random.Range(-100, 100));
        _connectionViewers[connection] = viewer;

        if (_replicationDriver is BasicReplicationGraph repGraph)
        {
            repGraph.AddClientConnection(connection);
        }
        
        return connection;
    }
} 