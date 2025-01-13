using UnityEngine;
using System.Collections.Generic;

public class NetworkManager : MonoBehaviour
{
	private static NetworkManager _instance;
	public static NetworkManager Instance => _instance;

	private NetworkDriver _driver;
	public NetworkDriver Driver => _driver;

	private Dictionary<uint, ReplicatedActorInfo> _actorInfoMap = new Dictionary<uint, ReplicatedActorInfo>();
	private uint _nextNetId = 1;

	private void Awake()
	{
		if (_instance == null)
		{
			_instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else
		{
			Destroy(gameObject);
		}
	}

	private void Start()
	{
		InitializeNetwork();
	}

	private void InitializeNetwork()
	{
		_driver = new NetworkDriver();
		_driver.InitForNetManager(this);

		var replicationGraph = new BasicReplicationGraph();
		_driver.InitReplicationDriver(replicationGraph);
	}

	public void SpawnNetworkActor(ReplicatedActor actor)
	{
		actor.NetId = _nextNetId++;
		var actorInfo = new ReplicatedActorInfo(actor);
		_actorInfoMap.Add(actor.NetId, actorInfo);
	}

	public void DespawnNetworkActor(uint netId)
	{
		if (_actorInfoMap.TryGetValue(netId, out var actorInfo))
		{
			_actorInfoMap.Remove(netId);
		}
	}

	public ReplicatedActorInfo GetActorInfo(uint netId)
	{
		_actorInfoMap.TryGetValue(netId, out var actorInfo);
		return actorInfo;
	}

	// 游戏框架层面的网络功能
	public void StartServer() { }
	public void StopServer() { }
	public void CreateTestClients() { }
}
