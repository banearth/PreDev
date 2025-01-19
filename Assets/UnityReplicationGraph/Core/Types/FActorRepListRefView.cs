using System;
using System.Collections.Generic;
using System.Numerics;
using Unity.VisualScripting;

/// <summary>
/// Actor复制列表的引用视图,用于管理需要复制的Actor列表
/// </summary>
public class FActorRepListRefView
{
    // 内部Actor列表 - 与UE保持一致的命名
    public List<FActorRepListType> RepList;

    public FActorRepListRefView()
    {
        RepList = new List<FActorRepListType>();
    }

    /// <summary>
    /// 重置列表但不释放内部内存。如果指定的最大大小大于当前最大值,则会调整大小
    /// </summary>
    public void Reset(int ExpectedMaxSize = 0)
    {
        RepList.Clear();
        if (ExpectedMaxSize > RepList.Capacity)
        {
            RepList.Capacity = ExpectedMaxSize;
        }
    }

    /// <summary>
    /// 预分配列表容量
    /// </summary>
    public void Reserve(int Size)
    {
        if (Size > RepList.Capacity)
        {
            RepList.Capacity = Size;
        }
    }

	/// <summary>
	/// 在添加Actor前检查其是否有效,如果有效则添加
	/// </summary>
	public bool ConditionalAdd(FActorRepListType NewElement)
	{
		if (ReplicationGraphUtils.IsActorValidForReplicationGather(NewElement))
		{
			Add(NewElement);
			return true;
		}
		return false;
	}

	public void Add(FActorRepListType NewElement)
    {
        if (NewElement == null)
            return;

        RepList.Add(NewElement);
    }

    public void Remove(FActorRepListType NewElement) 
    { 
        RepList.Remove(NewElement); 
    }

    /// <summary>
    /// 快速移除一个元素(通过交换删除法)
    /// 警告:这会改变列表中元素的顺序,因为会用最后一个元素替换被删除的元素
    /// </summary>
    public bool RemoveFast(FActorRepListType ElementToRemove)
    {
        if (ElementToRemove == null)
            return false;

        int index = RepList.IndexOf(ElementToRemove);
        if (index != -1)
        {
            int lastIndex = RepList.Count - 1;
            if (index != lastIndex)
            {
                RepList[index] = RepList[lastIndex];
            }
            RepList.RemoveAt(lastIndex);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 有序移除一个Actor(保持其他元素顺序)
    /// </summary>
    public bool RemoveSlow(FActorRepListType ElementToRemove)
    {
        if (ElementToRemove == null)
            return false;

        return RepList.Remove(ElementToRemove);
    }

	public void RemoveAtSwap(int idx)
	{
		int lastIndex = RepList.Count - 1;
		if (idx != lastIndex)
		{
			// 用最后一个元素替换要删除的元素
			RepList[idx] = RepList[lastIndex];
		}	
		// 移除最后一个元素
		RepList.RemoveAt(lastIndex);
	}

    public void RemoveAt(int idx)
    {
        RepList.RemoveAt(idx);
    }

    public void CopyContentsFrom(FActorRepListRefView source)
    {
        // 直接复制引用列表
        RepList = new List<FActorRepListType>(source.RepList);
    }

    public void AppendContentsFrom(FActorRepListRefView source)
    {
        // 将源列表的内容添加到当前列表末尾
        RepList.AddRange(source.RepList);
    }

	public bool VerifyContents_Slow()
    {
        foreach (var actor in RepList)
        {
            // 检查Actor是否有效且可以复制
            if (!ReplicationGraphUtils.IsActorValidForReplicationGather(actor))
            {
                ReplicationGraphDebugger.LogWarning($"Actor {actor} not valid for replication");
                return false;
            }

            // 检查Actor引用是否有效
            if (actor == null)
            {
                ReplicationGraphDebugger.LogWarning($"Actor {actor} failed reference check");
                return false;
            }
        }

        return true;
    }

	public void AppendToTArray(List<FActorRepListType> OutArray)
	{
		OutArray.AddRange(RepList);
	}

	public string BuildDebugString()
    {
        if (RepList.Count > 0)
        {
            var str = ReplicationGraphDebugger.GetActorRepListTypeDebugString(RepList[0]);
            for (int i = 1; i < RepList.Count; ++i)
            {
                str += ", " + ReplicationGraphDebugger.GetActorRepListTypeDebugString(RepList[i]);
            }
            return str;
        }
        return string.Empty;
    }

	public FActorRepListType this[int idx]
    {
        get { return RepList[idx]; }
    }

	public IEnumerator<FActorRepListType> GetEnumerator()
	{
		return RepList.GetEnumerator();
	}

	public bool IsEmpty()
	{
		return RepList.Count <= 0;
	}

	public int Num()
    {
        return RepList.Count;
    }

    public void TearDown()
    {
        RepList.Clear();
    }

	public int IndexOf(FActorRepListType Actor)
	{
		return RepList.IndexOf(Actor);
	}

	public bool Contains(FActorRepListType Actor)
    {
        return RepList.Contains(Actor);
    }

}