using UnityEngine;

public class ReplicatedActorInfo
{
    public ReplicatedActor Actor { get; private set; }
    public Vector3 Location { get; set; }
    public float CullDistance { get; set; }
    public ClassReplicationInfo ClassInfo { get; set; }
    public uint NetId { get; set; }

    public ReplicatedActorInfo(ReplicatedActor actor)
    {
        Actor = actor;
        Location = actor.Position;
        CullDistance = actor.NetCullDistanceSquared;
        NetId = actor.NetId;
    }
}