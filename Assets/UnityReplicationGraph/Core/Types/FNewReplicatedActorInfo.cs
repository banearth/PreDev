using System;
using UnityEngine;

/// <summary>
/// 存储Actor的复制相关信息
/// </summary>
public class FNewReplicatedActorInfo
{
    /// <summary>
    /// Actor引用
    /// </summary>
    public FActorRepListType Actor { get; private set; }

    /// <summary>
    /// Actor所在流关卡的名称
    /// </summary>
    public string StreamingLevelName { get; private set; }

    /// <summary>
    /// Actor的类型
    /// </summary>
    public Type ActorClass { get; private set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public FNewReplicatedActorInfo(FActorRepListType InActor)
    {
        Actor = InActor;
        if (Actor != null)
        {
            StreamingLevelName = GetStreamingLevelNameOfActor(Actor);
        }
        else
        {
            StreamingLevelName = string.Empty;
        }
    }

	public FNewReplicatedActorInfo(FActorRepListType InActor,string OverrideLevelName)
	{
        Actor = InActor;
		StreamingLevelName = OverrideLevelName;
	}

	private string GetStreamingLevelNameOfActor(FActorRepListType Actor)
    {
        // 通过Outer获取Actor所在的Level
        var level = Actor.GetOuter() as ULevel;
        if (level == null)
            return string.Empty;

        // 如果是PersistentLevel返回空
        if (level.IsPersistentLevel())
            return string.Empty;

        // 返回Level的StreamingLevelName
        return level.GetStreamingLevelName();
    }

    /// <summary>
    /// 检查Actor是否有效
    /// </summary>
    public bool IsValid()
    {
        return Actor != null;
    }

    /// <summary>
    /// 获取调试信息
    /// </summary>
    public string BuildDebugString()
    {
        if (!IsValid())
        {
            return "Invalid";
        }
        return $"Actor: {Actor}, ReplicationType: {Actor.ReplicationType}, Level: {StreamingLevelName}";
    }
}