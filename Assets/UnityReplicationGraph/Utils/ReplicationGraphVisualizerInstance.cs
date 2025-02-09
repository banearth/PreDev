using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Linq;

namespace ReplicationGraph
{

	public interface IObserveePath
	{
		void OnDraw(Color color);
	}

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
			public Vector3 position;
			public Dictionary<string, ObserveeData> observees = new Dictionary<string, ObserveeData>();
		}

		private class ObserveeData
		{
			public string name;
			public Vector3 position;
			public ObserveeType type;
			public float lastUpdateTime;
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
		[SerializeField] private Color _serverColor = new Color(1, 0, 0, 0.3f);
		[SerializeField] private Color _clientColor = new Color(0, 1, 1, 0.3f);
		[SerializeField] private float _observationRadius = 15f;

		[Header("时效性可视化")]
		[SerializeField] private float _recentDataThreshold = 1f;     // 最新数据阈值（秒）
		[SerializeField] private float _staleDataThreshold = 5f;      // 过期数据阈值（秒）
		[SerializeField] private Color _recentDataColor = Color.green; // 新数据颜色
		[SerializeField] private Color _normalDataColor = Color.yellow; // 正常数据颜色
		[SerializeField] private Color _staleDataColor = Color.gray;   // 旧数据颜色

		[Header("调试显示")]
		[SerializeField] private float _smartLabelOffsetMultiple = 1;// 智能Label整体偏移倍数
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
		private Dictionary<ObserveeData, IObserveePath> _pathRegistry = new Dictionary<ObserveeData, IObserveePath>();

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

		#region Observee
		// 增加被观察者
		internal void AddObservee_Internal(
			string observerId,
			string observeeId,
			Vector3 position,
			string type)
		{
			if (!_observerRegistry.TryGetValue(observerId, out var data))
			{
				data = new ObserverData();
				_observerRegistry[observerId] = data;
			}

			data.observees[observeeId] = new ObserveeData
			{
				name = observeeId,
				position = position,
				type = StringToType(type),
				lastUpdateTime = Time.time,
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
			if (!data.observees.TryGetValue(observeeId, out var observee))
			{
				// 注意：由于是Update时发现的，我们默认其为动态对象类型
				observee = new ObserveeData
				{
					name = observeeId,
					type = ObserveeType.DynamicActor,
					position = position,
					lastUpdateTime = Time.time
				};
				data.observees[observeeId] = observee;
			}
			else
			{
				// 已存在则更新位置和时间
				observee.position = position;
				observee.lastUpdateTime = Time.time;
			}
		}

		// 移除被观察者
		internal void RemoveObservee_Internal(string observerId, string observeeId)
		{
			if (_observerRegistry.TryGetValue(observerId, out var data) &&
				data.observees.TryGetValue(observeeId,out var observeeData))
			{
				data.observees.Remove(observeeId);
				// 如果存在路径，那么删除
				if (_pathRegistry.ContainsKey(observeeData))
				{
					_pathRegistry.Remove(observeeData);
				}
			}
		}

		// 绑定被观察者路径展示
		internal void BindObserveePath(string observerId, string observeeId, IObserveePath observeePath)
		{

			if (_observerRegistry.TryGetValue(observerId, out var data) &&
					data.observees.TryGetValue(observeeId, out var observeeData))
			{
				_pathRegistry[observeeData] = observeePath;
			}
			else
			{
				Debug.LogWarning(string.Format("无法找到 observerId:{0} observeeId:{1}的被观察者", observerId, observeeId));
			}
		}

		#endregion

		#region Observer

		// 增加观察者
		internal void AddObserver_Internal(string observerId, Vector3 position)
		{
			if (!_observerRegistry.TryGetValue(observerId, out var data))
			{
				data = new ObserverData();
				_observerRegistry[observerId] = data;
			}
			data.position = position;
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
			data.position = position;
		}

		// 移除观察者
		internal void RemoveObserver_Internal(string observerId)
		{
			_observerRegistry.Remove(observerId);
		}

		#endregion

		// 设置观察模式
		internal void SetViewMode(string mode, string targetId = "")
		{
			_currentMode = StringToMode(mode);
			_targetObserverId = targetId;
		}

		// 绘制单个客户端视角
		private void DrawSingleClient(string observerId, Color? overrideColor = null)
		{
			if (_observerRegistry.TryGetValue(observerId, out var data))
			{
				// 使用传入的颜色或默认的客户端颜色
				bool isServer = observerId == ReplicationGraphVisualizer.MODE_SERVER;
				Color viewColor = overrideColor ?? (isServer ? _serverColor : _clientColor);

				// 只绘制当前观察者的十字标记和视野范围
				DrawObserver(data.position, isServer);

				// 绘制被观察者（不带十字标记）
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
			if (_showRadius)
			{
				string radiusInfo = $"R:{_observationRadius}m";
				Handles.Label(center + Vector3.up * 0, radiusInfo);
			}
#endif
		}

		private bool ShouldShowName(ObserveeType type)
		{
			return (_nameDisplayMask & (1 << (int)type)) != 0;
		}

		private void DrawSmartLabel(Vector3 position, List<LabelContent> contents)
		{
#if UNITY_EDITOR
			if (contents == null || contents.Count == 0) return;

			Vector3 basePosition = position + Vector3.forward * 0.5f;

			for (int i = 0;i<contents.Count;i++)
			{
				var content = contents[i];
				GUIStyle style = new GUIStyle();
				style.normal.textColor = content.color;
				// 根据累积的高度向上偏移，使用整体偏移倍数
				Vector3 labelPosition = basePosition + Vector3.back * _smartLabelOffsetMultiple * i;
				UnityEditor.Handles.Label(labelPosition, content.text, style);
			}
#endif
		}

		// 准备标签内容
		private List<LabelContent> labelContents = new List<LabelContent>();

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
						// 静态物体用空心方块
						Gizmos.DrawWireCube(observeeData.position, Vector3.one * 0.5f);
						break;
					case ObserveeType.DynamicActor:
						// 动态物体用实心方块
						Gizmos.DrawCube(observeeData.position, Vector3.one * 0.5f);
						break;
					case ObserveeType.PlayerCharacter:
#if UNITY_EDITOR
						UnityEditor.Handles.color = timeBasedColor;
						UnityEditor.Handles.DrawSolidDisc(observeeData.position, Vector3.up, 0.4f);
#endif
						break;
				}

#if UNITY_EDITOR
				// 准备标签内容
				labelContents.Clear();

				// 添加名字（如果启用）
				if (ShouldShowName(observeeData.type))
				{
					labelContents.Add(new LabelContent
					{
						text = observeeData.name,
						color = timeBasedColor,
					});
				}

				// 添加更新时间（如果启用）
				if (_showUpdateTime)
				{
					labelContents.Add(new LabelContent
					{
						text = $"{timeSinceUpdate:F1}s",
						color = timeBasedColor,
					});
				}

				// 绘制所有标签
				DrawSmartLabel(observeeData.position, labelContents);
#endif

				// 如果有预测路径绘制回调，则调用它
				if (_pathRegistry.TryGetValue(observeeData, out var pathData))
				{
					pathData.OnDraw(timeBasedColor);
				}
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

#if UNITY_EDITOR
		private void OnGUI()
		{
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

			GUILayout.Space(10);

			// 显示观察者说明
			GUILayout.Label("观察者标记:");
			GUILayout.Label($"<color={ColorToHex(_serverColor)}>+</color> 服务器 (无范围限制)");
			GUILayout.Label($"<color={ColorToHex(_clientColor)}>+</color> 客户端 (R={_observationRadius}m)");

			GUILayout.EndArea();
		}

		private string ColorToHex(Color color)
		{
			return $"#{ColorUtility.ToHtmlStringRGBA(color)}";
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
			var observers = GetObservers();
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

		// 在OnInspectorGUI中使用的按钮样式
		public GUIStyle GetSelectedButtonStyle()
		{
			GUIStyle style = new GUIStyle(GUI.skin.button);
			style.fontStyle = FontStyle.Bold;
			return style;
		}

		private void DrawObserver(Vector3 position, bool isServer = false)
		{
			// 如果是服务器观察者，直接返回，不绘制任何标记
			if (isServer)
			{
				return;
			}

			float crossSize = 0.5f;
			Color viewColor = new Color(_clientColor.r, _clientColor.g, _clientColor.b, 0.2f);
			Color borderColor = _clientColor;

			// 绘制观察者位置标记（十字线）
			Gizmos.color = borderColor;
			Gizmos.DrawLine(
				position + Vector3.left * crossSize,
				position + Vector3.right * crossSize
			);
			Gizmos.DrawLine(
				position + Vector3.forward * crossSize,
				position + Vector3.back * crossSize
			);

#if UNITY_EDITOR
			// 绘制半透明圆形
			UnityEditor.Handles.color = viewColor;
			UnityEditor.Handles.DrawSolidDisc(position, Vector3.up, _observationRadius);

			// 绘制边界线
			UnityEditor.Handles.color = borderColor;
			UnityEditor.Handles.DrawWireDisc(position, Vector3.up, _observationRadius);

			// 只在开启时显示半径信息
			if (_showRadius)
			{
				string radiusInfo = $"R:{_observationRadius}m";
				UnityEditor.Handles.Label(position + Vector3.up, radiusInfo);
			}
#endif
		}
#endif

	
	}
}