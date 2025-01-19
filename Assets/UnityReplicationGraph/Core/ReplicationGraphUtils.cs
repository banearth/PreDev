using System;
using System.Numerics;
using System.Collections.Generic;

public static class ReplicationGraphUtils
{

    public static bool IsActorValidForReplication(FActorRepListType actor)
    {
        // 后面漏了 && IsValidChecked(Actor) && !Actor->IsUnreachable(); 
        // 我评估暂时应该不用管
        return (actor != null) && !actor.IsActorBeingDestroyed();
    }

	/// <summary>
	/// 测试一个Actor是否有效用于复制收集。
	/// 意味着它可以从复制图中收集并考虑进行复制。
	/// </summary>
	public static bool IsActorValidForReplicationGather(FActorRepListType actor)
    {
        // 检查actor是否为null
        if (actor == null)
            return false;

        // 检查actor是否有效用于复制
        if (!IsActorValidForReplication(actor))
            return false;

        // 检查actor是否启用了复制
        if (!actor.IsReplicated)
            return false;

        // 检查actor是否已经TearOff
        if (actor.GetTearOff())
            return false;

        // 检查初始休眠的网络启动Actor
        if (actor.NetDormancy == ENetDormancy.DORM_Initial && actor.IsNetStartupActor)
            return false;

        return true;
    }


}