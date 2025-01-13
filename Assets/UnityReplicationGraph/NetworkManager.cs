using UnityEngine;
using System.Collections.Generic;

// 网络管理器
public class NetworkManager : MonoBehaviour
{
	private NetworkDriver _driver;
	private Dictionary<uint, ReplicatedActorInfo> _actorInfoMap = new Dictionary<uint, ReplicatedActorInfo>();
	private uint _nextNetId = 1;

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