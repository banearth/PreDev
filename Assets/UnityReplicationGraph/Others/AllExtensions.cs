using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public static class AllExtensions
{
    public static Vector2 WithX(this Vector2 vector, float x)
    {
        return new Vector2(x, vector.y);
    }

    public static Vector2 WithY(this Vector2 vector, float y)
    {
        return new Vector2(vector.x, y);
    }

    public static Vector3 WithX(this Vector3 vector, float x)
    {
        return new Vector3(x, vector.y, vector.z);
    }

    public static Vector3 WithY(this Vector3 vector, float y)
    {
        return new Vector3(vector.x, y, vector.z);
    }

    public static Vector3 WithZ(this Vector3 vector, float z)
    {
        return new Vector3(vector.x, vector.y, z);
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
