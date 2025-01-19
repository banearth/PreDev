using UnityEngine;
using System.Collections.Generic;

public class FNetViewer
{
    public UNetConnection Connection { get; private set; }
    public Vector3 ViewLocation { get; set; }
	public Vector3 ViewDir { get; set; }
	public FActorRepListType InViewer { get; set; }
	public FActorRepListType ViewTarget { get; set; }

	// 简化版本的构造函数
	public FNetViewer(UNetConnection inConnection)
    {
        Connection = inConnection;
        ViewLocation = Vector3.zero;  // 初始化位置
    }
}