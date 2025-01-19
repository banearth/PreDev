using System.Collections.Generic;

public class FPerConnectionActorInfoMap
{
    public Dictionary<FActorRepListType, FConnectionReplicationActorInfo> ActorMap = 
        new Dictionary<FActorRepListType, FConnectionReplicationActorInfo>();
        
    private Dictionary<ActorChannel, FConnectionReplicationActorInfo> ChannelMap = 
        new Dictionary<ActorChannel, FConnectionReplicationActorInfo>();
        
    private FGlobalActorReplicationInfoMap GlobalMap;

    /// <summary>
    /// 查找或添加Actor的连接信息
    /// </summary>
    public FConnectionReplicationActorInfo FindOrAdd(FActorRepListType Actor)
    {
        if (ActorMap.TryGetValue(Actor, out var info))
        {
            return info;
        }

        var newInfo = new FConnectionReplicationActorInfo(GlobalMap.Get(Actor));
        ActorMap[Actor] = newInfo;
        return newInfo;
    }

    /// <summary>
    /// 查找Actor的连接信息
    /// </summary>
    public FConnectionReplicationActorInfo Find(FActorRepListType Actor)
    {
        ActorMap.TryGetValue(Actor, out var info);
        return info;
    }

    /// <summary>
    /// 通过Channel查找连接信息
    /// </summary>
    public FConnectionReplicationActorInfo FindByChannel(ActorChannel Channel)
    {
        ChannelMap.TryGetValue(Channel, out var info);
        return info;
    }

    /// <summary>
    /// 添加Channel
    /// </summary>
    public void AddChannel(FActorRepListType Actor, ActorChannel Channel)
    {
        if (ActorMap.TryGetValue(Actor, out var info))
        {
            ChannelMap[Channel] = info;
        }
    }

    /// <summary>
    /// 移除Channel
    /// </summary>
    public void RemoveChannel(ActorChannel Channel)
    {
        ChannelMap.Remove(Channel);
    }

    /// <summary>
    /// 移除Actor
    /// </summary>
    public void RemoveActor(FActorRepListType Actor)
    {
        ActorMap.Remove(Actor);
    }

    /// <summary>
    /// 获取Actor映射的迭代器
    /// </summary>
    public Dictionary<FActorRepListType, FConnectionReplicationActorInfo>.Enumerator CreateIterator()
    {
        return ActorMap.GetEnumerator();
    }

    /// <summary>
    /// 设置全局映射
    /// </summary>
    public void SetGlobalMap(FGlobalActorReplicationInfoMap inGlobalMap)
    {
        GlobalMap = inGlobalMap;
    }

    /// <summary>
    /// 获取Channel映射的迭代器
    /// </summary>
    public Dictionary<ActorChannel, FConnectionReplicationActorInfo>.Enumerator CreateChannelIterator()
    {
        return ChannelMap.GetEnumerator();
    }

    /// <summary>
    /// 重置Actor映射
    /// </summary>
    public void ResetActorMap()
    {
        ActorMap.Clear();
        ChannelMap.Clear();
    }

    /// <summary>
    /// 获取Actor数量
    /// </summary>
    public int Num()
    {
        return ActorMap.Count;
    }
}