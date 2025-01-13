using UnityEngine;
using System.Collections.Generic;

public class ReplicationDemo : MonoBehaviour
{
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
        CreateTestClients();
        SpawnTestActors();
    }

    private void CreateTestClients()
    {
        var driver = NetworkManager.Instance.Driver;
        
        for (int i = 0; i < clientCount; i++)
        {
            var connection = driver.CreateClientConnection();
            
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
        }
    }

    private void SpawnTestActors()
    {
        for (int i = 0; i < actorCount; i++)
        {
            Vector3 randomPos = Random.insideUnitCircle * spawnRange;
            randomPos = new Vector3(randomPos.x, 0, randomPos.y);

            var actor = new TestActor(randomPos, cullDistance)
            {
                IsMoving = Random.value < moveProbability,
                MoveSpeed = moveSpeed
            };

            spawnedActors.Add(actor);
            NetworkManager.Instance.SpawnNetworkActor(actor);
        }
    }

    private void Update()
    {
        UpdateActors();
        
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
            actor.UpdateMovement(Time.deltaTime);
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