public class FLevelBasedActorList
{
    public FActorRepListRefView PermanentLevelActors = new FActorRepListRefView();
    public FStreamingLevelActorListCollection StreamingLevelActors = new FStreamingLevelActorListCollection();
	public void Gather(UNetReplicationGraphConnection ConnectionManager, FGatheredReplicationActorLists OutGatheredList)
	{
		OutGatheredList.AddReplicationActorList(PermanentLevelActors);
		StreamingLevelActors.Gather(ConnectionManager, OutGatheredList);
	}
}