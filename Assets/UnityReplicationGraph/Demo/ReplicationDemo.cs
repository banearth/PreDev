using UnityEngine;
using System.Collections.Generic;

public class ReplicationDemo : MonoBehaviour
{
    private List<TestActor> spawnedActors = new List<TestActor>();

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

    private UNetworkDriver networkDriver;
    private UReplicationGraph replicationGraph;

    private void Start()
    {
        InitializeNetwork();
        CreateTestClients();
        SpawnTestActors();
    }

    private void InitializeNetwork()
    {
        networkDriver = new UNetworkDriver();
        replicationGraph = new UBasicReplicationGraph();
        networkDriver.InitReplicationDriver(replicationGraph);
    }

    private void CreateTestClients()
    {
        for (int i = 0; i < clientCount; i++)
        {
            var connection = networkDriver.CreateClientConnection();
            
            float angle = (360f / clientCount) * i;
            float radius = spawnRange * 0.5f;
            var spawnPosition = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                0,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );

            var testActor = new TestActor(name: $"Client_{i}_Actor",position: spawnPosition,cullDistance)
            {
                IsMoving = Random.value < moveProbability,
                MoveSpeed = moveSpeed
            };

            connection.OwningActor = testActor;
            connection.ViewTarget = testActor;
            
            replicationGraph.AddNetworkActor(testActor);
        }
    }

    private void SpawnTestActors()
    {
        for (int i = 0; i < actorCount; i++)
        {
            Vector3 randomPos = Random.insideUnitCircle * spawnRange;
            randomPos = new Vector3(randomPos.x, 0, randomPos.y);
            var actor = new TestActor($"Test:{i}",randomPos, cullDistance)
            {
                IsMoving = Random.value < moveProbability,
                MoveSpeed = moveSpeed,
            };
            spawnedActors.Add(actor);
            replicationGraph.AddNetworkActor(actor);
        }
    }

    private void Update()
    {
        UpdateActors();
        networkDriver.TickFlush(Time.deltaTime);
        
        if (showDebugGizmos)
        {
            var viewers = new List<FNetViewer>();
            foreach (var conn in networkDriver.ClientConnections)
            {
                viewers.Add(new FNetViewer(conn));
            }
            
            ReplicationGraphDebugger.DrawViewers(viewers, clientViewRadius);
            ReplicationGraphDebugger.DrawActors(spawnedActors, IsActorVisibleToAnyViewer);
        }
    }

    private void UpdateActors()
    {
        foreach (var actor in spawnedActors)
        {
            actor.UpdateMovement(Time.deltaTime);
        }
    }

    private bool IsActorVisibleToAnyViewer(TestActor actor)
    {
        foreach (var conn in networkDriver.ClientConnections)
        {
            var viewer = new FNetViewer(conn);
            float distanceSq = (actor.Position - viewer.ViewLocation).sqrMagnitude;
            if (distanceSq <= (clientViewRadius * clientViewRadius))
            {
                return true;
            }
        }
        return false;
    }

    //private void OnDrawGizmos()
    //{
    //    if (!Application.isPlaying || !showDebugGizmos)
    //        return;

    //    // 绘制调试可视化
    //    Gizmos.color = Color.yellow;
    //    foreach (var viewer in connectionViewers.Values)
    //    {
    //        Gizmos.DrawWireSphere(viewer.ViewLocation, clientViewRadius);
    //    }

    //    foreach (var actor in spawnedActors)
    //    {
    //        Gizmos.color = IsActorVisibleToAnyViewer(actor) ? Color.green : Color.red;
    //        Gizmos.DrawSphere(actor.Position, 1f);
    //    }
    //}


}