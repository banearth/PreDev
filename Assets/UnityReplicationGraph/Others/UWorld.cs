using System.Collections.Generic;

/// <summary>
/// 对应UE的UWorld
/// </summary>
public class UWorld : UObject
{
    // 永久关卡(主关卡)
    public ULevel PersistentLevel { get; private set; }

    // 所有已加载的关卡列表
    private List<ULevel> Levels = new List<ULevel>();

    // 网络相关 - 只保留NetDriver
    private UNetworkDriver _netDriver;

    public UWorld(string name)
    {
        Name = name;
        
        // 创建永久关卡
        PersistentLevel = new ULevel("PersistentLevel");
        PersistentLevel.SetOuter(this);
        Levels.Add(PersistentLevel);
    }

    /// <summary>
    /// 设置NetDriver
    /// </summary>
    public void SetNetDriver(UNetworkDriver driver)
    {
        _netDriver = driver;
    }

    /// <summary>
    /// 获取NetDriver
    /// </summary>
    public UNetworkDriver GetNetDriver()
    {
        return _netDriver;
    }

    /// <summary>
    /// 添加关卡
    /// </summary>
    public void AddLevel(ULevel Level)
    {
        if (!Levels.Contains(Level))
        {
            Levels.Add(Level);
            Level.SetOuter(this);
        }
    }

    /// <summary>
    /// 移除关卡
    /// </summary>
    public bool RemoveLevel(ULevel Level)
    {
        if (Level == PersistentLevel)
            return false;
        return Levels.Remove(Level);
    }

    /// <summary>
    /// 获取所有关卡
    /// </summary>
    public IReadOnlyList<ULevel> GetAllLevels()
    {
        return Levels;
    }

    /// <summary>
    /// 根据关卡名称查找关卡
    /// </summary>
    public ULevel GetLevelByName(string streamingLevelName)
    {
        return Levels.Find(x => x.GetStreamingLevelName() == streamingLevelName);
    }

    /// <summary>
    /// 获取所有关卡中指定类型的Actor
    /// </summary>
    public IEnumerable<FActorRepListType> GetAllActorsOfType(System.Type type)
    {
        var result = new List<FActorRepListType>();
        
        // 从所有关卡中收集指定类型的Actor
        foreach (var level in Levels)
        {
            result.AddRange(level.GetActorsOfType(type));
        }
        
        return result;
    }

    /// <summary>
    /// World对象重写GetWorld方法，直接返回自身
    /// </summary>
    public override UWorld GetWorld()
    {
        return this;
    }
} 