using System.Collections.Generic;
using UnityEngine;

public class UNetworkDriver
{
    private UReplicationDriver _replicationDriver;
    private uint _nextConnectionId = 1;
    public List<UNetConnection> ClientConnections { get; private set; }
    private Dictionary<UNetConnection, FNetViewer> _connectionViewers;
    public uint ReplicationFrame;

    public UNetworkDriver()
    {
        ClientConnections = new List<UNetConnection>();
        _connectionViewers = new Dictionary<UNetConnection, FNetViewer>();
    }

    public void InitReplicationDriver(UReplicationDriver driver)
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

    public UNetConnection CreateClientConnection()
    {
        var connection = new UNetConnection(_nextConnectionId++);
        ClientConnections.Add(connection);
        
        var viewer = new FNetViewer(connection);
        viewer.ViewLocation = new Vector3(Random.Range(-100, 100), 0, Random.Range(-100, 100));
        _connectionViewers[connection] = viewer;

        if (_replicationDriver != null)
        {
            _replicationDriver.AddClientConnection(connection);
        }
        
        return connection;
    }
} 