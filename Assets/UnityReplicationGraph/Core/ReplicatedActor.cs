using UnityEngine;

public abstract class ReplicatedActor
{
    public Vector3 Position { get; set; }
    public NetworkConnection Owner { get; set; }
    
    // 复制相关的基础属性
    public float NetUpdateFrequency { get; set; } = 30f;  // 默认30Hz
    public bool bAlwaysRelevant { get; set; } = false;
    public bool bOnlyRelevantToOwner { get; set; } = false;
    public float NetCullDistanceSquared { get; set; } = 10000f;  // 默认100米
    
    public ReplicatedActor()
    {
        Position = Vector3.zero;
    }

    // 子类可以重写这些方法来自定义复制行为
    public virtual bool IsNetRelevantFor(NetworkConnection connection)
    {
        if (bAlwaysRelevant) return true;
        if (bOnlyRelevantToOwner) return connection == Owner;
        return true;
    }
} 