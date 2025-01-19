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