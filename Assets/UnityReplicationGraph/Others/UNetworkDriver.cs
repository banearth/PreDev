using System.Collections.Generic;
using UnityEngine;

public class UNetworkDriver
{
    private UReplicationDriver _replicationDriver;
    private uint _nextConnectionId = 1;
    public List<UNetConnection> ClientConnections { get; private set; }
    public uint ReplicationFrame;

    public UNetworkDriver()
    {
        ClientConnections = new List<UNetConnection>();
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
        if (_replicationDriver != null)
        {
            _replicationDriver.AddClientConnection(connection);
        }
        return connection;
    }
} 