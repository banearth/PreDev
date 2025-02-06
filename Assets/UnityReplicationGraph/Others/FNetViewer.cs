using UnityEngine;
using System.Collections.Generic;
using System;

public struct FNetViewer
{
    public UNetConnection Connection;
    public FActorRepListType InViewer;      // PlayerController或OwningActor
	public FActorRepListType ViewTarget;
	public Vector3 ViewLocation;
	public Vector3 ViewDir;

	public FNetViewer(UNetConnection inConnection)
    {
        // 基础参数校验
        if (inConnection == null || inConnection.OwningActor == null)
            throw new ArgumentNullException(nameof(inConnection));

        Connection = inConnection;
        
        // 确定观察者身份
        InViewer = inConnection.OwningActor;
        ViewTarget = inConnection.ViewTarget;

        // 获取基础视角位置
        ViewLocation = ViewTarget.Position;
		ViewDir = Vector3.zero;

		// 在获得精确视角
		InViewer.GetPlayerViewPoint(ref ViewLocation,ref ViewDir);
        
    }
}