using System;
using System.Collections.Generic;

public class FGlobalActorReplicationInfoMap
{
    private Dictionary<FActorRepListType, FGlobalActorReplicationInfo> ActorMap = new Dictionary<FActorRepListType, FGlobalActorReplicationInfo>();
    private Dictionary<string, FClassReplicationInfo> ClassMap = new Dictionary<string, FClassReplicationInfo>();
    
    public IEnumerable<FActorRepListType> ViewAllActors()
    {
        return ActorMap.Keys;
    }

    /// <summary>
    /// 获取Actor的全局复制信息,如果不存在则创建
    /// </summary>
    public FGlobalActorReplicationInfo Get(FActorRepListType actor)
    {
        if (ActorMap.TryGetValue(actor, out var info))
        {
            return info;
        }
        // 检查Actor是否有效
        ReplicationGraphDebugger.EnsureMsg(IsActorValidForReplication(actor), 
            "An invalid actor pointer is passed to FGlobalActorReplicationInfo::Get(), storing this data will generate stale data in the map.");
        var classInfo = GetClassInfo(actor.ReplicationType);
        ReplicationGraphDebugger.EnsureMsg(classInfo != null, $"ReplicationType {actor.ReplicationType} was not registered with FGlobalActorReplicationInfoMap. Call SetClassInfo() first.");
        // 为这个actor创建新数据
        var newInfo = new FGlobalActorReplicationInfo(classInfo);
        ActorMap[actor] = newInfo;
        return newInfo;
    }

    /// <summary>
    /// 检查Actor是否有效
    /// </summary>
    private bool IsActorValidForReplication(FActorRepListType actor)
    {
        if (actor == null)
        {
            ReplicationGraphDebugger.LogError("Invalid actor: null pointer");
            return false;
        }
        if (!ReplicationGraphUtils.IsActorValidForReplication(actor))
        {
            ReplicationGraphDebugger.LogError($"Invalid actor for replication: {actor}");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 查找Actor的全局复制信息
    /// </summary>
    public FGlobalActorReplicationInfo Find(FActorRepListType actor)
    {
        ActorMap.TryGetValue(actor, out var info);
        return info;
    }

    /// <summary>
    /// 移除Actor的全局复制信息
    /// </summary>
    public void Remove(FActorRepListType actor)
    {
        ActorMap.Remove(actor);
    }

    /// <summary>
    /// 设置类的复制信息
    /// </summary>
    public void SetClassInfo(string replicationType, FClassReplicationInfo classInfo)
    {
        if(ClassMap.ContainsKey(replicationType))
        {
            ReplicationGraphDebugger.LogError($"ReplicationType {replicationType} is already registered with FGlobalActorReplicationInfoMap.");
            return;
        }
        ClassMap.Add(replicationType, classInfo);
    }

    /// <summary>
    /// 获取类的复制信息,如果未注册则报错
    /// </summary>
    public FClassReplicationInfo GetClassInfo(string replicationType)
    {
        if (ClassMap.TryGetValue(replicationType, out var info))
        {
            return info;
        }

        // 如果类未注册,抛出错误
        ReplicationGraphDebugger.EnsureMsg(false, 
            $"ReplicationType {replicationType} was not registered with FGlobalActorReplicationInfoMap. Call SetClassInfo() first.");
        
        return null; // 不会执行到这里,因为EnsureMsg会抛出异常
    }

    /// <summary>
    /// 清空所有映射
    /// </summary>
    public void Reset()
    {
        ActorMap.Clear();
        ClassMap.Clear();
    }
}
