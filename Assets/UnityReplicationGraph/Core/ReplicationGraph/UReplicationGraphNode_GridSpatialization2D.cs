using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class UReplicationGraphNode_GridSpatialization2D : UReplicationGraphNode
{
    public float CellSize { get; set; }
    public Vector2 SpatialBias { get; set; }
    public float ConnectionMaxZ { get; set; } = 100000f; // 类似UE的UE::Net::Private::RepGraphWorldMax

	private Bounds GridBounds;

	// 使用二维数组替代Dictionary
	private List<List<UReplicationGraphNode_GridCell>> Grid = new List<List<UReplicationGraphNode_GridCell>>();

    // 动态Actor缓存
    private Dictionary<FActorRepListType, FCachedDynamicActorInfo> DynamicSpatializedActors = 
        new Dictionary<FActorRepListType, FCachedDynamicActorInfo>();

    // 静态Actor缓存
    private Dictionary<FActorRepListType, FCachedStaticActorInfo> StaticSpatializedActors = 
        new Dictionary<FActorRepListType, FCachedStaticActorInfo>();

    private bool bDestroyDormantDynamicActors = true;
	private int DestroyDormantDynamicActorsCellTTL = 1;
	private int ReplicatedDormantDestructionInfosPerFrame = int.MaxValue;

	// 这是一个用于收集用于休眠清理的 Actor 的重用 FActorRepListRefView
	private FActorRepListRefView GatheredActors = new FActorRepListRefView();

	// This is a reused TArray for gathering actor nodes. Just to prevent using a stack based TArray everywhere or static/reset patten.
	private List<UReplicationGraphNode_GridCell> GatheredNodes;

	public UReplicationGraphNode_GridSpatialization2D()
    {
        bRequiresPrepareForReplicationCall = true;
    }

    private List<UReplicationGraphNode_GridCell> GetGridX(int x)
    {
        while (Grid.Count <= x)
        {
            Grid.Add(new List<UReplicationGraphNode_GridCell>());
        }
        return Grid[x];
    }

    private UReplicationGraphNode_GridCell GetCell(List<UReplicationGraphNode_GridCell> GridX,int Y)
    {
		while (GridX.Count <= Y)
		{
			GridX.Add(new UReplicationGraphNode_GridCell());
		}
        return GridX[Y];
	}

	private UReplicationGraphNode_GridCell GetCell(int x, int y)
    {
        var gridX = GetGridX(x);
        var grid = GetCell(gridX, y);
        return grid;
    }

    public override void NotifyAddNetworkActor(FNewReplicatedActorInfo actorInfo)
    {
        ReplicationGraphDebugger.EnsureAlwaysMsg(false, "UReplicationGraphNode_GridSpatialization2D::NotifyAddNetworkActor should not be called directly");
    }

    public override bool NotifyRemoveNetworkActor(FNewReplicatedActorInfo actorInfo, bool bWarnIfNotFound = true)
    {
		ReplicationGraphDebugger.EnsureAlwaysMsg(false, "UReplicationGraphNode_GridSpatialization2D::NotifyRemoveNetworkActor should not be called directly");
		return false;
    }

    private FActorCellInfo GetCellInfoForActor(FActorRepListType actor, Vector3 location, float cullDistance)
    {
        var minCell = GetCellCoord(new Vector3(location.x - cullDistance, 0, location.z - cullDistance));
        var maxCell = GetCellCoord(new Vector3(location.x + cullDistance, 0, location.z + cullDistance));

        return new FActorCellInfo
        {
            StartX = minCell.x,
            StartY = minCell.y,
            EndX = maxCell.x,
            EndY = maxCell.y
        };
    }

    private Vector2Int GetCellCoord(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition.x - SpatialBias.x) / CellSize);
        int y = Mathf.FloorToInt((worldPosition.z - SpatialBias.y) / CellSize);
        return new Vector2Int(x, y);
    }

    public override void GatherActorListsForConnection(FConnectionGatherActorListParameters Params)
    {
        // 用于追踪已处理的唯一网格单元
        var uniqueCurrentGridCells = new HashSet<Vector2Int>();

        // 遍历所有观察者
        foreach (var viewer in Params.Viewers)
        {
            if (viewer.Connection == null || viewer.ViewLocation.z > ConnectionMaxZ)
            {
                continue;
            }

            // 限制视图位置在有效范围内
            Vector3 clampedViewLoc = GridBounds.ClosestPoint(viewer.ViewLocation);

            // 计算观察者所在的网格单元
            int cellX = Mathf.Max(0, Mathf.FloorToInt((clampedViewLoc.x - SpatialBias.x) / CellSize));
            int cellY = Mathf.Max(0, Mathf.FloorToInt((clampedViewLoc.y - SpatialBias.y) / CellSize));
            var curGridCell = new Vector2Int(cellX, cellY);

            // 如果这个单元格还未处理过
            if (!uniqueCurrentGridCells.Contains(curGridCell))
            {
                var cellNode = GetCell(GetGridX(cellX), cellY);
                if (cellNode != null)
                {
                    cellNode.GatherActorListsForConnection(Params);
                }

                uniqueCurrentGridCells.Add(curGridCell);

                // 更新可见单元格生命周期
                if (bDestroyDormantDynamicActors && DestroyDormantDynamicActorsCellTTL > 0)
                {
                    var visibleCells = Params.ConnectionManager.GetVisibleCellsForNode(this);
                    int existingCellIndex = visibleCells.FindIndex(cell => cell.Location == curGridCell);
                    if (existingCellIndex != -1)
                    {
                        var cellInfo = visibleCells[existingCellIndex];
                        cellInfo.Lifetime = DestroyDormantDynamicActorsCellTTL;
                        visibleCells[existingCellIndex] = cellInfo;
                    }
                    else
                    {
                        visibleCells.Add(new FVisibleCellInfo(curGridCell, DestroyDormantDynamicActorsCellTTL));
                    }
                }
            }
        }

        // 处理休眠动态Actor的销毁
        if (bDestroyDormantDynamicActors && ReplicationGraphDebugger.CVar_RepGraph_DormantDynamicActorsDestruction > 0 
            && Params.bIsSelectedForHeavyComputation)
        {
            var prevDormantActorList = Params.ConnectionManager.GetPrevDormantActorListForNode(this);
            var visibleCells = Params.ConnectionManager.GetVisibleCellsForNode(this);
            if (visibleCells.Count > 0)
            {
                // 缓存休眠节点
                var dormancyNodesCache = new List<UReplicationGraphNode_DormancyNode>(visibleCells.Count);
                // 验证并缓存休眠节点
                for (int i = 0; i < visibleCells.Count; i++)
                {
                    var visibleCell = visibleCells[i];
                    var gridCell = visibleCell.Location;
                    // 验证单元格有效性
                    if (!ValidateFVisibleCellInfo(visibleCell))
                    {
						visibleCells.RemoveAt(i--);
                        continue;
                    }
                    var prevCell = GetCell(GetGridX(gridCell.x), gridCell.y);
                    if (prevCell != null)
                    {
                        dormancyNodesCache.Add(prevCell.GetDormancyNode(false));
                    }
                }
				// 目标：生成集合C，其中：
				// C = A && !B
				// A = 生命周期为0的单元格中的Actor(已死亡单元格)
				// B = 生命周期大于0的单元格中的Actor(可观察单元格)

				// 重置临时列表，用于存储可观察的休眠Actor(集合B)
                GatheredActors.Reset();
				for (int i = 0; i < visibleCells.Count; i++)
				{
					if (visibleCells[i].Lifetime == 0) // 处理已死亡单元格
					{
						var dormancyNode = dormancyNodesCache[i];
						if (dormancyNode != null)
						{
							// 将"不再可观察"单元格中的休眠Actor添加到PrevDormantActorList
							// 但要注意这些Actor可能从其他单元格可见，所以传入GatheredActors作为第二个列表
							dormancyNode.ConditionalGatherDormantDynamicActors(
								prevDormantActorList,  // 目标列表
								Params,                // 参数
								GatheredActors,        // 排除列表(避免重复)
								true                   // 是否是死亡单元格
							);
						}
						// 处理完后移除单元格
						dormancyNodesCache.RemoveAt(i);
						visibleCells.RemoveAt(i--);
					}
					else // 处理仍可观察的单元格
					{
						var dormancyNode = dormancyNodesCache[i];
						if (dormancyNode != null)
						{
							// 确保从PrevDormantActorList中移除这些Actor
							// 因为它们仍然可见，不应该被销毁
							dormancyNode.ConditionalGatherDormantDynamicActors(
								GatheredActors,        // 收集可见的休眠Actor
								Params,                // 参数
								null,                  // 不需要排除列表
								false,                 // 不是死亡单元格
								prevDormantActorList   // 从这个列表中移除仍可见的Actor
							);
						}
					}
				}

			}
			// 清理过期的休眠Actor
			// 如果有需要处理的休眠Actor
            if (prevDormantActorList.Num() > 0)
            {
                // 每帧要处理的Actor数量限制
                int numActorsToRemove = ReplicatedDormantDestructionInfosPerFrame;

                ReplicationGraphDebugger.LogInfo($"GridSpatialization2D: 正在移除 {Mathf.Min(numActorsToRemove, prevDormantActorList.Num())} " +
					$"个Actor (列表大小: {prevDormantActorList.Num()})");

                // 获取全局复制信息映射
                var globalRepMap = GraphGlobals?.GlobalActorReplicationInfoMap;

                // 处理之前休眠但现在不在当前节点休眠列表中的Actor
                for (int i = 0; i < prevDormantActorList.Num() && numActorsToRemove > 0; i++)
                {
                    var actor = prevDormantActorList[i];
                    var globalActorInfo = globalRepMap?.Find(actor);
                    var actorInfo = Params.ConnectionManager.ActorInfoMap.Find(actor);
					if (actorInfo != null)
                    {
                        if (actorInfo.bDormantOnConnection)
                        {
                            // 如果Actor不是始终相关的（有剔除距离），则标记为在客户端上销毁
                            if (actorInfo.GetCullDistanceSquared() > 0.0f)
                            {
                                Params.ConnectionManager.NotifyAddDormantDestructionInfo(actor);
                            }
                            // 重置休眠状态标志
                            actorInfo.bDormantOnConnection = false;
                            actorInfo.bGridSpatilization_AlreadyDormant = false;
							// 使用存储的Actor位置重新添加到特定连接的休眠节点
							var actorLocation = globalActorInfo != null ? 
                                globalActorInfo.WorldLocation : actor.Position;
                            var cellInfo = GetCellInfoForActor(actor, actorLocation, actorInfo.GetCullDistance());
                            GetGridNodesForActor(actor, cellInfo, GatheredNodes);
                            // 通知相关的休眠节点
                            foreach (var node in GatheredNodes)
                            {
                                var dormancyNode = node.GetDormancyNode();
                                if (dormancyNode != null)
                                {
                                    // 只有当客户端之前在单元格内时才通知连接节点
                                    var connectionDormancyNode = dormancyNode.GetExistingConnectionNode(Params);
                                    if (connectionDormancyNode != null)
                                    {
                                        connectionDormancyNode.NotifyActorDormancyFlush(actor);
                                    }
                                }
                            }

                            // 更新计数并从列表中移除
                            numActorsToRemove--;
                            prevDormantActorList.RemoveAt(i--);
                        }
                        else if (actorInfo.Channel == null)
                        {
                            // 通道在进入休眠前关闭，从列表中移除
                            Debug.LogWarning("GridSpatialization2D: Actor的通道指针为空，" +
                                "最好在清除通道时从PrevDormantActorList中移除Actor");
                            actorInfo.bGridSpatilization_AlreadyDormant = false;
                            prevDormantActorList.RemoveAt(i--);
                        }
                    }
                }
            }
        }
    }

	private bool ValidateFVisibleCellInfo(FVisibleCellInfo visibleCell)
	{
		bool passesValidation = true;
		var gridCell = visibleCell.Location;
		// 验证网格边界
		if (!(Grid.Count > gridCell.x && Grid[gridCell.x].Count > gridCell.y))
		{
            ReplicationGraphDebugger.LogWarning($"GridSpatialization2D: Previously visible cell ({gridCell.x}x{gridCell.y}) " +
				"is out of bounds due to grid resize, skipping.");
			passesValidation = false;
		}

		// 验证生命周期
		if (visibleCell.Lifetime < 0)
		{
			ReplicationGraphDebugger.LogWarning($"GridSpatialization2D: Cell ({gridCell.x}x{gridCell.y}) lifetime was negative, " +
				"that shouldn't happen.");
			passesValidation = false;
		}
		return passesValidation;
	}

	// 这个函数是用来获取一个Actor所在的所有网格单元格节点
	private void GetGridNodesForActor(FActorRepListType actor, FActorCellInfo cellInfo, List<UReplicationGraphNode_GridCell> outNodes)
	{
		// 验证单元格信息是否有效
		if (!cellInfo.IsValid())
		{
			Debug.LogError("GetGridNodesForActor: Invalid cell info");
			return;
		}
		// 清空输出列表
		outNodes.Clear();

		// 获取Actor覆盖的网格范围
		int startX = cellInfo.StartX;
		int startY = cellInfo.StartY;
		int endX = cellInfo.EndX;
		int endY = cellInfo.EndY;

		// 确保网格X维度足够大
		while (Grid.Count <= endX)
		{
			Grid.Add(new List<UReplicationGraphNode_GridCell>());
		}

		// 遍历Actor覆盖的所有网格单元
		for (int x = startX; x <= endX; x++)
		{
			var gridY = GetGridX(x);

			// 确保网格Y维度足够大
			while (gridY.Count <= endY)
			{
				gridY.Add(new UReplicationGraphNode_GridCell());
			}

			// 收集每个覆盖单元格的节点
			for (int y = startY; y <= endY; y++)
			{
				var node = GetCell(gridY, y);
				outNodes.Add(node);
			}
		}
	}

}

// 用于缓存Actor信息的辅助类
public class FCachedDynamicActorInfo
{
    public FNewReplicatedActorInfo ActorInfo;
    public FActorCellInfo CellInfo;

    public FCachedDynamicActorInfo(FNewReplicatedActorInfo actorInfo)
    {
        ActorInfo = actorInfo;
    }
}

public class FCachedStaticActorInfo
{
    public FNewReplicatedActorInfo ActorInfo;
    public bool bDormancyDriven;

    public FCachedStaticActorInfo(FNewReplicatedActorInfo actorInfo, bool dormancyDriven)
    {
        ActorInfo = actorInfo;
        bDormancyDriven = dormancyDriven;
    }
}

public struct FActorCellInfo
{
    public int StartX;
    public int StartY;
    public int EndX;
    public int EndY;

    public bool IsValid() => StartX != -1;
    public void Reset() => StartX = -1;
} 