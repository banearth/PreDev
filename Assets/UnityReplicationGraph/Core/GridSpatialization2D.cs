using UnityEngine;
using System.Collections.Generic;

public class GridSpatialization2D : ReplicationGraphNode
{
    public float CellSize { get; set; }
    public Vector2 SpatialBias { get; set; }

    private Dictionary<GridCell, List<ReplicatedActorInfo>> Grid = new Dictionary<GridCell, List<ReplicatedActorInfo>>();
    private Dictionary<ReplicatedActorInfo, HashSet<GridCell>> ActorCells = new Dictionary<ReplicatedActorInfo, HashSet<GridCell>>();

    public override void NotifyAddNetworkActor(ReplicatedActorInfo actorInfo)
    {
        // 获取Actor所在的网格单元
        var cells = GetRelevantCells(actorInfo);
        
        // 记录Actor与网格的关系
        ActorCells[actorInfo] = cells;

        // 将Actor添加到相关的所有网格中
        foreach (var cell in cells)
        {
            if (!Grid.TryGetValue(cell, out var actors))
            {
                actors = new List<ReplicatedActorInfo>();
                Grid[cell] = actors;
            }
            actors.Add(actorInfo);
        }
    }

    public override void NotifyRemoveNetworkActor(ReplicatedActorInfo actorInfo)
    {
        if (ActorCells.TryGetValue(actorInfo, out var cells))
        {
            foreach (var cell in cells)
            {
                if (Grid.TryGetValue(cell, out var actors))
                {
                    actors.Remove(actorInfo);
                    if (actors.Count == 0)
                    {
                        Grid.Remove(cell);
                    }
                }
            }
            ActorCells.Remove(actorInfo);
        }
    }

    public override void GatherActorListsForConnection(ConnectionGatherActorListParameters parameters)
    {
        // 遍历所有viewer
        foreach (var viewer in parameters.Viewers)
        {
            var viewerPos = viewer.ViewLocation;
            var relevantCells = GetRelevantCells(viewerPos, 50f); // 使用固定视距或从配置获取

            foreach (var cell in relevantCells)
            {
                if (Grid.TryGetValue(cell, out var actors))
                {
                    foreach (var actor in actors)
                    {
                        if (IsRelevantForViewer(actor, viewer))
                        {
                            parameters.OutGatheredReplicationLists.Add(actor);
                        }
                    }
                }
            }
        }
    }

    private HashSet<GridCell> GetRelevantCells(ReplicatedActorInfo actorInfo)
    {
        var cells = new HashSet<GridCell>();
        var pos = actorInfo.Location;
        var radius = actorInfo.CullDistance;

        // 计算Actor影响范围内的所有网格
        var minCell = GetCell(new Vector3(pos.x - radius, 0, pos.z - radius));
        var maxCell = GetCell(new Vector3(pos.x + radius, 0, pos.z + radius));

        for (int x = minCell.X; x <= maxCell.X; x++)
        {
            for (int y = minCell.Y; y <= maxCell.Y; y++)
            {
                cells.Add(new GridCell(x, y));
            }
        }

        return cells;
    }

    private HashSet<GridCell> GetRelevantCells(Vector3 position, float radius)
    {
        var cells = new HashSet<GridCell>();
        var minCell = GetCell(new Vector3(position.x - radius, 0, position.z - radius));
        var maxCell = GetCell(new Vector3(position.x + radius, 0, position.z + radius));

        for (int x = minCell.X; x <= maxCell.X; x++)
        {
            for (int y = minCell.Y; y <= maxCell.Y; y++)
            {
                cells.Add(new GridCell(x, y));
            }
        }

        return cells;
    }

    private GridCell GetCell(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition.x - SpatialBias.x) / CellSize);
        int y = Mathf.FloorToInt((worldPosition.z - SpatialBias.y) / CellSize);
        return new GridCell(x, y);
    }

    private bool IsRelevantForViewer(ReplicatedActorInfo actor, NetViewer viewer)
    {
        var viewerPos = viewer.ViewLocation;
        float distanceSq = (actor.Location - viewerPos).sqrMagnitude;
        return distanceSq <= (actor.CullDistance * actor.CullDistance);
    }
}

public struct GridCell
{
    public int X { get; }
    public int Y { get; }

    public GridCell(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override bool Equals(object obj)
    {
        if (obj is GridCell other)
        {
            return X == other.X && Y == other.Y;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return X.GetHashCode() ^ Y.GetHashCode();
    }
} 