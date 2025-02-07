using System;
using UnityEngine;

public class FGlobalActorReplicationInfo
{
    /// <summary>
    /// 对应的Actor
    /// </summary>
    public FActorRepListType Actor { get; set; }

    /// <summary>
    /// Actor的世界位置缓存
    /// </summary>
    public Vector3 WorldLocation { get; set; }

    /// <summary>
    /// 上一次PreReplication调用的帧号
    /// </summary>
    public uint LastPreReplicationFrame { get; set; }

    /// <summary>
    /// 最后一次ForceNetUpdate的帧号
    /// </summary>
    public uint ForceNetUpdateFrame { get; set; }

    /// <summary>
    /// 是否想要进入休眠状态
    /// </summary>
    public bool bWantsToBeDormant { get; set; }

    /// <summary>
    /// 是否在复制前交换角色
    /// </summary>
    public bool bSwapRolesOnReplicate { get; set; }

    /// <summary>
    /// 位置是否被限制
    /// </summary>
    public bool bWasWorldLocClamped { get; set; }

    /// <summary>
    /// 类复制设置
    /// </summary>
    public FClassReplicationInfo Settings { get; private set; }

	/// <summary>
	/// 复制事件系统
	/// </summary>
	public FGlobalActorReplicationEvents Events { get; private set; }

	/// <summary>
	/// 当这个Actor进行复制时，如果客户端已经加载了该Actor所在的关卡，并且网络更新频率不过快，我们会紧接着复制这些Actor。
	/// </summary>
	public FLevelBasedActorList DependentActorList = new FLevelBasedActorList();

	public FFastSharedReplicationInfo FastSharedReplicationInfo = null;

	public FGlobalActorReplicationInfo(FClassReplicationInfo classInfo)
    {
        LastPreReplicationFrame = 0;
        WorldLocation = Vector3.zero;
        bWantsToBeDormant = false;
        bSwapRolesOnReplicate = false;
        bWasWorldLocClamped = false;
        Settings = classInfo;
        Events = new FGlobalActorReplicationEvents();
    }

	public void GatherDependentActorLists(UNetReplicationGraphConnection ConnectionManager, FGatheredReplicationActorLists OutGatheredList)
	{
		DependentActorList.Gather(ConnectionManager, OutGatheredList);
	}
}