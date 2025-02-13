using UnityEngine;
using System.Collections.Generic;

namespace ReplicationGraph
{
	public static class ReplicationGraphVisualizer
	{
		// Lua可访问的常量定义
		public const string MODE_SERVER = "server";
		public const string MODE_SINGLE_CLIENT = "single";
		public const string MODE_ALL_CLIENTS = "all";

		public const string TYPE_STATIC = "static";
		public const string TYPE_DYNAMIC = "dynamic";
		public const string TYPE_PLAYER = "player";

		private static ReplicationGraphVisualizerInstance _instance;
		public static ReplicationGraphVisualizerInstance Instance
		{
			get
			{
				if (_instance == null)
				{
					var go = new GameObject("ReplicationGraphVisualizer");
					_instance = go.AddComponent<ReplicationGraphVisualizerInstance>();
				}
				return _instance;
			}
		}

		public static void SetupInstance(ReplicationGraphVisualizerInstance instance)
		{
			_instance = instance;
		}

		// 增加全局被观察者
		public static void AddGlobalObservee(string observeeId, float x, float y, float z, string type)
		{
			Instance.AddGlobalObservee_Internal(observeeId, new Vector3(x, y, z), type);
		}

		// 更新全局被观察者位置
		public static void UpdateGlobalObservee(string observeeId, float x, float y, float z)
		{
			Instance.UpdateGlobalObservee_Internal(observeeId, new Vector3(x, y, z));
		}

		// 移除全局被观察者
		public static void RemoveGlobalObservee(string observeeId)
		{
			Instance.RemoveGlobalObservee_Internal(observeeId);
		}

		// 更新被观察者位置
		public static void UpdateObservee(string observerId, string observeeId)
		{
			Instance.UpdateObservee_Internal(observerId, observeeId);
		}

		// 切换观察者视角
		// 可以是服务器，也可以是客户端
		// 存在几个特殊的ID：
		// 1. "server" - 服务器视角
		// 2. "all" - 所有客户端视角
		// 3. 其他 - 特定客户端视角
		public static void SwitchObserver(string observerId)
		{
			switch (observerId.ToLower())
			{
				case MODE_SERVER:
					Instance.SetViewMode(MODE_SERVER);
					break;
				case MODE_ALL_CLIENTS:
					Instance.SetViewMode(MODE_ALL_CLIENTS);
					break;
				default:
					Instance.SetViewMode(MODE_SINGLE_CLIENT, observerId);
					break;
			}
		}

		// 增加观察者
		public static void AddObserver(string observerId, float x, float y, float z, float viewRadius)
		{
			Instance.AddObserver_Internal(observerId, new Vector3(x, y, z), viewRadius);
		}

		// 更新观察者位置
		public static void UpdateObserver(string observerId, float x, float y, float z,float viewRadius)
		{
			Instance.UpdateObserver_Internal(
				observerId,
				new Vector3(x, y, z),
				viewRadius
			);
		}

		// 移除观察者
		public static void RemoveObserver(string observerId)
		{
			Instance.RemoveObserver_Internal(observerId);
		}

	}
}