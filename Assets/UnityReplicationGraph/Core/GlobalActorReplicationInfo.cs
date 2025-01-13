using UnityEngine;

public class GlobalActorReplicationInfo
{
    public ReplicatedActor Actor { get; set; }
    public Vector3 Position { get; set; }
    public float CullDistance { get; set; }
    public ClassReplicationInfo ClassInfo { get; set; }
} 