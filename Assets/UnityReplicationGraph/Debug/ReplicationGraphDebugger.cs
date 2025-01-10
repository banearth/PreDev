using UnityEngine;
using System.Collections.Generic;
using CPL;

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

    public void DrawActors(IEnumerable<TestActor> actors, System.Func<TestActor, bool> isVisibleFunc)
    {
        foreach (var actor in actors)
        {
            DrawActor(actor, isVisibleFunc(actor));
        }
    }

    private void DrawViewer(NetViewer viewer, float viewRadius)
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

    private void DrawActor(TestActor actor, bool isVisible)
    {
        var color = isVisible ? visibleActorColor : culledActorColor;
        DebugDraw.DrawSolidSphere(actor.Position, 1f, color, 0);
        //if (actor.IsMoving)
        //{
        //    DebugDraw.DrawDisc(actor.InitialPosition, Vector3.up, actor.MoveRadius, Color.yellow, 0);
        //}
    }
}