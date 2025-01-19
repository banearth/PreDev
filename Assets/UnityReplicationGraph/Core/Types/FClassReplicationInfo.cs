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

	public void SetCullDistanceSquared(float inCullDistanceSquared)
	{
		CullDistanceSquared = inCullDistanceSquared;
		CullDistance = Mathf.Sqrt(CullDistanceSquared);
	}

	public float GetCullDistance() => CullDistance;
	public float GetCullDistanceSquared() => CullDistanceSquared;
}