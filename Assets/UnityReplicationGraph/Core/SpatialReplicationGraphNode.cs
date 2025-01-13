using System.Collections.Generic;

public class SpatialReplicationGraphNode : ReplicationGraphNode
{
    private List<ReplicatedActorInfo> _actorList = new List<ReplicatedActorInfo>();

    public override void NotifyAddNetworkActor(ReplicatedActorInfo actorInfo)
    {
        _actorList.Add(actorInfo);
    }

    public override void NotifyRemoveNetworkActor(ReplicatedActorInfo actorInfo)
    {
        _actorList.Remove(actorInfo);
    }
} 