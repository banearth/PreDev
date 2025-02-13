using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ReplicationGraph
{
	public class ReplicationGraphVisualizerDemo : MonoBehaviour
	{
		[Header("Mock数据配置")]
		[SerializeField] private float _updateInterval = 0.5f;  // 更新间隔
		[SerializeField] private float _moveSpeed = 2f;         // 移动速度
		[SerializeField] private float _moveRange = 10f;        // 移动范围
		[SerializeField] private float _clientViewRadius = 15f; // 客户端视野范围

		[SerializeField] private bool _drawActorEnable = true;
		[SerializeField] private bool _drawActorPathEnable = true;
		[SerializeField] private Color _actorColor = new Color(1,1,1,0.1f); 
		public class Actor
		{
			public string Id;
			public Vector3 Position;
			public string Type;
			public bool IsDynamic;

			public Vector3 _initialPosition;  // 保存初始位置作为圆心
			public float _phaseOffset;        // 每个Actor的相位偏移
			public float _moveRange;          // 每个Actor的运动半径
			public bool _ownedByClient;

			public Actor(string id, Vector3 position, string type, bool isDynamic, float moveRange, bool ownedByClient)
			{
				Id = id;
				Position = position;
				_initialPosition = position;
				Type = type;
				IsDynamic = isDynamic;
				_moveRange = moveRange;
				_phaseOffset = Random.Range(0f, Mathf.PI * 2f); // 随机初始相位
				_ownedByClient = ownedByClient;
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

			public void OnDrawActor(Color color)
			{
				// 绘制本体
				if(_ownedByClient)
				{
					ReplicationGraphVisualizerUtils.DrawPlayerCharacter(this.Position, color);
				}
				else if (IsDynamic)
				{
					ReplicationGraphVisualizerUtils.DrawDynamicActor(this.Position, color);
				}
				else
				{
					ReplicationGraphVisualizerUtils.DrawStaticActor(this.Position, color);
				}
			}

			public void OnDrawActorPath(Color color)
			{
				// 绘制路径
				if (IsDynamic)
				{
					ReplicationGraphVisualizerUtils.DrawWireCircle(this._initialPosition, this._moveRange, color);
				}
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
		private Actor _draggingActor = null;
		private Vector3 _dragOffset;
		private Camera _camera;
		private void Start()
		{
			_camera = Camera.main;
			// 创建服务器观察者（全图视野）
			ReplicationGraphVisualizer.AddObserver(ReplicationGraphVisualizer.MODE_SERVER, 0, 0, 0);

			// 先创建玩家角色
			CreateActor("player1", new Vector3(-5, 0, -5), ReplicationGraphVisualizer.TYPE_PLAYER, true, true);
			CreateActor("player2", new Vector3(5, 0, 5), ReplicationGraphVisualizer.TYPE_PLAYER, true, true);

			// 创建静态物体
			CreateActor("static1", Vector3.zero, ReplicationGraphVisualizer.TYPE_STATIC, false, false);
			CreateActor("static2", new Vector3(10, 0, 10), ReplicationGraphVisualizer.TYPE_STATIC, false, false);
			CreateActor("static3", new Vector3(-10, 0, -10), ReplicationGraphVisualizer.TYPE_STATIC, false, false);

			// 创建动态物体
			CreateActor("dynamic1", new Vector3(3, 0, 3), ReplicationGraphVisualizer.TYPE_DYNAMIC, true, false);
			CreateActor("dynamic2", new Vector3(-3, 0, -3), ReplicationGraphVisualizer.TYPE_DYNAMIC, true, false);

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
			// 处理拖拽逻辑
			HandleActorDragging();

			// 只有在非拖拽状态下才更新 Actor 的自动移动
			if (Time.time - _lastUpdateTime < _updateInterval) return;
			_lastUpdateTime = Time.time;

			foreach (var actor in _actors)
			{
				if (actor != _draggingActor)  // 不是正在拖拽的 Actor 才更新位置
				{
					actor.UpdatePosition(Time.time, _moveSpeed);
				}

				// 如果是玩家角色，更新对应的客户端观察者位置
				var client = _clients.Find(c => c.PlayerActorId == actor.Id);
				if (client != null)
				{
					client.Position = actor.Position;
					ReplicationGraphVisualizer.UpdateObserver(client.Id,
						client.Position.x, client.Position.y, client.Position.z);
				}
			}

			// 更新全局被观察者的位置
			foreach (var actor in _actors)
			{
				ReplicationGraphVisualizer.UpdateGlobalObservee(
					actor.Id, 
					actor.Position.x,
					actor.Position.y,
					actor.Position.z);
			}

			// 服务器始终知道所有Actor的位置
			foreach (var actor in _actors)
			{
				ReplicationGraphVisualizer.UpdateObservee(
					ReplicationGraphVisualizer.MODE_SERVER,
					actor.Id
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
							actor.Id
						);
						client.LastUpdateTimes[actor.Id] = Time.time;
					}
				}
			}
		}

		private void OnDrawGizmos()
		{
			if(!Application.isPlaying)
			{
				return;
			}
			if(_drawActorEnable)
			{
				foreach (var actor in _actors)
				{
					actor.OnDrawActor(_actorColor);
				}
			}
			if (_drawActorPathEnable)
			{
				foreach (var actor in _actors)
				{
					actor.OnDrawActorPath(_actorColor);
				}
			}
		}

		private void HandleActorDragging()
		{
			if (Input.GetMouseButtonDown(0))
			{
				// 尝试选中 Actor
				Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
				RaycastHit[] hits = Physics.RaycastAll(ray);
				
				// 由于我们没有实际的碰撞体，我们需要手动检查点击位置是否在 Actor 范围内
				Vector3 clickWorldPos = GetWorldPositionFromMouse();
				_draggingActor = FindClickedActor(clickWorldPos);
				
				if (_draggingActor != null)
				{
					// 记录点击位置与 Actor 位置的偏移
					_dragOffset = _draggingActor.Position - clickWorldPos;
				}
			}
			else if (Input.GetMouseButton(0) && _draggingActor != null)
			{
				// 更新被拖拽 Actor 的位置
				Vector3 newPos = GetWorldPositionFromMouse() + _dragOffset;
				Vector3 movement = newPos - _draggingActor.Position;
				
				// 更新 Actor 的当前位置和初始位置（圆心）
				_draggingActor.Position = newPos;
				_draggingActor._initialPosition += movement;
			}
			else if (Input.GetMouseButtonUp(0) && _draggingActor != null)
			{
				// 释放拖拽的 Actor
				_draggingActor = null;
			}
		}

		private Vector3 GetWorldPositionFromMouse()
		{
			// 获取鼠标在世界空间中的位置（在XZ平面上）
			Plane plane = new Plane(Vector3.up, Vector3.zero);
			Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
			
			if (plane.Raycast(ray, out float distance))
			{
				Vector3 worldPosition = ray.GetPoint(distance);
				return new Vector3(worldPosition.x, 0, worldPosition.z);
			}
			
			return Vector3.zero;
		}

		private Actor FindClickedActor(Vector3 clickPosition)
		{
			const float clickRadius = 0.5f; // 点击检测半径
			return _actors.FirstOrDefault(actor => 
				Vector3.Distance(new Vector3(actor.Position.x, 0, actor.Position.z), 
							   new Vector3(clickPosition.x, 0, clickPosition.z)) <= clickRadius);
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

		private void CreateActor(string id, Vector3 position, string type, bool isDynamic,bool ownedByClient)
		{
			float moveRange = isDynamic ?
				Random.Range(_moveRange * 0.5f, _moveRange * 1.5f) :
				0f;

			var actor = new Actor(id, position, type, isDynamic, moveRange, ownedByClient);
			_actors.Add(actor);

			// 添加到全局被观察者
			ReplicationGraphVisualizer.AddGlobalObservee(
				id,
				position.x,
				position.y,
				position.z,
				type);
		}

	}
}