using System;

public class FGlobalActorReplicationEvents
{
    // 休眠状态改变事件
    public event Action<FActorRepListType, FGlobalActorReplicationInfo, ENetDormancy, ENetDormancy> DormancyChange;
    
    // 休眠刷新事件(这个事件在广播后会被清除)
    public event Action<FActorRepListType, FGlobalActorReplicationInfo> DormancyFlush;
    
    // 强制网络更新事件(可选)
    #if REPGRAPH_ENABLE_FORCENETUPDATE_DELEGATE
    public event Action<FActorRepListType, FGlobalActorReplicationInfo> ForceNetUpdate;
    #endif

    // 清除所有事件
    public void Clear()
    {
        DormancyChange = null;
        DormancyFlush = null;
        #if REPGRAPH_ENABLE_FORCENETUPDATE_DELEGATE
        ForceNetUpdate = null;
        #endif
    }
}