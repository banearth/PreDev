using UnityEngine;

/// <summary>
/// 保存解析已删除Actor时可能发送给观察者的相关数据
/// </summary>
public struct FRepGraphDestructionViewerInfo
{
	// 观察者位置
	public Vector3 ViewerLocation;
	
	// 上次超出范围的位置检查
	public Vector3 LastOutOfRangeLocationCheck;

	public FRepGraphDestructionViewerInfo(Vector3 viewerLocation, Vector3 outOfRangeLocationCheck)
	{
		ViewerLocation = viewerLocation;
		LastOutOfRangeLocationCheck = outOfRangeLocationCheck;
	}
}