using System.Collections.Generic;
using UnityEngine;

public class NetworkDriver
{
    private BasicReplicationGraph _repGraph;
    private int _nextConnectionId = 1;
    private Dictionary<NetworkConnection, NetViewer> _connectionViewers;
    public List<NetworkConnection> ClientConnections { get; private set; }

    public NetworkDriver()
    {
        // 在构造函数中初始化所有字段
        ClientConnections = new List<NetworkConnection>();
        _connectionViewers = new Dictionary<NetworkConnection, NetViewer>();
        _repGraph = new BasicReplicationGraph();
    }

    public void InitForNetManager(NetworkManager manager)
    {
        _repGraph.InitForNetDriver(this);
    }

    public NetworkConnection CreateClientConnection()
    {
        var connection = new NetworkConnection(_nextConnectionId++);
        ClientConnections.Add(connection);
        
        // 创建并设置Viewer
        var viewer = new NetViewer(connection);
        viewer.ViewLocation = new Vector3(UnityEngine.Random.Range(-100, 100), 0, UnityEngine.Random.Range(-100, 100));
        _connectionViewers[connection] = viewer;

        _repGraph.AddClientConnection(connection);
        
        return connection;
    }

    public void RemoveConnection(NetworkConnection connection)
    {
        ClientConnections.Remove(connection);
        _connectionViewers.Remove(connection);
    }

    public NetViewer GetViewer(NetworkConnection connection)
    {
        return _connectionViewers.TryGetValue(connection, out var viewer) ? viewer : null;
    }

    public BasicReplicationGraph GetReplicationGraph() => _repGraph;
} 