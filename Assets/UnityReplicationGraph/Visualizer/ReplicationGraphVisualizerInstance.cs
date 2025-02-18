using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;
using System;


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
		[SerializeField] private float _newDataThreshold = 1f;     			// 新数据阈值（秒）
		[SerializeField] private float _laggingDataThreshold = 5f;      	// 滞后数据阈值（秒）
		[SerializeField] private Color _newDataColor = Color.green; 		// 新数据颜色
		[SerializeField] private Color _laggingDataColor = Color.yellow; 	// 滞后数据颜色
		[SerializeField] private Color _expiredDataColor = Color.gray;   	// 过期数据颜色

		[Header("调试显示")]
		[SerializeField] private bool _onguiEnable = true;
		[SerializeField] private float _smartLabelOffsetMultiple = 1;// 智能Label整体偏移倍数
		[SerializeField] private float _smartLabelBaseOffset = 0.5f; // 智能Label基础偏移
		[SerializeField] private bool _showUpdateTime = true;    	// 是否显示更新时间
		[SerializeField] private bool _showLegend = true;        	// 是否显示图例

		[SerializeField] private int _nameDisplayMask = -1;      	// 默认全部显示

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

		// 添加滚动视图相关变量
		private Vector2 _clientButtonsScrollPos = Vector2.zero;  // 左上角客户端按钮的滚动位置
		private Vector2 _observerInfoScrollPos = Vector2.zero;   // 底部Observer信息的滚动位置

		[Header("网格可视化")]
		[SerializeField] private bool _showGridGizmos = true;  // 控制网格显示开关
		private Vector2 _gridInfoScrollPos;   // 网格信息的滚动位置
		private float _cellSize;
		private Vector2 _spatialBias;
		private int[] _gridSize;
		private bool _hasGridSetup;
		private Dictionary<int,int> _gridIndex2ActorCount = new Dictionary<int, int>(); // 网格上存在Actor的格子所对应Actor数量

		private void Awake()
		{
			ReplicationGraphVisualizer.SetupInstance(this);
			_smartLabel = new SmartLabel(_smartLabelOffsetMultiple, _smartLabelBaseOffset);
		}

		private void Update()
		{
			if (!Application.isPlaying || !_hasGridSetup) return;

			// 检测 Ctrl + 左键点击
			if (Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftControl))
			{
				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
				if (new Plane(Vector3.up, 0).Raycast(ray, out float enter))
				{
					Vector3 hitPoint = ray.GetPoint(enter);
					int gridX = Mathf.FloorToInt((hitPoint.x - _spatialBias.x) / _cellSize);
					int gridZ = Mathf.FloorToInt((hitPoint.z - _spatialBias.y) / _cellSize);

					// 只要在有效的网格范围内就可以点击
					if (gridX >= 0 && gridX < _gridSize.Length && gridZ >= 0 && gridZ < _gridSize[gridX])
					{
						int index = gridX * _gridSize.Max() + gridZ;
						int actorCount = 0;
						_gridIndex2ActorCount.TryGetValue(index, out actorCount); // 即使没有actor也允许点击
						
						Debug.Log($"Clicked grid ({gridX}, {gridZ}) with {actorCount} actors");
						ReplicationGraphVisualizer.TriggerGridClicked(gridX, gridZ);
					}
				}
			}
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
					observerData.observees[observeeId] = localData;
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
		private void DrawGizmos_SingleObserver(string observerId)
		{
			if (_observerRegistry.TryGetValue(observerId, out var observerData))
			{
				bool isServer = observerId == ReplicationGraphVisualizer.MODE_SERVER;

				// 绘制被观察者（不带十字标记）
				DrawObservees(observerData);

				// 只绘制客户端的
				if (!isServer)
				{
					ReplicationGraphVisualizerUtils.DrawObserver(observerData.position, observerData.viewRadius, _viewColors[0], _borderColor);
				}
			}
		}

		// 绘制所有客户端视角
		private void DrawGizmos_AllObservers()
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
				DrawGizmos_SingleObserver(observerId);
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
			if (timeSinceUpdate <= _newDataThreshold)
			{
				// 最新数据
				return _newDataColor;
			}
			else if (timeSinceUpdate <= _laggingDataThreshold)
			{
				// 正常数据，根据时间插值
				float t = (timeSinceUpdate - _newDataThreshold) /
						 (_laggingDataThreshold - _newDataThreshold);
				return Color.Lerp(_laggingDataColor, _expiredDataColor, t);
			}
			else
			{
				// 过期数据
				return _expiredDataColor;
			}
		}

		// 根据当前模式选择绘制方式
		private void OnDrawGizmos()
		{
			if (!Application.isPlaying) return;

			if (_hasGridSetup && _showGridGizmos)
			{
				ReplicationGraphVisualizerUtils.DrawGrid2D(
					_spatialBias,
					_cellSize,
					_gridSize,
					_gridIndex2ActorCount
				);
			}

			switch (_currentMode)
			{
				case ObserverMode.Server:
					DrawGizmos_SingleObserver(ReplicationGraphVisualizer.MODE_SERVER);
					break;
				case ObserverMode.SingleClient:
					DrawGizmos_SingleObserver(_targetObserverId);
					break;
				case ObserverMode.AllClients:
					DrawGizmos_AllObservers();
					break;
				default:
					Debug.LogError($"Invalid observer mode: {_currentMode}");
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
			if (!_onguiEnable || !Application.isPlaying) return;

			// 绘制图例和控制面板
			if (_showLegend) DrawLegend();
			DrawViewControls();

			// 根据当前模式绘制观察关系
			switch (_currentMode)
			{
				case ObserverMode.Server:
					// 服务器视角不绘制
					break;
				case ObserverMode.AllClients:
					DrawGUI_AllObservers();
					break;
				case ObserverMode.SingleClient:
					DrawGUI_SingleObserver(_targetObserverId);
					break;
				default:
					Debug.LogError($"Invalid observer mode: {_currentMode}");
					break;
			}

			// 在原有内容之后添加网格设置面板
			if (_hasGridSetup)
			{
				float panelWidth = 170;  // 与视角切换面板相同宽度
				float panelHeight = 150;
				float padding = 10;
				
				// 放在视角切换面板正下方
				Rect panelRect = new Rect(
					padding, 
					padding + 130,  // 视角切换面板的高度
					panelWidth, 
					panelHeight
				);
				
				GUI.Box(panelRect, "网格可视化设置");
				
				GUILayout.BeginArea(new Rect(
					panelRect.x + 5, 
					panelRect.y + 20, 
					panelRect.width - 10, 
					panelRect.height - 25
				));
				{
					_gridInfoScrollPos = GUILayout.BeginScrollView(_gridInfoScrollPos);
					
					_showGridGizmos = GUILayout.Toggle(_showGridGizmos, "显示网格");
					
					GUILayout.Space(5);
					GUILayout.Label($"网格大小: {_cellSize}");
					GUILayout.Label($"偏移: ({_spatialBias.x:F1}, {_spatialBias.y:F1})");
					if (_gridSize != null)
					{
						GUILayout.Label($"列数: {_gridSize.Length}");
						GUILayout.Label($"最大行数: {(_gridSize.Length > 0 ? _gridSize.Max() : 0)}");
					}
					
					GUILayout.EndScrollView();
				}
				GUILayout.EndArea();
			}
		}

		private void DrawGUI_AllObservers()
		{
			var observers = GetObserversExceptServer();

			int padding = 10;
			int width = 200;
			int height = 150;
			int totalWidth = Screen.width - (padding * 2);

			// 使用滚动视图
			_observerInfoScrollPos = GUI.BeginScrollView(
				new Rect(padding, Screen.height - height - padding, totalWidth, height),
				_observerInfoScrollPos,
				new Rect(0, 0, observers.Count * (width + 20), height - 20)
			);
			
			int index = 0;
			foreach (var observerId in observers)
			{
				if (_observerRegistry.TryGetValue(observerId, out var observer))
				{
					DrawGUI_ObserverInfo(observerId, observer, index);
					index++;
				}
			}
			
			GUI.EndScrollView();
		}

		#region DistanceReferObject

		private class DistanceReferObject
		{
			public float distance;
			public object referObject;
			public T GetObjectAsType<T>() => (T)referObject;
		}

		private class DistanceReferObjectComparer : IComparer<DistanceReferObject>
		{
			public int Compare(DistanceReferObject x, DistanceReferObject y)
			{
				return x.distance.CompareTo(y.distance);
			}
		}

		private List<DistanceReferObject> _sortedDistanceReferObjectList = new List<DistanceReferObject>();
		private int _sortedDistanceReferObjectCount = 0;
		private DistanceReferObjectComparer _distanceReferObjectComparer = new DistanceReferObjectComparer();

		private void AddDistanceReferObject(float distance, object referObject)
		{
			_sortedDistanceReferObjectCount++;
			DistanceReferObject curDistanceReferObject = null;
			if (_sortedDistanceReferObjectList.Count >= _sortedDistanceReferObjectCount)
			{
				curDistanceReferObject = _sortedDistanceReferObjectList[_sortedDistanceReferObjectCount - 1];
			}
			else
			{
				curDistanceReferObject = new DistanceReferObject();
				_sortedDistanceReferObjectList.Add(curDistanceReferObject);
			}
			curDistanceReferObject.distance = distance;
			curDistanceReferObject.referObject = referObject;
		}

		private void ClearDistanceReferObjectList()
		{
			_sortedDistanceReferObjectCount = 0;
		}

		private void SortDistanceReferObjectList()
		{
			_sortedDistanceReferObjectList.Sort(0, _sortedDistanceReferObjectCount, _distanceReferObjectComparer);
		}

		#endregion

		private void DrawGUI_ObserverInfo(string observerId, ObserverData observer, int index)
		{
			int width = 200;
			int height = 150;
			int spacing = 20;
			int xPos = (width + spacing) * index;
			
			// 绘制窗口（注意x坐标现在是相对于滚动视图的）
			GUI.Box(new Rect(xPos, 0, width, height - 20), 
				$"Observer {observerId}");

			GUILayout.BeginArea(new Rect(xPos + 5, 25, width - 10, height - 50));

			// 计算所有被观察者到观察者的距离
			ClearDistanceReferObjectList();
			foreach (var observee in observer.observees.Values)
			{
				AddDistanceReferObject(Vector3.Distance(observer.position, observee.position), observee);
			}
			SortDistanceReferObjectList();

			for(int i = 0;i<_sortedDistanceReferObjectCount;i++)
			{
				var distanceReferObject = _sortedDistanceReferObjectList[i];
				var observee = distanceReferObject.GetObjectAsType<ObserveeData>();
				float timeSinceUpdate = Time.time - observee.lastUpdateTime;
				Color timeBasedColor = GetTimeBasedColor(timeSinceUpdate);
				string colorHex = ColorToHex(timeBasedColor);
				string typeText = GetTypeDisplayText(observee.type);

				// 显示名称、类型和距离
				GUILayout.Label($"<color={colorHex}>{observee.name} ({typeText}) - {distanceReferObject.distance:F1}m</color>");

				if (_showUpdateTime)
				{
					GUILayout.Label($"<color={colorHex}>  {timeSinceUpdate:F1}s</color>");
				}
			}

			GUILayout.EndArea();
		}

		private string GetTypeDisplayText(ObserveeType type)
		{
			return type switch
			{
				ObserveeType.StaticActor => "静态",
				ObserveeType.DynamicActor => "动态",
				ObserveeType.PlayerCharacter => "玩家",
				_ => "未知"
			};
		}

		private void DrawGUI_SingleObserver(string observerId)
		{
			// 使用相同的滚动视图
			int padding = 10;
			int width = 200;
			int height = 150;
			int totalWidth = Screen.width - (padding * 2);
			int observerCount = 1;
			_observerInfoScrollPos = GUI.BeginScrollView(
				new Rect(padding, Screen.height - height - padding, totalWidth, height),
				_observerInfoScrollPos,
				new Rect(0, 0, observerCount * (width + 20), height - 20)
			);
			if (_observerRegistry.TryGetValue(observerId, out var observer))
			{
				DrawGUI_ObserverInfo(observerId, observer, 0);
			}
			GUI.EndScrollView();
		}

		private void DrawLegend()
		{
			int padding = 10;
			int width = 150;
			int height = 240;

			// 创建一个背景框
			GUI.Box(new Rect(Screen.width - width - padding, padding, width, height), "图例");

			GUILayout.BeginArea(new Rect(Screen.width - width - padding + 5, padding + 25, width - 10, height - 30));

			GUI.skin.label.richText = true;  // 启用富文本

			// 显示不同类型的图标说明
			GUILayout.Label("实体类型:");
			GUILayout.Label("□ 静态物体 (空心方块)");
			GUILayout.Label("■ 动态物体 (实心方块)");
			GUILayout.Label("● 玩家角色 (实心圆)");

			GUILayout.Space(10);

			// 显示时效性说明
			GUILayout.Label("时效性颜色:");
			GUILayout.Label($"<color={ColorToHex(_newDataColor)}>■</color> 最新数据 (<{_newDataThreshold}s)");
			GUILayout.Label($"<color={ColorToHex(_laggingDataColor)}>■</color> 滞后数据 (<{_laggingDataThreshold}s)");
			GUILayout.Label($"<color={ColorToHex(_expiredDataColor)}>■</color> 过期数据");

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
			int width = 170;
			int height = 120;
			
			// 创建一个背景框
			GUI.Box(new Rect(padding, padding, width, height), "视角切换");
			
			GUILayout.BeginArea(new Rect(padding + 5, padding + 25, width - 10, height - 30));
			
			// 第一行固定按钮
			GUILayout.BeginHorizontal();
			DrawServerButton();
			DrawAllClientsButton();
			GUILayout.EndHorizontal();
			
			// 客户端按钮使用滚动视图
			_clientButtonsScrollPos = GUILayout.BeginScrollView(_clientButtonsScrollPos, 
				GUILayout.Height(60));  // 固定滚动区域高度
			
			var observers = GetObserversExceptServer();
			int buttonsPerRow = 2;  // 每行显示的按钮数
			
			for (int i = 0; i < observers.Count; i += buttonsPerRow)
			{
				GUILayout.BeginHorizontal();
				for (int j = 0; j < buttonsPerRow && (i + j) < observers.Count; j++)
				{
					string observer = observers[i + j];
					DrawClientButton(observer);
				}
				GUILayout.EndHorizontal();
			}
			
			GUILayout.EndScrollView();
			GUILayout.EndArea();
		}

		// 辅助方法：绘制服务器按钮
		private void DrawServerButton()
		{
			GUI.backgroundColor = _currentMode == ObserverMode.Server ?
				new Color(0.7f, 0.3f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);
			
			if (GUILayout.Button("服务器视角", GUILayout.Height(30)))
			{
				ReplicationGraphVisualizer.SwitchObserver(ReplicationGraphVisualizer.MODE_SERVER);
			}
		}

		// 辅助方法：绘制所有客户端按钮
		private void DrawAllClientsButton()
		{
			GUI.backgroundColor = _currentMode == ObserverMode.AllClients ?
				new Color(0.3f, 0.7f, 0.7f) : new Color(0.3f, 0.3f, 0.3f);
			
			if (GUILayout.Button("所有客户端", GUILayout.Height(30)))
			{
				ReplicationGraphVisualizer.SwitchObserver(ReplicationGraphVisualizer.MODE_ALL_CLIENTS);
			}
		}

		// 辅助方法：绘制单个客户端按钮
		private void DrawClientButton(string observer)
		{
			GUI.backgroundColor = (_currentMode == ObserverMode.SingleClient && _targetObserverId == observer) ?
				new Color(0.3f, 0.7f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);
			
			if (GUILayout.Button(observer, GUILayout.Height(25)))
			{
				ReplicationGraphVisualizer.SwitchObserver(observer);
			}
		}

		// 获取当前观察者ID
		internal string GetCurObserver_Internal()
		{
			switch (_currentMode)
			{
				case ObserverMode.Server:
					return ReplicationGraphVisualizer.MODE_SERVER;
				case ObserverMode.AllClients:
					return ReplicationGraphVisualizer.MODE_ALL_CLIENTS;
				case ObserverMode.SingleClient:
					return _targetObserverId;
				default:
					return string.Empty;
			}
		}

		internal void RemoveObservee_Internal(string observerId, string observeeId)
		{
			// 检查观察者是否存在
			if (!_observerRegistry.TryGetValue(observerId, out var observer))
			{
				return;
			}

			// 从观察者的可见列表中移除被观察者
			if (observer.observees.ContainsKey(observeeId))
			{
				observer.observees.Remove(observeeId);
			}

			// 如果是全局被观察者，不从_globalObservees中移除
			// 因为其他观察者可能还需要看到它
			// 只有在RemoveGlobalObservee时才真正移除全局被观察者
		}

		internal void SetupGrid2D_Internal(float cellSize, float spatialBiasX, float spatialBiasY, int[] grids, int[] gridActorIndexes, int[] gridActorCounts)
		{
			_hasGridSetup = true;
			_cellSize = cellSize;
			_spatialBias = new Vector2(spatialBiasX, spatialBiasY);
			_gridSize = grids;
			_gridIndex2ActorCount.Clear();
			for(int i = 0;i<gridActorIndexes.Length;i++)
			{
				_gridIndex2ActorCount[gridActorIndexes[i]] = gridActorCounts[i];
			}
		}

		public void ClearGrid2D_Internal()
		{
			_hasGridSetup = false;
			_cellSize = 0;
			_spatialBias = Vector2.zero;
			_gridSize = null;
			_gridIndex2ActorCount.Clear();
		}

	}
}