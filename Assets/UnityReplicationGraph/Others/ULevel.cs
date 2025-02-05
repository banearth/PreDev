using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 对应UE的ULevel
/// </summary>
public class ULevel : UObject
{
    private string StreamingLevelName;
    private List<FActorRepListType> Actors = new List<FActorRepListType>();
    
    // 按类型组织的Actor映射
    private Dictionary<System.Type, HashSet<FActorRepListType>> TypeToActorsMap = 
        new Dictionary<System.Type, HashSet<FActorRepListType>>();

    public ULevel(string name)
    {
        Name = name;
        StreamingLevelName = name;
    }

    public void AddActor(FActorRepListType Actor)
    {
        if (!Actors.Contains(Actor))
        {
            Actors.Add(Actor);
            Actor.SetOuter(this);

            // 添加到类型映射
            var type = Actor.GetType();
            if (!TypeToActorsMap.TryGetValue(type, out var actorSet))
            {
                actorSet = new HashSet<FActorRepListType>();
                TypeToActorsMap[type] = actorSet;
            }
            actorSet.Add(Actor);
        }
    }

    public bool RemoveActor(FActorRepListType Actor)
    {
        if (Actors.Remove(Actor))
        {
            // 从类型映射中移除
            var type = Actor.GetType();
            if (TypeToActorsMap.TryGetValue(type, out var actorSet))
            {
                actorSet.Remove(Actor);
                if (actorSet.Count == 0)
                {
                    TypeToActorsMap.Remove(type);
                }
            }
            return true;
        }
        return false;
    }

    public bool IsPersistentLevel()
    {
        var world = GetOuter() as UWorld;
        return world != null && world.PersistentLevel == this;
    }

    public string GetStreamingLevelName()
    {
        return IsPersistentLevel() ? string.Empty : StreamingLevelName;
    }

    // 获取指定类型的所有Actor
    public IEnumerable<FActorRepListType> GetActorsOfType(System.Type type)
    {
        return TypeToActorsMap.TryGetValue(type, out var actors) ? actors : 
            Enumerable.Empty<FActorRepListType>();
    }

    // 获取所有Actor
    public IReadOnlyList<FActorRepListType> GetAllActors()
    {
        return Actors;
    }
}