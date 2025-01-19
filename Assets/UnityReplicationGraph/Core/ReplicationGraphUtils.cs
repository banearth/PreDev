using System;
using System.Numerics;
using System.Collections.Generic;

public static class ReplicationGraphUtils
{

    public static bool IsActorValidForReplication(FActorRepListType actor)
    {
        // 后面漏了 && IsValidChecked(Actor) && !Actor->IsUnreachable(); 
        // 我评估暂时应该不用管
        return (actor != null) && actor.IsActorBeingDestroyed();
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

	/// <summary>
	/// 查找键对应的值，如果不存在则添加由工厂方法创建的新值
	/// 模拟UE的TMap::FindOrAdd功能
	/// </summary>
	public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key,
		Func<TValue> valueFactory) where TKey : notnull
	{
        if (!dictionary.TryGetValue(key, out TValue value))
        {
            value = valueFactory();
            dictionary.Add(key, value);
        }
        return value;
    }

	/// <summary>
	/// 返回列表中未使用的空间数量
	/// 相当于UE的GetSlack()
	/// </summary>
	public static int GetSlack<T>(this List<T> list)
	{
		return list.Capacity - list.Count;
	}

	/// <summary>
	/// 返回列表当前分配的内存大小(以字节为单位)
	/// 相当于UE的GetAllocatedSize()
	/// </summary>
	public static int GetAllocatedSize<T>(this List<T> list)
	{
		return list.Capacity * System.Runtime.InteropServices.Marshal.SizeOf<T>();
	}

    /// <summary>
    /// 通过交换最后一个元素的方式移除指定索引的元素
    /// 这种方式比普通的Remove更高效，但不保持元素顺序
    /// </summary>
    public static void RemoveAtSwap<T>(this List<T> list, int index)
    {
        // 范围检查
        if (index < 0 || index >= list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        // 实现移除并交换
        int lastIndex = list.Count - 1;
        if (index != lastIndex)
        {
            list[index] = list[lastIndex];
        }
        list.RemoveAt(lastIndex);
    }

}