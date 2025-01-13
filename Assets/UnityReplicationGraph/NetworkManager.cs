using UnityEngine;
using System.Collections.Generic;

// 网络连接
public class NetworkConnection
{
	public int ConnectionId { get; private set; }
	public float ReplicationInterval { get; set; } = 0.1f;
	public Dictionary<int, ReplicatedActor> ReplicatedActors { get; private set; } = new Dictionary<int, ReplicatedActor>();

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
	
	private void Start()
	{
		_driver = new NetworkDriver();
		_driver.InitForNetManager(this);
	}

	// 游戏框架层面的网络功能
	public void StartServer() { }
	public void StopServer() { }
	public void CreateTestClients() { }
}

// 测试用的复制对象
public class TestReplicatedObject : ReplicatedActor
{
	private static int NextId = 1;
	
	public int NetId { get; set; } = NextId++;
	public float CullDistance { get; set; }

	public Vector3 GetLocation() => Position;
	public float GetCullDistance() => CullDistance;
}