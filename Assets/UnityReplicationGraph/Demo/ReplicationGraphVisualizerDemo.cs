using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static ReplicationGraph.ReplicationGraphVisualizerDemo;

namespace ReplicationGraph
{
	public class ReplicationGraphVisualizerDemo : MonoBehaviour
	{
		[Header("Mock数据配置")]
		[SerializeField] private float _updateInterval = 0.5f;  // 更新间隔
		[SerializeField] private float _moveSpeed = 2f;         // 移动速度
		[SerializeField] private float _moveRange = 10f;        // 移动范围
		[SerializeField] private float _clientViewRadius = 15f; // 客户端视野范围

		public class Actor
		{
			public string Id;
			public Vector3 Position;
			public string Type;
			public bool IsDynamic;

			public Vector3 _initialPosition;  // 保存初始位置作为圆心
			public float _phaseOffset;        // 每个Actor的相位偏移
			public float _moveRange;          // 每个Actor的运动半径

			public Actor(string id, Vector3 position, string type, bool isDynamic, float moveRange)
			{
				Id = id;
				Position = position;
				_initialPosition = position;
				Type = type;
				IsDynamic = isDynamic;
				_moveRange = moveRange;
				_phaseOffset = Random.Range(0f, Mathf.PI * 2f); // 随机初始相位
			}

			public void UpdatePosition(float time, float speed)
			{
				if (!IsDynamic) return;

				float angle = time * speed + _phaseOffset;
				Vector3 offset = new Vector3(
					Mathf.Sin(angle) * _moveRange,
					0,
					Mathf.Cos(angle) * _moveRange
				);

				Position = _initialPosition + offset;
			}
		}

		public class ActorPath : IObserveePath
		{
			public Actor actor;
			public void OnDraw(Color color)
			{
#if UNITY_EDITOR
				UnityEditor.Handles.DrawWireDisc(
						actor._initialPosition,
						Vector3.up,
						actor._moveRange
					);
#endif
			}
		}

		private class Client
		{
			public string Id;
			public Vector3 Position;
			public float ViewRadius;
			public string PlayerActorId;  // 添加对应玩家Actor的ID引用
			public Dictionary<string, float> LastUpdateTimes = new Dictionary<string, float>();

			public bool CanSeeActor(Actor actor)
			{
				return Vector3.Distance(Position, actor.Position) <= ViewRadius;
			}
		}

		private List<Actor> _actors = new List<Actor>();
		private List<Client> _clients = new List<Client>();
		private float _lastUpdateTime;

		private void Start()
		{
			// 创建服务器观察者（全图视野）
			ReplicationGraphVisualizer.AddObserver(ReplicationGraphVisualizer.MODE_SERVER, 0, 0, 0);

			// 先创建玩家角色
			CreateActor("player1", new Vector3(-5, 0, -5), ReplicationGraphVisualizer.TYPE_PLAYER, true);
			CreateActor("player2", new Vector3(5, 0, 5), ReplicationGraphVisualizer.TYPE_PLAYER, true);

			// 创建静态物体
			CreateActor("static1", Vector3.zero, ReplicationGraphVisualizer.TYPE_STATIC, false);
			CreateActor("static2", new Vector3(10, 0, 10), ReplicationGraphVisualizer.TYPE_STATIC, false);
			CreateActor("static3", new Vector3(-10, 0, -10), ReplicationGraphVisualizer.TYPE_STATIC, false);

			// 创建动态物体
			CreateActor("dynamic1", new Vector3(3, 0, 3), ReplicationGraphVisualizer.TYPE_DYNAMIC, true);
			CreateActor("dynamic2", new Vector3(-3, 0, -3), ReplicationGraphVisualizer.TYPE_DYNAMIC, true);

			// 创建客户端，使用对应玩家的位置
			foreach (var actor in _actors.Where(a => a.Type == ReplicationGraphVisualizer.TYPE_PLAYER))
			{
				string clientId = "client" + actor.Id.Substring(6); // 从"player1"提取数字作为"client1"
				CreateClient(clientId, actor.Id);
			}

			// 默认显示服务器视角
			ReplicationGraphVisualizer.SwitchObserver(ReplicationGraphVisualizer.MODE_SERVER);
		}

		private void Update()
		{
			if (Time.time - _lastUpdateTime < _updateInterval) return;
			_lastUpdateTime = Time.time;

			// 更新所有Actor位置，不再传入moveRange参数
			foreach (var actor in _actors)
			{
				actor.UpdatePosition(Time.time, _moveSpeed);

				// 如果是玩家角色，更新对应的客户端观察者位置
				var client = _clients.Find(c => c.PlayerActorId == actor.Id);
				if (client != null)
				{
					client.Position = actor.Position;
					ReplicationGraphVisualizer.UpdateObserver(client.Id,
						client.Position.x, client.Position.y, client.Position.z);
				}
			}

			// 服务器始终知道所有Actor的位置
			foreach (var actor in _actors)
			{
				ReplicationGraphVisualizer.UpdateObservee(
					ReplicationGraphVisualizer.MODE_SERVER,
					actor.Id,
					actor.Position.x,
					actor.Position.y,
					actor.Position.z
				);
			}

			// 根据视野范围更新客户端的可见性
			foreach (var client in _clients)
			{
				foreach (var actor in _actors)
				{
					if (client.CanSeeActor(actor))
					{
						ReplicationGraphVisualizer.UpdateObservee(
							client.Id,
							actor.Id,
							actor.Position.x,
							actor.Position.y,
							actor.Position.z
						);
						client.LastUpdateTimes[actor.Id] = Time.time;
					}
				}
			}
		}

		private void CreateClient(string id, string playerActorId)
		{
			// 获取对应玩家的位置
			var playerActor = _actors.Find(a => a.Id == playerActorId);
			if (playerActor == null) return;

			_clients.Add(new Client
			{
				Id = id,
				Position = playerActor.Position, // 使用玩家的位置
				ViewRadius = _clientViewRadius,
				PlayerActorId = playerActorId
			});

			// 使用玩家位置创建观察者
			ReplicationGraphVisualizer.AddObserver(id,
				playerActor.Position.x,
				playerActor.Position.y,
				playerActor.Position.z);
		}

		private void CreateActor(string id, Vector3 position, string type, bool isDynamic)
		{
			float moveRange = isDynamic ?
				Random.Range(_moveRange * 0.5f, _moveRange * 1.5f) :
				0f;

			var actor = new Actor(id, position, type, isDynamic, moveRange);
			_actors.Add(actor);

			// 先添加被观察者（基础API调用）
			ReplicationGraphVisualizer.AddObservee(
				ReplicationGraphVisualizer.MODE_SERVER,
				id,
				position.x,
				position.y,
				position.z,
				type
			);

			// 获取实例并设置自定义数据和绘制回调
			ReplicationGraphVisualizer.Instance.BindObservePath(ReplicationGraphVisualizer.MODE_SERVER, id, new ActorPath { actor = actor });

			// 检查哪些客户端可以看到这个Actor
			foreach (var client in _clients)
			{
				if (client.CanSeeActor(actor))
				{
					ReplicationGraphVisualizer.AddObservee(
						client.Id,
						id,
						position.x,
						position.y,
						position.z,
						type
					);
					client.LastUpdateTimes[id] = Time.time;
				}
			}
		}

	}
}