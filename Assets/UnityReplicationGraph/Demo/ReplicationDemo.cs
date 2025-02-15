using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ReplicationGraph;

public class ReplicationDemo : MonoBehaviour
{
    [Header("Mock数据配置")]
    [SerializeField] private float updateInterval = 0.1f;       // 更新间隔
    [SerializeField] private float moveSpeed = 5f;             // 移动速度
    [SerializeField] private float moveRange = 3f;             // 移动范围
    [SerializeField] private float clientViewRadius = 50f;     // 客户端视野半径
    
    [Header("出生配置")]
	[SerializeField] private Color _spawnAreaColor = new Color(1, 0, 0, 0.5f);
	[SerializeField] private int staticActorCount = 10;        // 静态Actor数量
    [SerializeField] private int dynamicActorCount = 10;       // 动态Actor数量
    [SerializeField] private int playerActorCount = 3;         // 玩家Actor数量
    [SerializeField] private Rect spawnAreaRect = new Rect(-50, -50, 100, 100);

    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugGizmos = true;

    [Header("可视化配置")]
    [SerializeField] private bool drawEnable = true;            // 是否启用绘制
    [SerializeField] private Color actorColor = Color.white;    // Actor颜色
    [SerializeField] private float smartLabelOffsetMultiple = 1;  // 智能Label整体偏移倍数
    [SerializeField] private float smartLabelBaseOffset = 0.5f;   // 智能Label基础偏移

    private List<TestActor> spawnedActors = new List<TestActor>();
    private float lastUpdateTime;

    private Camera _camera;
    private SmartLabel _actorLabel; // 用于显示Actor名字的标签管理器
    private TestActor _draggingActor = null;
    private Vector3 _dragStartPosition;  // 记录开始拖拽时Actor的位置
    private Vector3 _dragOffset;         // 记录鼠标点击位置与Actor位置的偏移
    private Vector3 _dragStartInitialPosition; // 记录开始拖拽时的运动圆心

    private Vector3 GetRandomSpawnPosition()
    {
        return new Vector3(
            Random.Range(spawnAreaRect.xMin, spawnAreaRect.xMax),
            0,
            Random.Range(spawnAreaRect.yMin, spawnAreaRect.yMax)
        );
    }

    private void Start()
    {
        _camera = Camera.main;
        // 初始化ActorLabel，使用序列化的参数
        _actorLabel = new SmartLabel(smartLabelOffsetMultiple, smartLabelBaseOffset);

        SpawnActors();
        CreateClients();
    }

    private void SpawnActors()
    {
        // 先创建玩家角色
        for (int i = 0; i < playerActorCount; i++)
        {
            var actor = CreateActor($"player{i}", GetRandomSpawnPosition(), ReplicationGraphVisualizer.TYPE_PLAYER, true);
            spawnedActors.Add(actor);
        }

        // 创建静态物体
        for (int i = 0; i < staticActorCount; i++)
        {
            var actor = CreateActor($"static{i}", GetRandomSpawnPosition(), ReplicationGraphVisualizer.TYPE_STATIC, false);
            spawnedActors.Add(actor);
        }

        // 创建动态物体
        for (int i = 0; i < dynamicActorCount; i++)
        {
            var actor = CreateActor($"dynamic{i}", GetRandomSpawnPosition(), ReplicationGraphVisualizer.TYPE_DYNAMIC, true);
            spawnedActors.Add(actor);
        }
    }

    private TestActor CreateActor(string id, Vector3 position, string type, bool isDynamic)
    {
        float actorMoveRange = isDynamic ? 
            Random.Range(moveRange * 0.5f, moveRange * 1.5f) : 
            0f;
		var actor = new TestActor(id, position, type, isDynamic, actorMoveRange, moveSpeed);
		NetworkManager.Instance.SpawnNetworkActor(actor, type);
		return actor;
    }

    private void CreateClients()
    {
        var playerActors = spawnedActors.Where(a => a.Type == ReplicationGraphVisualizer.TYPE_PLAYER).ToList();
        
        for (int i = 0; i < playerActors.Count; i++)
        {
            var playerActor = playerActors[i];
            string clientId = $"client{i}";
			playerActor.OwnedClientId = clientId;

			var connection = NetworkManager.Instance.Driver.CreateClientConnection();
			connection.ViewTarget = playerActor;
			connection.OwningActor = playerActor;

			ReplicationGraphVisualizer.AddObserver(
                clientId,
                playerActor.Position.x,
                playerActor.Position.y,
                playerActor.Position.z,
                clientViewRadius
            );
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            drawEnable = !drawEnable;
        }

		// 处理拖拽逻辑
		HandleActorDragging();

		// Actor 的自动移动
		if (Time.time - lastUpdateTime >= updateInterval)
        {
            float deltaTime = Time.time - lastUpdateTime;
            lastUpdateTime = Time.time;
            UpdateActors(deltaTime);
        }
    }

    private void UpdateActors(float deltaTime)
    {
        foreach (var actor in spawnedActors)
        {
            // 如果正在拖拽该Actor，跳过自动移动更新
            if (actor == _draggingActor) continue;

            actor.UpdateMovement(deltaTime);
            
            ReplicationGraphVisualizer.UpdateGlobalObservee(
                actor.Id,
                actor.Position.x,
                actor.Position.y,
                actor.Position.z
            );
        }

        // 如果有正在拖拽的Actor，单独更新它的位置
        if (_draggingActor != null)
        {
            ReplicationGraphVisualizer.UpdateGlobalObservee(
                _draggingActor.Id,
                _draggingActor.Position.x,
                _draggingActor.Position.y,
                _draggingActor.Position.z
            );
        }
    }

    private bool IsActorVisibleToViewer(TestActor actor, FNetViewer viewer)
    {
        float distanceSq = (actor.Position - viewer.ViewLocation).sqrMagnitude;
        return distanceSq <= (clientViewRadius * clientViewRadius);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
			Gizmos.color = _spawnAreaColor;
            Gizmos.DrawWireCube(
                new Vector3(spawnAreaRect.center.x, 0, spawnAreaRect.center.y),
                new Vector3(spawnAreaRect.width, 0, spawnAreaRect.height)
            );
            return;
        }

        if (drawEnable)
        {
            foreach (var actor in spawnedActors)
            {
                if (actor.IsOwnedByClient)
                {
                    ReplicationGraphVisualizerUtils.DrawPlayerCharacter(actor.Position, actorColor);
                }
                else if (actor.IsDynamic)
                {
                    ReplicationGraphVisualizerUtils.DrawDynamicActor(actor.Position, actorColor);
                }
                else
                {
                    ReplicationGraphVisualizerUtils.DrawStaticActor(actor.Position, actorColor);
                }

                if (actor.IsDynamic)
                {
                    ReplicationGraphVisualizerUtils.DrawCirclePath(actor.InitialPosition, actor.MoveRange, actorColor);
                }

                _actorLabel.Clear();
                _actorLabel.Add(actor.Id, actorColor);
                _actorLabel.Draw(actor.Position);
            }
        }
    }

    private void HandleActorDragging()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 clickWorldPos = GetWorldPositionFromMouse();
            _draggingActor = FindClickedActor(clickWorldPos);
            
            if (_draggingActor != null)
            {
                // 记录开始拖拽时的位置、圆心和偏移
                _dragStartPosition = _draggingActor.Position;
                _dragStartInitialPosition = _draggingActor.InitialPosition;
                _dragOffset = _draggingActor.Position - clickWorldPos;
            }
        }
        else if (Input.GetMouseButton(0) && _draggingActor != null)
        {
            // 计算拖拽的位移
            Vector3 newPos = GetWorldPositionFromMouse() + _dragOffset;
            Vector3 movement = newPos - _dragStartPosition;
            
            // 同时更新当前位置和运动圆心，保持相对关系
            _draggingActor.Position = newPos;
            _draggingActor.InitialPosition = _dragStartInitialPosition + movement;
        }
        else if (Input.GetMouseButtonUp(0) && _draggingActor != null)
        {
            _draggingActor = null;
        }
    }

    private Vector3 GetWorldPositionFromMouse()
    {
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        
        if (plane.Raycast(ray, out float distance))
        {
            Vector3 worldPosition = ray.GetPoint(distance);
            return new Vector3(worldPosition.x, 0, worldPosition.z);
        }
        
        return Vector3.zero;
    }

    private TestActor FindClickedActor(Vector3 clickPosition)
    {
        const float clickRadius = 0.5f;
        return spawnedActors.FirstOrDefault(actor => 
            Vector3.Distance(new Vector3(actor.Position.x, 0, actor.Position.z), 
                           new Vector3(clickPosition.x, 0, clickPosition.z)) <= clickRadius);
    }

    private void OnValidate()
    {
        if (_actorLabel != null)
        {
            _actorLabel.SetOffsetMultiple(smartLabelOffsetMultiple);
            _actorLabel.SetBaseOffset(smartLabelBaseOffset);
        }
    }
}