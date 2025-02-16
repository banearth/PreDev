using System;
using UnityEngine;

/// <summary>
/// Actor类的复制设置信息
/// </summary>
public class FClassReplicationInfo
{
	public float DistancePriorityScale = 1.0f;
	public float StarvationPriorityScale = 1.0f;
	public float AccumulatedNetPriorityBias = 0.0f;

	public ushort ReplicationPeriodFrame = 1;
	public ushort FastPath_ReplicationPeriodFrame = 1;
	public ushort ActorChannelFrameTimeout = 4;

	public float CullDistance = 0.0f;
	public float CullDistanceSquared = 0.0f;

	public Func<FActorRepListType, bool> FastSharedReplicationFunc { get; private set; }

	public FClassReplicationInfo SetCullDistance(float cullDistance)
	{
		CullDistanceSquared = cullDistance * cullDistance;
		CullDistance = cullDistance;
		return this;
	}

	public FClassReplicationInfo SetDistancePriorityScale(float scale)
	{
		DistancePriorityScale = scale;
		return this;
	}

	public FClassReplicationInfo SetStarvationPriorityScale(float scale)
	{
		StarvationPriorityScale = scale;
		return this;
	}

	public FClassReplicationInfo SetReplicationPeriodFrame(ushort frame)
	{
		ReplicationPeriodFrame = frame;
		return this;
	}

	public FClassReplicationInfo SetFastPathReplicationPeriodFrame(ushort frame)
	{
		FastPath_ReplicationPeriodFrame = frame;
		return this;
	}

	public FClassReplicationInfo SetActorChannelFrameTimeout(ushort timeout)
	{
		ActorChannelFrameTimeout = timeout;
		return this;
	}

	public float GetCullDistance() => CullDistance;
	public float GetCullDistanceSquared() => CullDistanceSquared;
}