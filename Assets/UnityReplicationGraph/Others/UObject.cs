/// <summary>
/// 对应UE的UObject基类
/// </summary>
public class UObject
{
    /// <summary>
    /// 对应UE中的Outer指针,指向拥有该对象的UObject
    /// </summary>
    protected UObject Outer;

    /// <summary>
    /// 获取对象的名称
    /// </summary>
    public virtual string Name { get; protected set; }

    /// <summary>
    /// 获取对象的外部对象
    /// </summary>
    public UObject GetOuter()
    {
        return Outer;
    }

    /// <summary>
    /// 设置对象的外部对象
    /// </summary>
    internal void SetOuter(UObject NewOuter)
    {
        Outer = NewOuter;
    }

    /// <summary>
    /// 获取当前世界实例
    /// 递归向上查找直到找到World对象或到达顶层
    /// </summary>
    public virtual UWorld GetWorld()
    {
        // 如果有外部对象，递归向上查找
        if (Outer != null)
        {
            return Outer.GetWorld();
        }
        return null;
    }

    /// <summary>
    /// 获取指定类型的外部对象
    /// 递归向上查找直到找到指定类型的对象或到达顶层
    /// </summary>
    public T GetTypedOuter<T>() where T : UObject
    {
        UObject currentOuter = Outer;
        while (currentOuter != null)
        {
            if (currentOuter is T typedOuter)
            {
                return typedOuter;
            }
            currentOuter = currentOuter.GetOuter();
        }
        return null;
    }

}