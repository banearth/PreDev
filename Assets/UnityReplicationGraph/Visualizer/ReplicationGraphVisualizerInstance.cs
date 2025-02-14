using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UIElements;


#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Linq;

namespace ReplicationGraph
{
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
			private static int NEXT_UUID = 0;
			public int uuid;
			public Vector3 position;
			public float viewRadius;
			public Dictionary<string, ObserveeData> observees = new Dictionary<string, ObserveeData>();
			public ObserverData()
			{
				uuid = NEXT_UUID++;
			}
		}
		private class ObserveeData
		{
			public string name;
			public Vector3 position;
			public ObserveeType type;
			public float lastUpdateTime;
			public void CopyFrom(ObserveeData other)
			{
				name = other.name;
				position = other.position;
				type = other.type;
				lastUpdateTime = other.lastUpdateTime;
			}
		}

		private class LabelContent
		{
			public string text;
			public Color color;
		}

		[Header("观察模式")]
		[SerializeField] private ObserverMode _currentMode = ObserverMode.Server;
		[SerializeField] private string _targetObserverId = "";

		[Header("可视化样式")]
		[SerializeField] private Color [] _viewColors = new Color[]
		{
			new Color(0, 1, 1, 0.3f),
			new Color(0, 1, 1, 0.3f),
			new Color(0, 1, 1, 0.3f),
		};
		[SerializeField] private Color _borderColor = new Color(0, 1, 1, 0.3f);

		[Header("时效性可视化")]
		[SerializeField] private float _recentDataThreshold = 1f;     // 最新数据阈值（秒）
		[SerializeField] private float _staleDataThreshold = 5f;      // 过期数据阈值（秒）
		[SerializeField] private Color _recentDataColor = Color.green; // 新数据颜色
		[SerializeField] private Color _normalDataColor = Color.yellow; // 正常数据颜色
		[SerializeField] private Color _staleDataColor = Color.gray;   // 旧数据颜色

		[Header("调试显示")]
		[SerializeField] private bool _onguiEnable = true;
		[SerializeField] private float _smartLabelOffsetMultiple = 1;// 智能Label整体偏移倍数
		[SerializeField] private float _smartLabelBaseOffset = 0.5f; // 智能Label基础偏移
		[SerializeField] private bool _showUpdateTime = true;    // 是否显示更新时间
		[SerializeField] private bool _showRadius = true;        // 是否显示观察半径
		[SerializeField] private bool _showLegend = true;        // 是否显示图例
		[SerializeField] private int _nameDisplayMask = -1;      // 默认全部显示

		// 定义显示选项的枚举（按位标记）
		[System.Flags]
		public enum NameDisplayOptions
		{
			None = 0,
			StaticActor = 1 << 0,
			DynamicActor = 1 << 1,
			PlayerCharacter = 1 << 2,
			All = StaticActor | DynamicActor | PlayerCharacter
		}

		// 观察者数据: <观察者ID, 观察者数据>
		private Dictionary<string, ObserverData> _observerRegistry = new Dictionary<string, ObserverData>();

		private SmartLabel _smartLabel;

		private void Awake()
		{
			ReplicationGraphVisualizer.SetupInstance(this);
			_smartLabel = new SmartLabel(_smartLabelOffsetMultiple, _smartLabelBaseOffset);
		}

		private void OnValidate()
		{
			// 当在Inspector中修改offset时，更新SmartLabel的偏移量
			if (_smartLabel != null)
			{
				_smartLabel.SetOffsetMultiple(_smartLabelOffsetMultiple);
				_smartLabel.SetBaseOffset(_smartLabelBaseOffset);
			}
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

		#region Observee

		private Dictionary<string, ObserveeData> _globalObservees = new Dictionary<string, ObserveeData>();

		internal void AddGlobalObservee_Internal(string observeeId, Vector3 position, string type)
		{
			_globalObservees[observeeId] = new ObserveeData
			{
				name = observeeId,
				position = position,
				type = StringToType(type),
				lastUpdateTime = Time.time,
			};
		}

		internal void RemoveGlobalObservee_Internal(string observeeId)
		{
			_globalObservees.Remove(observeeId);
		}

		internal void UpdateGlobalObservee_Internal(string observeeId, Vector3 position)
		{
			if(_globalObservees.TryGetValue(observeeId, out ObserveeData observeeData))
			{
				observeeData.position = position;
				observeeData.lastUpdateTime = Time.time;
			}
		}

		// 更新被观察者位置
		internal void UpdateObservee_Internal(string observerId, string observeeId)
		{
			// 如果全局被观察者存在
			if (_globalObservees.TryGetValue(observeeId, out var globalData))
			{
				// 观察者不存在，则需要创建
				if (!_observerRegistry.TryGetValue(observerId, out var observerData))
				{
					observerData = new ObserverData();
					_observerRegistry[observerId] = observerData;
				}
				// 如果本地被观察者不存在，则需要创建
				if (!observerData.observees.TryGetValue(observeeId, out var localData))
				{
					localData = new ObserveeData();
					observerData.observees[observeeId] = new ObserveeData();
				}
				localData.CopyFrom(globalData);
			}
			else
			{
				// 如果全局观察者不存在，本地观察者已经存在，则需要删除
				if (_observerRegistry.TryGetValue(observerId, out var observerData))
				{
					if (observerData.observees.ContainsKey(observeeId))
					{
						observerData.observees.Remove(observeeId);
					}
				}
			}
		}

		#endregion

		#region Observer

		// 增加观察者
		internal void AddObserver_Internal(string observerId, Vector3 position, float viewRadius)
		{
			if (!_observerRegistry.TryGetValue(observerId, out var data))
			{
				data = new ObserverData();
				_observerRegistry[observerId] = data;
				RefreshCachedObservers();
			}
			data.position = position;
			data.viewRadius = viewRadius;
		}

		// 更新观察者位置
		internal void UpdateObserver_Internal(string observerId, Vector3 position, float viewRadius)
		{
			// 确保观察者存在
			if (_observerRegistry.TryGetValue(observerId, out var data))
			{
				data.position = position;
				data.viewRadius = viewRadius;
			}
		}

		// 移除观察者
		internal void RemoveObserver_Internal(string observerId)
		{
			if (_observerRegistry.ContainsKey(observerId))
			{
				_observerRegistry.Remove(observerId);
				RefreshCachedObservers();
			}
		}

		#endregion

		// 设置观察模式
		internal void SetViewMode(string mode, string targetId = "")
		{
			_currentMode = StringToMode(mode);
			_targetObserverId = targetId;
		}

		// 绘制单个客户端视角
		private void DrawSingleClient(string observerId)
		{
			if (_observerRegistry.TryGetValue(observerId, out var observerData))
			{
				bool isServer = observerId == ReplicationGraphVisualizer.MODE_SERVER;
				// 只绘制客户端的
				if (!isServer)
				{
					ReplicationGraphVisualizerUtils.DrawObserver(observerData.position, observerData.viewRadius, _viewColors[0], _borderColor);
				}

				// 绘制被观察者（不带十字标记）
				DrawObservees(observerData);
			}
		}

		// 绘制所有客户端视角
		private void DrawAllClients()
		{
			int colorIndex = 0;
			foreach (var pair  in _observerRegistry)
			{
				var observerData = pair.Value;
				var observerId = pair.Key;
				if (observerId == ReplicationGraphVisualizer.MODE_SERVER)
					continue;
				// 为每个客户端使用不同的半透明颜色
				Color observerColor = _viewColors[observerData.uuid % _viewColors.Length] * 0.5f;
				DrawSingleClient(observerId);
				colorIndex++;
			}
		}

		private bool ShouldShowName(ObserveeType type)
		{
			return (_nameDisplayMask & (1 << (int)type)) != 0;
		}

		private void DrawObservees(ObserverData data)
		{
			float currentTime = Time.time;

			foreach (var observeeData in data.observees.Values)
			{
				float timeSinceUpdate = currentTime - observeeData.lastUpdateTime;
				Color timeBasedColor = GetTimeBasedColor(timeSinceUpdate);
				Gizmos.color = timeBasedColor;
				
				// 绘制实体
				switch (observeeData.type)
				{
					case ObserveeType.StaticActor:
						ReplicationGraphVisualizerUtils.DrawStaticActor(observeeData.position, timeBasedColor);
						break;
					case ObserveeType.DynamicActor:
						ReplicationGraphVisualizerUtils.DrawDynamicActor(observeeData.position, timeBasedColor);
						break;
					case ObserveeType.PlayerCharacter:
						ReplicationGraphVisualizerUtils.DrawPlayerCharacter(observeeData.position, timeBasedColor);
						break;
				}

				// 使用SmartLabel管理标签
				_smartLabel.Clear();

				// 添加名字
				if (ShouldShowName(observeeData.type))
				{
					_smartLabel.Add(observeeData.name, timeBasedColor);
				}

				// 添加更新时间
				if (_showUpdateTime)
				{
					_smartLabel.Add($"{timeSinceUpdate:F1}s", timeBasedColor);
				}

				// 绘制所有标签
				_smartLabel.Draw(observeeData.position);
			}
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
					DrawSingleClient(ReplicationGraphVisualizer.MODE_SERVER);
					break;
				case ObserverMode.SingleClient:
					DrawSingleClient(_targetObserverId);
					break;
				case ObserverMode.AllClients:
					DrawAllClients();
					break;
			}
		}

		private List<string> _cachedObservers = null;

		private void RefreshCachedObservers()
		{ 
			_cachedObservers = null;
		}

		public List<string> GetObserversExceptServer()
		{
			if (_cachedObservers == null)
			{
				_cachedObservers = _observerRegistry?
				.Keys
				.Where(id => id != ReplicationGraphVisualizer.MODE_SERVER)
				.OrderBy(id => id)
				.ToList();
			}
			return _cachedObservers;
		}

		private void OnGUI()
		{
			if (!_onguiEnable) { return; }
			if (!Application.isPlaying) return;

			// 右上角图例
			if (_showLegend)
			{
				DrawLegend();
			}

			// 左上角视角切换面板
			DrawViewControls();
		}

		private void DrawLegend()
		{
			int padding = 10;
			int width = 200;

			// 创建一个背景框
			GUI.Box(new Rect(Screen.width - width - padding, padding, width, 200), "图例");

			GUILayout.BeginArea(new Rect(Screen.width - width - padding + 5, padding + 20, width - 10, 180));

			GUI.skin.label.richText = true;  // 启用富文本

			// 显示不同类型的图标说明
			GUILayout.Label("实体类型:");
			GUILayout.Label("□ 静态物体 (空心方块)");
			GUILayout.Label("■ 动态物体 (实心方块)");
			GUILayout.Label("● 玩家角色 (实心圆)");

			GUILayout.Space(10);

			// 显示时效性说明
			GUILayout.Label("时效性颜色:");
			GUILayout.Label($"<color={ColorToHex(_recentDataColor)}>■</color> 最新数据 (<{_recentDataThreshold}s)");
			GUILayout.Label($"<color={ColorToHex(_normalDataColor)}>■</color> 正常数据 (<{_staleDataThreshold}s)");
			GUILayout.Label($"<color={ColorToHex(_staleDataColor)}>■</color> 过期数据");

			GUILayout.EndArea();
		}

		private Dictionary<Color, string> _colorToHex = new Dictionary<Color, string>();

		private string ColorToHex(Color color)
		{
			if (!_colorToHex.TryGetValue(color, out var hex))
			{
				hex = $"#{ColorUtility.ToHtmlStringRGBA(color)}";
				_colorToHex[color] = hex;
			}
			return hex;
		}

		private void DrawViewControls()
		{
			int padding = 10;
			int width = 200;
			int buttonHeight = 30;

			// 创建一个背景框
			GUI.Box(new Rect(padding, padding, width, 120), "视角切换");

			GUILayout.BeginArea(new Rect(padding + 5, padding + 25, width - 10, 90));

			// 第一行按钮
			GUILayout.BeginHorizontal();

			// 服务器视角按钮
			Color originalColor = GUI.backgroundColor;
			GUI.backgroundColor = _currentMode == ObserverMode.Server ?
				new Color(0.7f, 0.3f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);

			if (GUILayout.Button("服务器视角", GUILayout.Height(buttonHeight)))
			{
				ReplicationGraphVisualizer.SwitchObserver(ReplicationGraphVisualizer.MODE_SERVER);
			}

			// 所有客户端按钮
			GUI.backgroundColor = _currentMode == ObserverMode.AllClients ?
				new Color(0.3f, 0.7f, 0.7f) : new Color(0.3f, 0.3f, 0.3f);

			if (GUILayout.Button("所有客户端", GUILayout.Height(buttonHeight)))
			{
				ReplicationGraphVisualizer.SwitchObserver(ReplicationGraphVisualizer.MODE_ALL_CLIENTS);
			}
			GUILayout.EndHorizontal();

			// 第二行按钮
			GUILayout.BeginHorizontal();
			var observers = GetObserversExceptServer();
			foreach (var observer in observers)
			{
				GUI.backgroundColor = (_currentMode == ObserverMode.SingleClient && _targetObserverId == observer) ?
					new Color(0.3f, 0.7f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);

				if (GUILayout.Button(observer, GUILayout.Height(buttonHeight)))
				{
					ReplicationGraphVisualizer.SwitchObserver(observer);
				}
			}
			GUILayout.EndHorizontal();

			// 恢复原始背景色
			GUI.backgroundColor = originalColor;

			GUILayout.EndArea();
		}

	
	}
}