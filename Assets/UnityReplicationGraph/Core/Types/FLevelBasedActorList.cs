public class FLevelBasedActorList
{
    public FActorRepListRefView PermanentLevelActors;
    public FStreamingLevelActorListCollection StreamingLevelActors;
	public FLevelBasedActorList(FActorRepListRefView PermanentLevelActors, FStreamingLevelActorListCollection StreamingLevelActors)
	{
		this.PermanentLevelActors = PermanentLevelActors;
		this.StreamingLevelActors = StreamingLevelActors;
	}
	public void Gather(UNetReplicationGraphConnection ConnectionManager, FGatheredReplicationActorLists OutGatheredList)
	{
		OutGatheredList.AddReplicationActorList(PermanentLevelActors);
		StreamingLevelActors.Gather(ConnectionManager, OutGatheredList);
	}
}