using UnityEngine;
using System.Collections.Generic;
using CPL;
using System;

public static class ReplicationGraphDebugger
{
    #region 调试配置

    private static Color clientViewColor = new Color(0, 0, 1, 0.2f);
    private static Color clientViewBorderColor = new Color(0, 0, 1, 1f);
    private static Color viewerPositionColor = Color.yellow;
    private static Color visibleActorColor = Color.green;
    private static Color culledActorColor = Color.red;
    private static float viewerCrossSize = 2f;

    // 验证开关
    public static bool CVar_RepGraph_Verify = true;
	public static int CVar_RepGraph_LogNetDormancyDetails = 0;
	public static int CVar_RepGraph_TrickleDistCullOnDormancyNodes = 1;
    public static float CVar_RepGraph_OutOfRangeDistanceCheckRatio = 0.5f;
    public static int CVar_ForceConnectionViewerPriority = 1;
	public static bool CVar_RepGraph_EnableFastSharedPath = true;
	public static int CVar_RepGraph_DormantDynamicActorsDestruction = 0;//If true, irrelevant dormant actors will be destroyed on the client
	#endregion

	#region 验证和日志

	private static HashSet<string> ReportedEnsures = new HashSet<string>();

    /// <summary>
    /// 检查条件，首次失败时记录错误并在开发阶段触发断点
    /// </summary>
    public static bool Ensure(bool condition)
    {
        if (!condition)
        {
            string stackTrace = System.Environment.StackTrace;
            if (ReportedEnsures.Add(stackTrace))  // 只在首次失败时报告
            {
                Debug.LogError($"[RepGraph] Ensure failed at: {stackTrace}");
                #if UNITY_EDITOR
                Debug.Break();
                #endif
            }
        }
        return condition;
    }

    /// <summary>
    /// 检查条件，首次失败时记录带格式的错误消息并在开发阶段触发断点
    /// </summary>
    public static bool EnsureMsg(bool condition, string message)
    {
        if (!condition)
        {
            string stackTrace = System.Environment.StackTrace;
            if (ReportedEnsures.Add(stackTrace))  // 只在首次失败时报告
            {
                Debug.LogError($"[RepGraph] Ensure failed: {message}\nAt: {stackTrace}");
                #if UNITY_EDITOR
                Debug.Break();
                #endif
            }
        }
        return condition;
    }

    /// <summary>
    /// 检查条件，每次失败都记录错误并在开发阶段触发断点
    /// </summary>
    public static bool EnsureAlways(bool condition)
    {
        if (!condition)
        {
            Debug.LogError($"[RepGraph] EnsureAlways failed at: {System.Environment.StackTrace}");
            #if UNITY_EDITOR
            Debug.Break();
            #endif
        }
        return condition;
    }

    /// <summary>
    /// 检查条件，每次失败都记录带格式的错误消息并在开发阶段触发断点
    /// </summary>
    public static bool EnsureAlwaysMsg(bool condition, string message)
    {
        if (!condition)
        {
            Debug.LogError($"[RepGraph] EnsureAlways failed: {message}\nAt: {System.Environment.StackTrace}");
            #if UNITY_EDITOR
            Debug.Break();
            #endif
        }
        return condition;
    }
    
    public static void LogInfo(string message)
    {
        Debug.Log($"[RepGraph] {message}");
    }

    public static void LogWarning(string message)
    {
        Debug.LogWarning($"[RepGraph] {message}");
    }

    public static void LogError(string message)
    {
        Debug.LogError($"[RepGraph] {message}");
    }

    #endregion

    #region 可视化调试

    public static void DrawViewers(IEnumerable<FNetViewer> viewers, float viewRadius)
    {
        foreach (var viewer in viewers)
        {
            DrawViewer(viewer, viewRadius);
        }
    }

    public static void DrawActors(IEnumerable<TestActor> actors, System.Func<TestActor, bool> isVisibleFunc)
    {
        foreach (var actor in actors)
        {
            DrawActor(actor, isVisibleFunc(actor));
        }
    }

    private static void DrawViewer(FNetViewer viewer, float viewRadius)
    {
		// 绘制填充的半透明圆形
		DebugDraw.DrawSolidDisc(viewer.ViewLocation, Vector3.up, viewRadius, clientViewColor, 0);

		// 绘制边缘线
		DebugDraw.DrawDisc(viewer.ViewLocation, Vector3.up, viewRadius, clientViewBorderColor, 0);

		// 绘制viewer位置标记
		DebugDraw.DrawLine(
			viewer.ViewLocation + Vector3.left * viewerCrossSize,
			viewer.ViewLocation + Vector3.right * viewerCrossSize,
			viewerPositionColor, 0);
		DebugDraw.DrawLine(
			viewer.ViewLocation + Vector3.forward * viewerCrossSize,
			viewer.ViewLocation + Vector3.back * viewerCrossSize,
			viewerPositionColor, 0);

        // 绘制名字
        if(viewer.ViewTarget != null)
        {
            DebugDraw.DrawLabel(viewer.ViewLocation, viewer.ViewTarget.Name, Color.white, 0);
        }
    }

    private static void DrawActor(TestActor actor, bool isVisible)
    {
        var color = isVisible ? visibleActorColor : culledActorColor;
        DebugDraw.DrawSolidSphere(actor.Position, 1f, color, 0);
        //if (actor.IsMoving)
        //{
        //    DebugDraw.DrawDisc(actor.InitialPosition, Vector3.up, actor.MoveRadius, Color.yellow, 0);
        //}
    }

    public static void Draw_UReplicationGraphNode_GridSpatialization2D(UReplicationGraphNode_GridSpatialization2D graphNode)
    {
        if (graphNode == null) return;

        // 获取网格参数
        float cellSize = graphNode.CellSize;
        Vector2 spatialBias = graphNode.SpatialBias;
        
        // 计算网格范围（这里可能需要根据实际情况调整）
        float worldSize = 200000f;
        int gridCellsX = Mathf.CeilToInt(worldSize / cellSize);
        int gridCellsY = Mathf.CeilToInt(worldSize / cellSize);

        // 定义网格颜色
        Color gridColor = new Color(0, 1, 0, 0.2f);        // 基础网格颜色
        Color occupiedColor = new Color(1, 0, 0, 0.2f);    // 有Actor的格子颜色
        Color borderColor = new Color(1, 1, 0, 1f);        // 边界颜色

        // 绘制每个格子
        for (int x = 0; x < gridCellsX; x++)
        {
            for (int y = 0; y < gridCellsY; y++)
            {
                Vector3 cellCenter = new Vector3(
                    spatialBias.x + (x + 0.5f) * cellSize,
                    0,  // Y轴在Unity中是上下方向
                    spatialBias.y + (y + 0.5f) * cellSize
                );

                // 绘制网格线框
                DebugDraw.DrawBox(
                    cellCenter,
                    new Vector3(cellSize, 100f, cellSize),  // 给一定高度以便观察
                    gridColor,
                    0  // duration = 0 表示只绘制一帧
                );

                // 如果格子中有Actor，绘制填充
                var actorCountInCell = graphNode.GetActorCountInCell(x, y);
				if (actorCountInCell > 0)
                {
					DebugDraw.DrawSolidBox(cellCenter,
						new Vector3(cellSize * 0.9f, 90f, cellSize * 0.9f),  // 稍微小一点避免完全重叠
						occupiedColor,
						0);
					// 可选：显示Actor数量
					DebugDraw.DrawLabel(cellCenter, $"Actors: {actorCountInCell}", Color.white, 0);
				}
            }
        }

        // 绘制整体边界
        Vector3 gridCenter = new Vector3(
            spatialBias.x + (gridCellsX * cellSize) * 0.5f,
            50f,  // 高度居中
            spatialBias.y + (gridCellsY * cellSize) * 0.5f
        );
        
        Vector3 gridSize = new Vector3(
            gridCellsX * cellSize,
            100f,
            gridCellsY * cellSize
        );

		DebugDraw.DrawBox(gridCenter, gridSize, borderColor, 0);
	}

	#endregion

	#region 其他

	public static string GetActorRepListTypeDebugString(FActorRepListType In)
    {
        if(In == null)
        {
            return "None";
		}
		else
		{
            return In.Name;
		}
	}

	public static void LogActorRepList(FReplicationGraphDebugInfo debugInfo, string prefix, FActorRepListRefView list)
    {
        if (list.Num() <= 0)
        {
            return;
        }

        var actorListStr = $"{prefix} [{list.Num()} Actors] ";

        if (debugInfo.Flags == FReplicationGraphDebugInfo.EFlags.ShowActors)
        {
            foreach (var actor in list)
            {
                actorListStr += GetActorRepListTypeDebugString(actor) + " ";
            }
        }
        else if (debugInfo.Flags == FReplicationGraphDebugInfo.EFlags.ShowClasses || 
            debugInfo.Flags == FReplicationGraphDebugInfo.EFlags.ShowNativeClasses)
        {
            var classCount = new Dictionary<Type, int>();
            foreach (var actor in list)
            {
                var actorClass = actor.GetType();
				var temp = classCount.GetOrAdd(actorClass, () => 0);
				classCount[actorClass] = temp + 1;
            }
            foreach (var kvp in classCount)
            {
                actorListStr += $"{kvp.Key.Name}:[{kvp.Value}] ";
            }
        }

        debugInfo.Log(actorListStr);
    }

	#endregion
}