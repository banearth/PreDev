using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

public class ReplicationDemo : MonoBehaviour
{
    private NetworkDriver networkDriver;
    private BasicReplicationGraph replicationGraph;
    private List<TestActor> spawnedActors = new List<TestActor>();
    private Dictionary<NetworkConnection, NetViewer> connectionViewers = new Dictionary<NetworkConnection, NetViewer>();
    private ReplicationGraphDebugger debugger;

    [Header("Network Setup")]
    [SerializeField] private int clientCount = 3;
    [SerializeField] private float clientViewRadius = 50f;
    
    [Header("Actor Setup")]
    [SerializeField] private int actorCount = 10;
    [SerializeField] private float spawnRange = 100f;
    [SerializeField] private float cullDistance = 50f;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float moveProbability = 0.5f;

    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugGizmos = true;

    private void Start()
    {
        debugger = new ReplicationGraphDebugger();
        InitializeNetwork();
        SpawnTestActors();
    }

    private void InitializeNetwork()
    {
        networkDriver = new NetworkDriver();
        replicationGraph = new BasicReplicationGraph();
        replicationGraph.InitForNetDriver(networkDriver);

        CreateTestClients();
    }

    private void CreateTestClients()
    {
        for (int i = 0; i < clientCount; i++)
        {
            var connection = networkDriver.CreateClientConnection();
            
            // 创建并设置Viewer
            float angle = (360f / clientCount) * i;
            float radius = spawnRange * 0.5f;
            Vector3 position = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                0,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );
            
            var viewer = new NetViewer(connection);
            viewer.ViewLocation = position;
            connectionViewers[connection] = viewer;

            replicationGraph.AddClientConnection(connection);
        }
    }

    private void SpawnTestActors()
    {
        for (int i = 0; i < actorCount; i++)
        {
            // 随机位置生成Actor
            Vector3 randomPos = Random.insideUnitCircle * spawnRange;
            randomPos = new Vector3(randomPos.x, 0, randomPos.y);

            var actor = new TestActor(randomPos, cullDistance)
            {
                IsMoving = Random.value < moveProbability,
                MoveSpeed = moveSpeed
            };

            spawnedActors.Add(actor);
            replicationGraph.AddNetworkActor(actor);
        }
    }

    private void Update()
    {
        // 更新Actor位置
        UpdateActors();
        
        // 调试绘制
        if (showDebugGizmos)
        {
            debugger.DrawViewers(connectionViewers.Values, clientViewRadius);
            debugger.DrawActors(spawnedActors, IsActorVisibleToAnyViewer);
        }
    }

    private void UpdateActors()
    {
        foreach (var actor in spawnedActors)
        {
            if (actor.IsMoving)
            {
                // 简单的圆周运动
                float angle = Time.time * actor.MoveSpeed;
                Vector3 center = actor.InitialPosition;
                float radius = actor.MoveRadius;
                
                actor.Position = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );
            }
        }
    }

    private bool IsActorVisibleToAnyViewer(TestActor actor)
    {
        foreach (var viewer in connectionViewers.Values)
        {
            float distanceSq = (actor.Position - viewer.ViewLocation).sqrMagnitude;
            if (distanceSq <= (clientViewRadius * clientViewRadius))
            {
                return true;
            }
        }
        return false;
    }
}