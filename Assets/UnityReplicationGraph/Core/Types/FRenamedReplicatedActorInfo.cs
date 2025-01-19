public class FRenamedReplicatedActorInfo
{
    // 存储Actor的新关卡名称信息
    public FNewReplicatedActorInfo NewActorInfo { get; private set; }

    // 存储Actor的旧关卡名称信息
    public FNewReplicatedActorInfo OldActorInfo { get; private set; }

    public FRenamedReplicatedActorInfo(FActorRepListType actor, string previousStreamingLevelName)
    {
        NewActorInfo = new FNewReplicatedActorInfo(actor);
        OldActorInfo = new FNewReplicatedActorInfo(actor, previousStreamingLevelName);
    }
}