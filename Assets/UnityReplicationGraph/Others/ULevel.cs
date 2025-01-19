using System.Collections.Generic;

/// <summary>
/// 对应UE的ULevel
/// </summary>
public class ULevel : UObject
{
    private string StreamingLevelName;
    private List<FActorRepListType> Actors = new List<FActorRepListType>();

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
        }
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
}