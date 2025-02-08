using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Linq;

public class ReplicationGraphVisualizerInstance : MonoBehaviour
{
	public enum ObserverMode
	{
		Server,         // 服务器视角
		SingleClient,   // 单个客户端视角
		AllClients      // 所有客户端视角
	}

	public enum ObserveeType
	{
		StaticActor,    // 静态被观察对象
		DynamicActor,   // 动态被观察对象
		PlayerCharacter // 玩家角色
	}

	private class ObserverData
	{
		public Vector3 Position;
		public Dictionary<string, ObserveeData> Observees = new Dictionary<string, ObserveeData>();
	}

	private class ObserveeData
	{
		public Vector3 Position;
		public ObserveeType Type;
		public float LastUpdateTime; // 只在被观察者这里保留时间戳
	}

	[Header("观察模式")]
	[SerializeField] private ObserverMode _currentMode = ObserverMode.Server;
	[SerializeField] private string _targetObserverId = "";

	[Header("可视化样式")]
	[SerializeField] private Color _serverColor = new Color(1, 0, 0, 0.3f);
	[SerializeField] private Color _clientColor = new Color(0, 1, 1, 0.3f);
	[SerializeField] private float _observationRadius = 15f;

	[Header("时效性可视化")]
	[SerializeField] private float _recentDataThreshold = 1f;     // 最新数据阈值（秒）
	[SerializeField] private float _staleDataThreshold = 5f;      // 过期数据阈值（秒）
	[SerializeField] private Color _recentDataColor = Color.green; // 新数据颜色
	[SerializeField] private Color _normalDataColor = Color.yellow; // 正常数据颜色
	[SerializeField] private Color _staleDataColor = Color.gray;   // 旧数据颜色

	// 观察者数据: <观察者ID, 观察者数据>
	private Dictionary<string, ObserverData> _observerRegistry = new Dictionary<string, ObserverData>();

	private void Awake()
	{
		ReplicationGraphVisualizer.SetupInstance(this);
	}

	// 字符串转枚举的工具方法
	private ObserveeType StringToType(string type)
	{
		return type.ToLower() switch
		{
			ReplicationGraphVisualizer.TYPE_PLAYER => ObserveeType.PlayerCharacter,
			ReplicationGraphVisualizer.TYPE_DYNAMIC => ObserveeType.DynamicActor,
			_ => ObserveeType.StaticActor
		};
	}

	private ObserverMode StringToMode(string mode)
	{
		return mode.ToLower() switch
		{
			ReplicationGraphVisualizer.MODE_SINGLE_CLIENT => ObserverMode.SingleClient,
			ReplicationGraphVisualizer.MODE_ALL_CLIENTS => ObserverMode.AllClients,
			_ => ObserverMode.Server
		};
	}

	internal void AddObservee_Internal(string observerId, string observeeId, Vector3 position, string type)
	{
		if (!_observerRegistry.TryGetValue(observerId, out var data))
		{
			data = new ObserverData();
			_observerRegistry[observerId] = data;
		}

		data.Observees[observeeId] = new ObserveeData
		{
			Position = position,
			Type = StringToType(type),
			LastUpdateTime = Time.time
		};
	}

	// 更新被观察者位置
	internal void UpdateObservee_Internal(string observerId, string observeeId, Vector3 position)
	{
		// 确保观察者存在
		if (!_observerRegistry.TryGetValue(observerId, out var data))
		{
			data = new ObserverData();
			_observerRegistry[observerId] = data;
		}

		// 如果被观察者不存在，则添加一个新的
		if (!data.Observees.TryGetValue(observeeId, out var observee))
		{
			// 注意：由于是Update时发现的，我们默认其为动态对象类型
			observee = new ObserveeData
			{
				Type = ObserveeType.DynamicActor,
				Position = position,
				LastUpdateTime = Time.time
			};
			data.Observees[observeeId] = observee;
		}
		else
		{
			// 已存在则更新位置和时间
			observee.Position = position;
			observee.LastUpdateTime = Time.time;
		}
	}

	// 移除被观察者
	internal void RemoveObservee_Internal(string observerId, string observeeId)
	{
		if (_observerRegistry.TryGetValue(observerId, out var data) &&
			data.Observees.TryGetValue(observeeId, out var actor))
		{
			data.Observees.Remove(observeeId);
		}
	}

	// 设置观察模式
	internal void SetViewMode(string mode, string targetId = "")
	{
		_currentMode = StringToMode(mode);
		_targetObserverId = targetId;
	}

	// 增加观察者
	internal void AddObserver_Internal(string observerId, Vector3 position)
	{
		if (!_observerRegistry.TryGetValue(observerId, out var data))
		{
			data = new ObserverData();
			_observerRegistry[observerId] = data;
		}
		data.Position = position;
	}

	// 更新观察者位置
	internal void UpdateObserver_Internal(string observerId, Vector3 position)
	{
		// 确保观察者存在
		if (!_observerRegistry.TryGetValue(observerId, out var data))
		{
			data = new ObserverData();
			_observerRegistry[observerId] = data;
		}

		data.Position = position;
	}

	// 移除观察者
	internal void RemoveObserver_Internal(string observerId)
	{
		_observerRegistry.Remove(observerId);
	}

	// 绘制单个客户端视角
	private void DrawSingleClient(string observerId, Color? overrideColor = null)
	{
		if (_observerRegistry.TryGetValue(observerId, out var data))
		{
			// 使用传入的颜色或默认的客户端颜色
			Gizmos.color = overrideColor ?? _clientColor;
			
			// 只为非服务器观察者绘制观察范围
			if (observerId != ReplicationGraphVisualizer.MODE_SERVER)
			{
				DrawViewSphere(data.Position);
			}
			
			DrawObservees(data);
		}
	}

	// 绘制所有客户端视角
	private void DrawAllClients()
	{
		Color[] colors = { Color.red, Color.green, Color.blue };
		int colorIndex = 0;
		
		foreach (var observerId in _observerRegistry.Keys)
		{
			// 跳过服务器观察者
			if (observerId == ReplicationGraphVisualizer.MODE_SERVER)
				continue;
				
			// 为每个客户端使用不同的半透明颜色
			Color observerColor = colors[colorIndex % colors.Length] * 0.5f;
			DrawSingleClient(observerId, observerColor);
			colorIndex++;
		}
	}

	private void DrawViewSphere(Vector3 center)
	{
		Gizmos.DrawWireSphere(center, _observationRadius);
		
		#if UNITY_EDITOR
		// 显示半径信息
		string radiusInfo = $"R:{_observationRadius}m";
		Handles.Label(center + Vector3.up, radiusInfo);
		#endif
	}

	private void DrawObservees(ObserverData data)
	{
		float currentTime = Time.time;
		
		foreach (var actor in data.Observees)
		{
			float timeSinceUpdate = currentTime - actor.Value.LastUpdateTime;
			Color baseColor = GetTypeColor(actor.Value.Type);
			Color timeBasedColor = GetTimeBasedColor(timeSinceUpdate);
			
			// 混合类型颜色和时效性颜色
			Gizmos.color = Color.Lerp(baseColor, timeBasedColor, 0.5f);
			Gizmos.DrawCube(actor.Value.Position, GetTypeSize(actor.Value.Type));
			
			#if UNITY_EDITOR
			// 显示更新时间
			string timeInfo = $"{timeSinceUpdate:F1}s";
			Handles.Label(actor.Value.Position + Vector3.up * 1.5f, timeInfo);
			#endif
		}
	}

	private Color GetTypeColor(ObserveeType type)
	{
		return type switch
		{
			ObserveeType.PlayerCharacter => Color.green,
			ObserveeType.DynamicActor => Color.yellow,
			_ => Color.gray
		};
	}

	private Vector3 GetTypeSize(ObserveeType type)
	{
		return type switch
		{
			ObserveeType.PlayerCharacter => new Vector3(1, 2, 1),
			ObserveeType.DynamicActor => Vector3.one * 0.8f,
			_ => Vector3.one * 0.5f
		};
	}

	private Color GetTimeBasedColor(float timeSinceUpdate)
	{
		if (timeSinceUpdate <= _recentDataThreshold)
		{
			// 最新数据
			return _recentDataColor;
		}
		else if (timeSinceUpdate <= _staleDataThreshold)
		{
			// 正常数据，根据时间插值
			float t = (timeSinceUpdate - _recentDataThreshold) / 
					 (_staleDataThreshold - _recentDataThreshold);
			return Color.Lerp(_normalDataColor, _staleDataColor, t);
		}
		else
		{
			// 过期数据
			return _staleDataColor;
		}
	}

	// 根据当前模式选择绘制方式
	private void OnDrawGizmos()
	{
		if (!Application.isPlaying) return;

		switch (_currentMode)
		{
			case ObserverMode.Server:
				DrawSingleClient(ReplicationGraphVisualizer.MODE_SERVER, _serverColor);
				break;
			case ObserverMode.SingleClient:
				DrawSingleClient(_targetObserverId);
				break;
			case ObserverMode.AllClients:
				DrawAllClients();
				break;
		}
	}

	public List<string> GetObservers()
	{
		return _observerRegistry?
			.Keys
			.Where(id => id != ReplicationGraphVisualizer.MODE_SERVER)
			.OrderBy(id => id)
			.ToList();
	}
}