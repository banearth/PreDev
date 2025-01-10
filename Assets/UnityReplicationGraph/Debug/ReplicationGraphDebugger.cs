using UnityEngine;
using System.Collections.Generic;
using CPL;  // 确保只使用CPL命名空间

public class ReplicationGraphDebugger
{
    private Color clientViewColor = new Color(0, 0, 1, 0.2f);
    private Color clientViewBorderColor = new Color(0, 0, 1, 1f);
    private Color viewerPositionColor = Color.yellow;
    private Color visibleActorColor = Color.green;
    private Color culledActorColor = Color.red;
    private float viewerCrossSize = 2f;

    public void DrawViewers(IEnumerable<NetViewer> viewers, float viewRadius)
    {
        foreach (var viewer in viewers)
        {
            DrawViewer(viewer, viewRadius);
        }
    }

    private void DrawViewer(NetViewer viewer, float viewRadius)
    {
        // 移除所有Handles相关代码，完全使用DebugDraw
        DebugDraw.DrawDisc(viewer.ViewLocation, Vector3.up, viewRadius, clientViewColor, 0);
        DebugDraw.DrawDisc(viewer.ViewLocation, Vector3.up, viewRadius, clientViewBorderColor, 0);

        // 绘制viewer位置标记
        Vector3 pos = viewer.ViewLocation;
        DebugDraw.DrawLine(
            pos + Vector3.left * viewerCrossSize,
            pos + Vector3.right * viewerCrossSize,
            viewerPositionColor,
            0
        );
        DebugDraw.DrawLine(
            pos + Vector3.forward * viewerCrossSize,
            pos + Vector3.back * viewerCrossSize,
            viewerPositionColor,
            0
        );
    }

    public void DrawActors(IEnumerable<TestActor> actors, System.Func<TestActor, bool> isVisibleFunc)
    {
        foreach (var actor in actors)
        {
            Color color = isVisibleFunc(actor) ? visibleActorColor : culledActorColor;
            DebugDraw.DrawSphere(actor.Position, 1f, color, 0);

            if (actor.IsMoving)
            {
                DebugDraw.DrawDisc(actor.InitialPosition, Vector3.up, actor.MoveRadius, Color.yellow, 0);
            }
        }
    }
}