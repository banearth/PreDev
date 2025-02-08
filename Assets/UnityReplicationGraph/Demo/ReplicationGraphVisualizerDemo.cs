using UnityEngine;
using System.Collections.Generic;

public class ReplicationGraphVisualizerDemo : MonoBehaviour
{
    [Header("Mock数据配置")]
    [SerializeField] private float _updateInterval = 0.5f;  // 更新间隔
    [SerializeField] private float _moveSpeed = 2f;         // 移动速度
    [SerializeField] private float _moveRange = 10f;        // 移动范围
    [SerializeField] private float _clientViewRadius = 15f; // 客户端视野范围

    private class Actor
    {
        public string Id;
        public Vector3 Position;
        public string Type;
        public bool IsDynamic;

        public void UpdatePosition(float time, float speed, float range)
        {
            if (!IsDynamic) return;
            Position = new Vector3(
                Mathf.Sin(time * speed) * range,
                0,
                Mathf.Cos(time * speed) * range
            );
        }
    }

    private class Client
    {
        public string Id;
        public Vector3 Position;
        public float ViewRadius;
        public float PhaseOffset;  // 添加相位偏移属性
        public Dictionary<string, float> LastUpdateTimes = new Dictionary<string, float>();

        public bool CanSeeActor(Actor actor)
        {
            return Vector3.Distance(Position, actor.Position) <= ViewRadius;
        }

        public void UpdatePosition(float time, float speed, float range)
        {
            Position = new Vector3(
                Mathf.Sin((time * speed) + PhaseOffset) * range,
                0,
                Mathf.Cos((time * speed) + PhaseOffset) * range
            );
        }
    }

    private List<Actor> _actors = new List<Actor>();
    private List<Client> _clients = new List<Client>();
    private float _lastUpdateTime;

    private void Start()
    {
        // 创建服务器观察者（全图视野）
        ReplicationGraphVisualizer.AddObserver(ReplicationGraphVisualizer.MODE_SERVER, 0, 10, 0);

        // 创建客户端
        CreateClient("client1", new Vector3(-5, 0, -5));
        CreateClient("client2", new Vector3(5, 0, 5));

        // 创建静态物体
        CreateActor("static1", Vector3.zero, ReplicationGraphVisualizer.TYPE_STATIC, false);
        CreateActor("static2", new Vector3(10, 0, 10), ReplicationGraphVisualizer.TYPE_STATIC, false);
        CreateActor("static3", new Vector3(-10, 0, -10), ReplicationGraphVisualizer.TYPE_STATIC, false);

        // 创建动态物体
        CreateActor("dynamic1", new Vector3(3, 0, 3), ReplicationGraphVisualizer.TYPE_DYNAMIC, true);
        CreateActor("dynamic2", new Vector3(-3, 0, -3), ReplicationGraphVisualizer.TYPE_DYNAMIC, true);

        // 创建玩家角色
        CreateActor("player1", new Vector3(-5, 0, -5), ReplicationGraphVisualizer.TYPE_PLAYER, true);
        CreateActor("player2", new Vector3(5, 0, 5), ReplicationGraphVisualizer.TYPE_PLAYER, true);

        // 默认显示服务器视角
        ReplicationGraphVisualizer.SwitchObserver(ReplicationGraphVisualizer.MODE_SERVER);
    }

    private void Update()
    {
        if (Time.time - _lastUpdateTime < _updateInterval) return;
        _lastUpdateTime = Time.time;

        // 更新所有Actor位置
        foreach (var actor in _actors)
        {
            actor.UpdatePosition(Time.time, _moveSpeed, _moveRange);
        }

        // 更新所有Client位置
        foreach (var client in _clients)
        {
            client.UpdatePosition(Time.time, _moveSpeed, _moveRange);
            ReplicationGraphVisualizer.UpdateObserver(client.Id, client.Position.x, client.Position.y, client.Position.z);
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
                    // 在视野范围内，更新位置
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

    private void CreateClient(string id, Vector3 position)
    {
        _clients.Add(new Client 
        { 
            Id = id, 
            Position = position,
            ViewRadius = _clientViewRadius,
            PhaseOffset = Random.Range(0f, Mathf.PI * 2f)  // 随机相位 0-360度
        });
        ReplicationGraphVisualizer.AddObserver(id, position.x, position.y, position.z);
    }

    private void CreateActor(string id, Vector3 position, string type, bool isDynamic)
    {
        var actor = new Actor
        {
            Id = id,
            Position = position,
            Type = type,
            IsDynamic = isDynamic
        };
        _actors.Add(actor);
        
        // 服务器始终知道所有Actor
        ReplicationGraphVisualizer.AddObservee(
            ReplicationGraphVisualizer.MODE_SERVER,
            id,
            position.x,
            position.y,
            position.z,
            type
        );
        
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