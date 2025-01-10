using UnityEngine;
using System.Collections.Generic;

// 简化的网络驱动
public class NetworkDriver
{
	public List<NetworkConnection> ClientConnections { get; private set; } = new List<NetworkConnection>();
	private int _nextConnectionId = 1;

	public NetworkConnection CreateClientConnection()
	{
		var connection = new NetworkConnection(_nextConnectionId++);
			ClientConnections.Add(connection);
			return connection;
	}

	public void RemoveConnection(NetworkConnection connection)
	{
		ClientConnections.Remove(connection);
	}
}

// 网络连接
public class NetworkConnection
{
	public int ConnectionId { get; private set; }
	public float ReplicationInterval { get; set; } = 0.1f;
	public Dictionary<int, IReplicatedObject> ReplicatedActors { get; private set; } = new Dictionary<int, IReplicatedObject>();

	public NetworkConnection(int id)
	{
		ConnectionId = id;
	}

	public void ReplicateActor(ReplicatedActorInfo actorInfo)
	{
		if (!ReplicatedActors.ContainsKey(actorInfo.NetId))
		{
			ReplicatedActors.Add(actorInfo.NetId, actorInfo.Actor);
			Debug.Log($"[Connection {ConnectionId}] Replicated actor {actorInfo.NetId}");
		}
	}
}

// 网络管理器
public class NetworkManager : MonoBehaviour
{
	private NetworkDriver _driver;
	private BasicReplicationGraph _repGraph;
	private Dictionary<NetworkConnection, NetViewer> _connectionViewers = new Dictionary<NetworkConnection, NetViewer>();

	private void Start()
	{
		_driver = new NetworkDriver();
		_repGraph = new BasicReplicationGraph();
		_repGraph.InitForNetDriver(_driver);
		
		CreateTestClients();
	}

	private void CreateTestClients()
	{
		for (int i = 0; i < 3; i++)
		{
			var connection = _driver.CreateClientConnection();
			
			// 创建并设置Viewer
			var viewer = new NetViewer(connection);
			viewer.ViewLocation = new Vector3(Random.Range(-100, 100), 0, Random.Range(-100, 100));
			_connectionViewers[connection] = viewer;

			_repGraph.AddClientConnection(connection);
		}
	}

	private void Update()
	{
		_repGraph.ServerReplicateActors(Time.deltaTime);
	}

	public void AddTestActor(Vector3 position, float cullDistance)
	{
		var actor = new TestReplicatedObject
		{
			Position = position,
			CullDistance = cullDistance
		};

		_repGraph.AddNetworkActor(actor);
	}
}

// 测试用的复制对象
public class TestReplicatedObject : IReplicatedObject
{
	private static int NextId = 1;
	
	public int NetId { get; set; } = NextId++;
	public Vector3 Position { get; set; }
	public float CullDistance { get; set; }

	public Vector3 GetLocation() => Position;
	public float GetCullDistance() => CullDistance;
}