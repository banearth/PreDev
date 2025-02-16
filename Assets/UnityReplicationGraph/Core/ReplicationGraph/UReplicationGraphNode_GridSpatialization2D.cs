using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEditor.PackageManager;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using ReplicationGraph;

public class UReplicationGraphNode_GridSpatialization2D : UReplicationGraphNode
{
    public float CellSize { get; set; }
    public Vector2 SpatialBias { get; set; }
    public float ConnectionMaxZ { get; set; } = 100000f; // 类似UE的UE::Net::Private::RepGraphWorldMax

	private Bounds? GridBounds;

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

	//这是一份被复用的 List，用于收集 actor 节点
	private List<UReplicationGraphNode_GridCell> GatheredNodes = new List<UReplicationGraphNode_GridCell>();

	// 某些Actor类（如投射物）不允许触发空间化树的重建，而是会被限制在现有边界内
	// 例如：子弹、火箭等投射物
	// 这是一个性能优化的设计决策，防止临时性的、快速移动的对象导致昂贵的空间重建操作。
	private HashSet<string> ClassRebuildDenyList = new HashSet<string>();

	private bool bNeedsRebuild = false;
	private bool bGridGizmosDirty = false;

	public UReplicationGraphNode_GridSpatialization2D()
    {
        bRequiresPrepareForReplicationCall = true;
    }

    private List<UReplicationGraphNode_GridCell> GetGridX(int x)
    {
        while (Grid.Count <= x)
        {
            Grid.Add(new List<UReplicationGraphNode_GridCell>());
			Debug.Log("Grid.Add");
			bGridGizmosDirty = true;
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

	public void AddActor_Dormancy(FNewReplicatedActorInfo actorInfo, FGlobalActorReplicationInfo actorRepInfo)
	{
		ReplicationGraphDebugger.LogInfo($"UReplicationGraphNode_GridSpatialization2D::AddActor_Dormancy {actorInfo.Actor.Name}");
		if (actorRepInfo.bWantsToBeDormant)
		{
			// 如果Actor想要休眠，作为静态Actor添加
			AddActorInternal_Static(actorInfo, actorRepInfo, true);
		}
		else
		{
			// 否则作为动态Actor添加
			AddActorInternal_Dynamic(actorInfo);
		}
		// 监听休眠状态变化事件，因为我们需要在状态改变时移动Actor
		// 注意我们不关心Flush操作
		actorRepInfo.Events.DormancyChange += OnNetDormancyChange;
	}

	protected virtual void AddActorInternal_Dynamic(FNewReplicatedActorInfo actorInfo)
	{
		
		// 将Actor添加到动态Actor集合中
		DynamicSpatializedActors[actorInfo.Actor] = new FCachedDynamicActorInfo(actorInfo);
	}

	protected virtual void AddActorInternal_Static(FNewReplicatedActorInfo actorInfo, FGlobalActorReplicationInfo actorRepInfo, bool bDormancyDriven)
	{
		var actor = actorInfo.Actor;
		// 验证检查
		// 源码中这里存在校验的需求，防止重复添加
		// bool containsActor = PendingStaticSpatializedActors.Exists(entry => entry.Actor == actorInfo.Actor);
		AddActorInternal_Static_Implementation(actorInfo, actorRepInfo, bDormancyDriven);
	}

	protected virtual void AddActorInternal_Static_Implementation(
		FNewReplicatedActorInfo actorInfo,
		FGlobalActorReplicationInfo actorRepInfo,
		bool bDormancyDriven)
	{
		var actor = actorInfo.Actor;
		Vector3 location3D = actor.Position;
		actorRepInfo.WorldLocation = location3D;
		// 检查Actor位置是否会导致空间边界扩大
		if (WillActorLocationGrowSpatialBounds(location3D))
		{
			HandleActorOutOfSpatialBounds(actor, location3D, true);
		}
		// 添加到静态Actor集合
		StaticSpatializedActors[actor] = new FCachedStaticActorInfo(actorInfo, bDormancyDriven);
		// 只有在不需要重建整个网格时才将Actor放入单元格
		if (!bNeedsRebuild)
		{
			PutStaticActorIntoCell(actorInfo, actorRepInfo, bDormancyDriven);
		}
	}

	private FActorCellInfo GetCellInfoForActor(FActorRepListType actor, Vector3 location3D, float cullDistance)
	{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
		if (cullDistance <= 0f)
		{
			ReplicationGraphDebugger.LogWarning(
				$"GetGridNodesForActor called on {actor.Name} when its CullDistance = {cullDistance:F2}. (Must be > 0)");
		}
#endif

		// 检查并限制位置在有效范围内
		Vector3 clampedLocation = location3D;
		float worldMax = 1000000f; // 类似UE的RepGraphHalfWorldMax

		// 检查位置是否在有效范围内
		if (location3D.x < -worldMax || location3D.x > worldMax ||
			location3D.y < -worldMax || location3D.y > worldMax ||
			location3D.z < -worldMax || location3D.z > worldMax)
		{
			var actorRepInfo = GraphGlobals.GlobalActorReplicationInfoMap.Get(actor);
			if (!actorRepInfo.bWasWorldLocClamped)
			{
				ReplicationGraphDebugger.LogWarning(
					$"GetCellInfoForActor: Actor {actor.Name} is outside world bounds with a location of {location3D}. " +
					"Clamping grid location to world bounds.");
				actorRepInfo.bWasWorldLocClamped = true;
			}
			// 限制在世界边界内
			clampedLocation.x = Mathf.Clamp(location3D.x, -worldMax, worldMax);
			clampedLocation.y = Mathf.Clamp(location3D.y, -worldMax, worldMax);
			clampedLocation.z = Mathf.Clamp(location3D.z, -worldMax, worldMax);
		}

		// 计算相对于空间偏移的位置
		float locationBiasX = clampedLocation.x - SpatialBias.x;
		float locationBiasZ = clampedLocation.z - SpatialBias.y; // Unity使用Z轴

		// 计算包含剔除距离的边界
		float minX = Mathf.Max(0, locationBiasX - cullDistance);
		float minZ = Mathf.Max(0, locationBiasZ - cullDistance);
		float maxX = locationBiasX + cullDistance;
		float maxZ = locationBiasZ + cullDistance;

		// 如果有网格边界限制，则应用限制
		if (GridBounds.HasValue)
		{
			var boundSize = GridBounds.Value.size;
			maxX = Mathf.Min(maxX, boundSize.x);
			maxZ = Mathf.Min(maxZ, boundSize.z); // Unity使用Z轴
		}

		// 计算格子索引
		return new FActorCellInfo
		{
			StartX = Mathf.Max(0, Mathf.FloorToInt(minX / CellSize)),
			StartY = Mathf.Max(0, Mathf.FloorToInt(minZ / CellSize)), // Unity使用Z轴
			EndX = Mathf.Max(0, Mathf.FloorToInt(maxX / CellSize)),
			EndY = Mathf.Max(0, Mathf.FloorToInt(maxZ / CellSize))    // Unity使用Z轴
		};
	}

	private Vector2Int GetCellCoord(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition.x - SpatialBias.x) / CellSize);
        int y = Mathf.FloorToInt((worldPosition.z - SpatialBias.y) / CellSize);
        return new Vector2Int(x, y);
    }

	public override void PrepareForReplication()
	{
		var globalRepMap = GraphGlobals?.GlobalActorReplicationInfoMap;
		if (globalRepMap == null)
		{
			ReplicationGraphDebugger.LogError("PrepareForReplication: GlobalRepMap is null");
			return;
		}

		// 更新动态Actor
		foreach (var kvp in DynamicSpatializedActors)
		{
			var dynamicActor = kvp.Key;
			var dynamicActorInfo = kvp.Value;
			var previousCellInfo = dynamicActorInfo.CellInfo;
			var actorInfo = dynamicActorInfo.ActorInfo;

			// 更新位置
			var globalActorInfo = globalRepMap.Get(dynamicActor);
			Vector3 location3D = dynamicActor.Position;
			globalActorInfo.WorldLocation = location3D;

			// 检查是否需要扩展空间边界
			if (WillActorLocationGrowSpatialBounds(location3D))
			{
				HandleActorOutOfSpatialBounds(dynamicActor, location3D, false);
			}

			if (!bNeedsRebuild)
			{
				// 获取新的单元格信息
				var newCellInfo = GetCellInfoForActor(dynamicActor, location3D, globalActorInfo.Settings.GetCullDistance());

				if (previousCellInfo.IsValid())
				{
					bool bDirty = false;

					if (newCellInfo.StartX > previousCellInfo.EndX || newCellInfo.EndX < previousCellInfo.StartX ||
						newCellInfo.StartY > previousCellInfo.EndY || newCellInfo.EndY < previousCellInfo.StartY)
					{
						#region 完全不相交的情况(可能性比较小)
						bDirty = true;

						// 从所有旧单元格中移除
						GetGridNodesForActor(dynamicActor, previousCellInfo, GatheredNodes);
						foreach (var node in GatheredNodes)
						{
							node.RemoveDynamicActor(actorInfo);
						}

						// 添加到所有新单元格
						GetGridNodesForActor(dynamicActor, newCellInfo, GatheredNodes);
						foreach (var node in GatheredNodes)
						{
							node.AddDynamicActor(actorInfo);
						}
						#endregion
					}
					else
					{
						#region 处理部分相交的情况
						// 处理部分重叠的情况
						// 处理左侧列
						if (previousCellInfo.StartX < newCellInfo.StartX)
						{
							bDirty = true;
							// 移除左侧不再覆盖的列
							for (int x = previousCellInfo.StartX; x < newCellInfo.StartX; x++)
							{
								var gridX = GetGridX(x);
								for (int y = previousCellInfo.StartY; y <= previousCellInfo.EndY; y++)
								{
									GetCell(gridX, y)?.RemoveDynamicActor(actorInfo);
								}
							}
						}
						else if (previousCellInfo.StartX > newCellInfo.StartX)
						{
							bDirty = true;
							// 添加新的左侧列
							for (int x = newCellInfo.StartX; x <= previousCellInfo.StartX; x++)
							{
								var gridX = GetGridX(x);
								for (int y = newCellInfo.StartY; y <= newCellInfo.EndY; y++)
								{
									GetCell(gridX, y).AddDynamicActor(actorInfo);
								}
							}
						}

						// 处理右侧列
						if (previousCellInfo.EndX < newCellInfo.EndX)
						{
							// 添加新的右侧列
							bDirty = true;
							for (int x = previousCellInfo.EndX + 1; x <= newCellInfo.EndX; ++x)
							{
								var gridX = GetGridX(x);
								for (int y = newCellInfo.StartY; y <= newCellInfo.EndY; ++y)
								{
									GetCell(gridX, y).AddDynamicActor(actorInfo);
								}
							}
						}
						else if (previousCellInfo.EndX > newCellInfo.EndX)
						{
							// 移除右侧不再覆盖的列
							bDirty = true;
							for (int x = newCellInfo.EndX + 1; x <= previousCellInfo.EndX; ++x)
							{
								var GridX = GetGridX(x);
								for (int y = previousCellInfo.StartY; y <= previousCellInfo.EndY; ++y)
								{
									GetCell(GridX, y)?.RemoveDynamicActor(actorInfo);
								}
							}
						}

						// --------------------------------------------------
						// 处理上下行的重叠区域
						// 只处理重叠区域的X范围
						int startX = Mathf.Max(newCellInfo.StartX, previousCellInfo.StartX);
						int endX = Mathf.Min(newCellInfo.EndX, previousCellInfo.EndX);

						// 处理上方行
						if (previousCellInfo.StartY < newCellInfo.StartY)
						{
							// 失去了上方的行
							bDirty = true;
							for (int x = startX; x <= endX; x++)
							{
								var gridX = GetGridX(x);
								for (int y = previousCellInfo.StartY; y < newCellInfo.StartY; y++)
								{
									GetCell(gridX, y)?.RemoveDynamicActor(actorInfo);
								}
							}
						}
						else if (previousCellInfo.StartY > newCellInfo.StartY)
						{
							// 在上方添加了新行
							bDirty = true;
							for (int x = startX; x <= endX; x++)
							{
								var gridX = GetGridX(x);
								for (int y = newCellInfo.StartY; y < previousCellInfo.StartY; y++)
								{
									GetCell(gridX, y).AddDynamicActor(actorInfo);
								}
							}
						}

						// 处理下方行
						if (previousCellInfo.EndY < newCellInfo.EndY)
						{
							// 在下方添加了新行
							bDirty = true;
							for (int x = startX; x <= endX; x++)
							{
								var gridX = GetGridX(x);
								for (int y = previousCellInfo.EndY + 1; y <= newCellInfo.EndY; y++)
								{
									GetCell(gridX, y).AddDynamicActor(actorInfo);
								}
							}
						}
						else if (previousCellInfo.EndY > newCellInfo.EndY)
						{
							// 失去了下方的行
							bDirty = true;

							for (int x = startX; x <= endX; x++)
							{
								var gridX = GetGridX(x);
								for (int y = newCellInfo.EndY + 1; y <= previousCellInfo.EndY; y++)
								{
									GetCell(gridX, y)?.RemoveDynamicActor(actorInfo);
								}
							}
						}
						#endregion
					}
					if (bDirty)
					{
						dynamicActorInfo.CellInfo = newCellInfo;
					}
				}
				else
				{
					// 首次添加
					GetGridNodesForActor(dynamicActor, newCellInfo, GatheredNodes);
					foreach (var node in GatheredNodes)
					{
						node.AddDynamicActor(actorInfo);
					}
					dynamicActorInfo.CellInfo = newCellInfo;
				}
			}
		}

		// 处理网格重建
		if (bNeedsRebuild)
		{
			ReplicationGraphDebugger.LogWarning($"Rebuilding spatialization graph for bias {SpatialBias}");

			// 清理所有现有节点
			foreach (var row in Grid)
			{
				foreach (var cell in row)
				{
					if (cell != null)
					{
						cell.TearDown();
					}
				}
			}
			Grid.Clear();
			bGridGizmosDirty = true;

			// 重新添加所有动态Actor
			foreach (var kvp in DynamicSpatializedActors)
			{
				var dynamicActor = kvp.Key;
				var dynamicActorInfo = kvp.Value;

				Vector3 location3D = dynamicActor.Position;
				var globalActorInfo = globalRepMap.Get(dynamicActor);
				globalActorInfo.WorldLocation = location3D;

				var newCellInfo = GetCellInfoForActor(
					dynamicActor,
					location3D,
					globalActorInfo.Settings.GetCullDistance()
				);

				GetGridNodesForActor(dynamicActor, newCellInfo, GatheredNodes);
				foreach (var node in GatheredNodes)
				{
					node.AddDynamicActor(dynamicActorInfo.ActorInfo);
				}
				dynamicActorInfo.CellInfo = newCellInfo;
			}

			// 重新添加所有静态Actor
			foreach (var kvp in StaticSpatializedActors)
			{
				var staticActor = kvp.Key;
				var staticActorInfo = kvp.Value;

				PutStaticActorIntoCell(
					staticActorInfo.ActorInfo,
					globalRepMap.Get(staticActor),
					staticActorInfo.bDormancyDriven
				);
			}

			bNeedsRebuild = false;
		}
		
		// 绘制Gizmos
		UpdateDrawGizmosGrid2D();
	}


	public override void GatherActorListsForConnection(FConnectionGatherActorListParameters Params)
    {
        // 用于追踪已处理的唯一网格单元
        var uniqueCurrentGridCells = new HashSet<Vector2Int>();

        // 遍历所有观察者
        foreach (var curViewer in Params.Viewers)
        {
            if (curViewer.Connection == null || curViewer.ViewLocation.z > ConnectionMaxZ)
            {
                continue;
            }

			// 限制视图位置在有效范围内
			Vector3 clampedViewLoc = curViewer.ViewLocation;
			if (GridBounds.HasValue)
			{
				clampedViewLoc = GridBounds.Value.ClosestPoint(curViewer.ViewLocation);
			}
			else
			{
				// 实现类似UE的BoundToCube逻辑
				clampedViewLoc = clampedViewLoc.BoundToCube(AllConstants.UE_OLD_HALF_WORLD_MAX);
			}

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

	protected virtual void OnNetDormancyChange(
		FActorRepListType actor,FGlobalActorReplicationInfo globalInfo,
		ENetDormancy newValue,ENetDormancy oldValue)
	{
		// 判断当前和之前的状态是否应该是静态的
		bool bCurrentShouldBeStatic = newValue > ENetDormancy.DORM_Awake;
		bool bPreviousShouldBeStatic = oldValue > ENetDormancy.DORM_Awake;
		if (bCurrentShouldBeStatic && !bPreviousShouldBeStatic)
		{
			// Actor从动态变为静态
			// 从动态列表移除并添加到静态列表
			var actorInfo = new FNewReplicatedActorInfo(actor);
			RemoveActorInternal_Dynamic(actorInfo);
			AddActorInternal_Static(actorInfo, globalInfo, true);
		}
		else if (!bCurrentShouldBeStatic && bPreviousShouldBeStatic)
		{
			// Actor从静态变为动态
			var actorInfo = new FNewReplicatedActorInfo(actor);
			// 第三个参数true表明这个actor之前是作为休眠actor放置的
			// （在这个回调时刻它已经不再休眠了）
			RemoveActorInternal_Static(actorInfo, globalInfo, true);
			AddActorInternal_Dynamic(actorInfo);
		}
	}

	protected virtual void RemoveActorInternal_Dynamic(FNewReplicatedActorInfo actorInfo)
	{
		// 尝试获取动态Actor信息
		if (DynamicSpatializedActors.TryGetValue(actorInfo.Actor, out var dynamicActorInfo))
		{
			// 如果Actor有有效的单元格信息
			if (dynamicActorInfo.CellInfo.IsValid())
			{
				var gatheredNodes = new List<UReplicationGraphNode_GridCell>();
				GetGridNodesForActor(actorInfo.Actor, dynamicActorInfo.CellInfo, gatheredNodes);
				// 从所有相关的网格单元中移除Actor
				foreach (var node in gatheredNodes)
				{
					node.RemoveDynamicActor(actorInfo);
				}
			}
			// 从动态Actor集合中移除
			DynamicSpatializedActors.Remove(actorInfo.Actor);
		}
		else
		{
			// 输出警告日志
			ReplicationGraphDebugger.LogWarning(
				$"UReplicationGraphNode_Simple2DSpatialization::RemoveActorInternal_Dynamic attempted remove " +
				$"{actorInfo.Actor.Name} from streaming dynamic list but it was not there.");
			// 检查是否错误地存在于静态Actor集合中
			if (StaticSpatializedActors.Remove(actorInfo.Actor))
			{
				ReplicationGraphDebugger.LogWarning("   It was in StaticSpatializedActors!");
			}
		}
	}

	protected virtual void RemoveActorInternal_Static(
		FNewReplicatedActorInfo actorInfo,
		FGlobalActorReplicationInfo actorRepInfo,
		bool bWasAddedAsDormantActor)
	{
		// 尝试从静态Actor集合中移除
		if (!StaticSpatializedActors.Remove(actorInfo.Actor))
		{
			// 可能是待处理的Actor，从待处理列表中查找
			// 如果实现了PendingStaticSpatializedActors，需要在PendingStaticSpatializedActors里面删除这个
			// 我暂时没实现，所以不用管
			
			// 如果既不在静态列表也不在待处理列表中，输出警告
			ReplicationGraphDebugger.LogWarning(
				$"UReplicationGraphNode_Simple2DSpatialization::RemoveActorInternal_Static attempted remove " +
				$"{actorInfo.Actor.Name} from static list but it was not there.");
			// 检查是否错误地存在于动态Actor集合中
			if (DynamicSpatializedActors.Remove(actorInfo.Actor))
			{
				ReplicationGraphDebugger.LogWarning("   It was in DynamicStreamingSpatializedActors!");
			}
		}
		// 从Actor所在的网格单元中移除它
		// 注意：即使Actor在最后一次复制帧之后移动，FGlobalActorReplicationInfo也不会被更新
		var gatheredNodes = new List<UReplicationGraphNode_GridCell>();
		GetGridNodesForActor(actorInfo.Actor, actorRepInfo, gatheredNodes);
		foreach (var node in gatheredNodes)
		{
			node.RemoveStaticActor(actorInfo, actorRepInfo, bWasAddedAsDormantActor);
		}
	}

	protected virtual void PutStaticActorIntoCell(FNewReplicatedActorInfo actorInfo, FGlobalActorReplicationInfo actorRepInfo, bool bDormancyDriven)
	{
		// 获取Actor所在的所有网格节点
		var gatheredNodes = new List<UReplicationGraphNode_GridCell>();
		GetGridNodesForActor(actorInfo.Actor, actorRepInfo, gatheredNodes);
		// 将Actor添加到每个相关的网格单元中
		foreach (var node in gatheredNodes)
		{
			node.AddStaticActor(actorInfo, actorRepInfo, bDormancyDriven);
		}
	}

	private void GetGridNodesForActor(FActorRepListType Actor, FGlobalActorReplicationInfo ActorRepInfo, List<UReplicationGraphNode_GridCell> OutNodes)
	{
		GetGridNodesForActor(Actor, GetCellInfoForActor(Actor, ActorRepInfo.WorldLocation, ActorRepInfo.Settings.GetCullDistance()), OutNodes);
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
			bGridGizmosDirty = true;
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

    private bool WillActorLocationGrowSpatialBounds(Vector3 Location)
    {
		// 当设置了边界时，我们不会扩展单元格，而是将Actor限制在边界内
		// 如果GridBounds有效，返回false（不需要增长）
		// 否则，检查Location是否在SpatialBias点的左下方，如果是则需要增长
		return GridBounds.HasValue ? false : (SpatialBias.x > Location.x || SpatialBias.y > Location.z);
    }

	protected virtual void HandleActorOutOfSpatialBounds(FActorRepListType actor, Vector3 location3D, bool bStaticActor)
	{
		// 对于在拒绝列表中的Actor类型，不重建空间化。它们将被限制在网格内。
		if (ClassRebuildDenyList.Contains(actor.ReplicationType))
		{
			return;
		}

		bool bOldNeedRebuild = bNeedsRebuild;

		// 检查并更新X轴空间偏移
		if (SpatialBias.x > location3D.x)
		{
			bNeedsRebuild = true;
			SpatialBias = SpatialBias.WithX(location3D.x - (CellSize / 2.0f));
		}

		// 检查并更新Z轴空间偏移（对应UE的Y轴）
		if (SpatialBias.y > location3D.z) // 注意这里改成了z
		{
			bNeedsRebuild = true;
			SpatialBias = SpatialBias.WithY(location3D.z - (CellSize / 2.0f));
		}

		// 如果需要重建且之前不需要重建，输出警告日志
		if (bNeedsRebuild && !bOldNeedRebuild)
		{
			ReplicationGraphDebugger.LogWarning(
				$"Spatialization Rebuild caused by: {actor.Name} at {location3D}. " +
				$"New Bias: {SpatialBias}. IsStatic: {(bStaticActor ? 1 : 0)}");
		}
	}

	public int GetActorCountInCell(int x, int y)
	{
		// 检查索引是否有效
		if (x < 0 || x >= Grid.Count)
			return 0;
		var row = Grid[x];
		if (y < 0 || y >= row.Count)
			return 0;
		var cell = row[y];
		return cell?.GetActorCount() ?? 0; // 假设GridCell有GetActorCount方法
	}

	public void UpdateDrawGizmosGrid2D()
	{
		if (!bGridGizmosDirty)
		{
			return;
		}
		bGridGizmosDirty = false;

		
		if (Grid == null || Grid.Count == 0)
		{
			ReplicationGraphVisualizer.ClearGrid2D();
			return;
		}

		// 计算网格的实际边界
		float minX = GridBounds.HasValue ? GridBounds.Value.min.x : SpatialBias.x;
		float maxX = GridBounds.HasValue ? GridBounds.Value.max.x : (Grid.Count * CellSize + SpatialBias.x);
		
		// 注意：Unity用Z轴，所以这里用z
		float minZ = GridBounds.HasValue ? GridBounds.Value.min.z : SpatialBias.y;
		float maxZ = GridBounds.HasValue ? GridBounds.Value.max.z : 
			(Grid.Count > 0 ? Grid[0].Count * CellSize + SpatialBias.y : SpatialBias.y);

		// 创建Unity的Rect（x, y, width, height）
		// 注意：Rect的y对应UE的Y轴（Unity的Z轴）
		Rect gridRect = new Rect(
			minX,                    // x起点
			minZ,                    // y起点（对应UE的Y轴/Unity的Z轴）
			maxX - minX,            // 宽度
			maxZ - minZ             // 高度
		);

		// 调用Visualizer的SetupGrid2D
		ReplicationGraphVisualizer.SetupGrid2D(
			CellSize,               // 单元格大小
			SpatialBias.x,         // X轴偏移
			SpatialBias.y,         // Y轴偏移（对应Unity的Z轴）
			Grid.Count,            // X方向网格数量
			Grid[0]?.Count ?? 0,   // Y方向网格数量（对应Unity的Z轴）
			GridBounds.HasValue ? gridRect : null  // 如果有边界限制，传入计算好的Rect
		);
	}

}

// 用于缓存Actor信息的辅助类
public class FCachedDynamicActorInfo
{
    public FNewReplicatedActorInfo ActorInfo;
    public FActorCellInfo CellInfo = FActorCellInfo.CreateInvalid();

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
	public static FActorCellInfo CreateInvalid()
	{
		return new FActorCellInfo{ StartX = -1};
	}
	public bool IsValid() => StartX != -1;
    public void Reset() => StartX = -1;
} 