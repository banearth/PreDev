using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ReplicationGraph
{
	public class ReplicationGraphVisualizerDemo : MonoBehaviour
	{
		[Header("Mock数据配置")]
		[SerializeField] private float _updateInterval = 0.1f;       // 更新间隔
		[SerializeField] private float _moveSpeed = 1f;              // 移动速度
		[SerializeField] private float _moveRange = 3f;              // 移动范围
		[SerializeField] private float _clientViewRadius = 10f;      // 客户端视野半径

		[Header("可视化配置")]
		[SerializeField] private bool _drawEnable = true;            // 是否启用绘制
		[SerializeField] private Color _actorColor = Color.white;    // Actor颜色
		[SerializeField] private float _smartLabelOffsetMultiple = 1;  // 智能Label整体偏移倍数
		[SerializeField] private float _smartLabelBaseOffset = 0.5f;   // 智能Label基础偏移

		[Header("可见性配置")]
		[SerializeField] private bool _autoDestroyOutOfSightActor = true;  // 是否自动销毁视野外Actor

		private Dictionary<string, HashSet<string>> _clientVisibleActors = new Dictionary<string, HashSet<string>>();

		public class Actor
		{
			public string Id;
			public Vector3 Position;
			public string Type;
			public bool IsDynamic;
			public string OwnedClientId;
			public bool IsOwnedByClient => !string.IsNullOrEmpty(OwnedClientId);

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
				UpdatePosition(0, 0);
			}

			public void UpdatePosition(float duration, float speed)
			{
				if (!IsDynamic) return;
				_phaseOffset+= duration * speed;
				var angle = _phaseOffset;
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
				if (IsOwnedByClient)
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
					ReplicationGraphVisualizerUtils.DrawCirclePath(this._initialPosition, this._moveRange, color);
				}
			}

		}

		private class Client
		{

			public string Id;
			public Vector3 Position;
			public float ViewRadius => _getViewRadius();
			public string PlayerActorId;  // 添加对应玩家Actor的ID引用
			public Dictionary<string, float> LastUpdateTimes = new Dictionary<string, float>();

			private System.Func<float> _getViewRadius = null;
			public Client(System.Func<float> getViewRadius)
			{
				_getViewRadius = getViewRadius;
			}
			public bool CanSeeActor(Actor actor)
			{
				var viewRadius = ViewRadius;
				return Vector3.SqrMagnitude(Position - actor.Position) <= viewRadius * viewRadius;
			}
		}

		private List<Actor> _actors = new List<Actor>();
		private Dictionary<string, Client> _clients = new Dictionary<string, Client>();
		private float _lastUpdateTime;
		private Actor _draggingActor = null;
		private Vector3 _dragOffset;
		private Camera _camera;
		private SmartLabel _actorLabel; // 用于显示Actor名字的标签管理器

		private void Start()
		{
			_camera = Camera.main;
			// 初始化ActorLabel，使用序列化的参数
			_actorLabel = new SmartLabel(_smartLabelOffsetMultiple, _smartLabelBaseOffset);
			// 创建服务器观察者（全图视野）
			ReplicationGraphVisualizer.AddObserver(ReplicationGraphVisualizer.MODE_SERVER, 0, 0, 0, -1);

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
				actor.OwnedClientId = clientId;
			}

			// 默认显示服务器视角
			ReplicationGraphVisualizer.SwitchObserver(ReplicationGraphVisualizer.MODE_SERVER);
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Space))
			{
				_drawEnable = !_drawEnable;
			}

			// 处理拖拽逻辑
			HandleActorDragging();

			// 只有在非拖拽状态下才更新 Actor 的自动移动
			if (Time.time - _lastUpdateTime >= _updateInterval)
			{
				_lastUpdateTime = Time.time;
				var deltaTime = Time.deltaTime;
				// Actor进行移动
				foreach (var actor in _actors)
				{
					if (actor != _draggingActor)  // 不是正在拖拽的 Actor 才更新位置
					{
						actor.UpdatePosition(deltaTime, _moveSpeed);
					}
				}
			}
			
			// Actor进行移动
			foreach (var actor in _actors)
			{
				if (actor.IsOwnedByClient)
				{
					// 更新客户端观察者的位置，使用对应玩家的位置
					if (_clients.TryGetValue(actor.OwnedClientId, out var client))
					{
						// 同步客户端位置到玩家位置
						client.Position = actor.Position;
						
						ReplicationGraphVisualizer.UpdateObserver(
							actor.OwnedClientId,
							actor.Position.x, 
							actor.Position.y,
							actor.Position.z,
							_clientViewRadius);
					}
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
			UpdateVisibility();
		}

		private void UpdateVisibility()
		{
			foreach (var client in _clients.Values)
			{
				// 确保每个client都有对应的可见性集合
				if (!_clientVisibleActors.ContainsKey(client.Id))
				{
					_clientVisibleActors[client.Id] = new HashSet<string>();
				}
				
				var currentVisibleActors = _clientVisibleActors[client.Id];
				var previousVisibleActors = new HashSet<string>(currentVisibleActors); // 保存上一帧的可见性状态
				currentVisibleActors.Clear();
				
				// 检查所有Actor的可见性
				foreach (var actor in _actors)
				{
					bool isVisible = client.CanSeeActor(actor);
					bool wasVisible = previousVisibleActors.Contains(actor.Id);

					if (isVisible)
					{
						// Actor在视野内，更新或添加
						currentVisibleActors.Add(actor.Id);
						client.LastUpdateTimes[actor.Id] = Time.time;
						ReplicationGraphVisualizer.UpdateObservee(client.Id, actor.Id);
					}
					else if (wasVisible && _autoDestroyOutOfSightActor)
					{
						// Actor刚离开视野，主动通知销毁
						RemoveActorFromClient(client.Id, actor.Id);
					}
				}
			}
		}

		// 从客户端移除Actor
		private void RemoveActorFromClient(string clientId, string actorId)
		{
			if (_clientVisibleActors.ContainsKey(clientId))
			{
				_clientVisibleActors[clientId].Remove(actorId);
			}
			
			if (_clients.TryGetValue(clientId, out var client))
			{
				client.LastUpdateTimes.Remove(actorId);
			}
			
			// 通知可视化系统移除Actor
			ReplicationGraphVisualizer.RemoveObservee(clientId, actorId);
		}

		private void OnDrawGizmos()
		{
			if(!Application.isPlaying)
			{
				return;
			}
			if(_drawEnable)
			{
				foreach (var actor in _actors)
				{
					// 绘制Actor本体和路径
					actor.OnDrawActor(_actorColor);
					actor.OnDrawActorPath(_actorColor);

					// 绘制标签，不需要每次传入方向	
					_actorLabel.Clear();
					_actorLabel.Add(actor.Id, _actorColor);
					_actorLabel.Draw(actor.Position);
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

		private float GetClientViewRadius()
		{
			return _clientViewRadius;
		}

		private void CreateClient(string clientId, string playerActorId)
		{
			// 获取对应玩家的位置
			var playerActor = _actors.Find(a => a.Id == playerActorId);
			if (playerActor == null) return;

			var client = new Client(GetClientViewRadius)
			{
				Id = clientId,
				Position = playerActor.Position, // 初始化时使用玩家位置
				PlayerActorId = playerActorId
			};
			_clients.Add(clientId, client);

			// 使用玩家位置创建观察者
			ReplicationGraphVisualizer.AddObserver(clientId,
				playerActor.Position.x,
				playerActor.Position.y,
				playerActor.Position.z,
				_clientViewRadius);
		}

		private void CreateActor(string id, Vector3 position, string type, bool isDynamic)
		{
			float moveRange = isDynamic ?
				Random.Range(_moveRange * 0.5f, _moveRange * 1.5f) :
				0f;

			var actor = new Actor(id, position, type, isDynamic, moveRange);
			_actors.Add(actor);

			// 添加到全局被观察者
			ReplicationGraphVisualizer.AddGlobalObservee(
				id,
				position.x,
				position.y,
				position.z,
				type);
		}

		private void OnValidate()
		{
			// 当在Inspector中修改offset时，更新SmartLabel的设置
			if (_actorLabel != null)
			{
				_actorLabel.SetOffsetMultiple(_smartLabelOffsetMultiple);
				_actorLabel.SetBaseOffset(_smartLabelBaseOffset);
			}
		}
	}
}