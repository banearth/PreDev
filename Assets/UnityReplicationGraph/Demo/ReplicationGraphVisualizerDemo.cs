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

        // 创建客户端，并关联到对应的玩家
        CreateClient("client1", new Vector3(-5, 0, -5), "player1");
        CreateClient("client2", new Vector3(5, 0, 5), "player2");

        // 创建静态物体
        CreateActor("static1", Vector3.zero, ReplicationGraphVisualizer.TYPE_STATIC, false);
        CreateActor("static2", new Vector3(10, 0, 10), ReplicationGraphVisualizer.TYPE_STATIC, false);
        CreateActor("static3", new Vector3(-10, 0, -10), ReplicationGraphVisualizer.TYPE_STATIC, false);

        // 创建动态物体
        CreateActor("dynamic1", new Vector3(3, 0, 3), ReplicationGraphVisualizer.TYPE_DYNAMIC, true);
        CreateActor("dynamic2", new Vector3(-3, 0, -3), ReplicationGraphVisualizer.TYPE_DYNAMIC, true);

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

    private void CreateClient(string id, Vector3 position, string playerActorId)
    {
        _clients.Add(new Client 
        { 
            Id = id, 
            Position = position,
            ViewRadius = _clientViewRadius,
            PlayerActorId = playerActorId
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