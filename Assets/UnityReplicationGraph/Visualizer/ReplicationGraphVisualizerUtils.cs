using UnityEngine;
using System.Collections.Generic;

namespace ReplicationGraph
{

	/// <summary>
    /// 智能标签管理器
    /// 用于复用标签内容，避免频繁创建新对象
    /// </summary>
	public class SmartLabel
    {
        public class LabelContent
        {
            public string text;
            public Color color;
        }

        private List<LabelContent> _labelContents = new List<LabelContent>();
        private int _labelContentUseCount = 0;
        private float _offsetMultiple = 0.5f;     // 标签间距倍数
        private float _baseOffset = 0.5f;         // 初始偏移距离
        private Vector3 _direction = Vector3.forward;  // 标签排列方向

        public SmartLabel(float offsetMultiple = 0.5f, float baseOffset = 0.5f, Vector3 direction = default)
        {
            _offsetMultiple = offsetMultiple;
            _baseOffset = baseOffset;
            _direction = direction == default ? Vector3.forward : direction.normalized;
        }

        /// <summary>
        /// 设置标签排列方向
        /// </summary>
        public void SetDirection(Vector3 direction)
        {
            _direction = direction.normalized;
        }

        /// <summary>
        /// 设置标签偏移倍数
        /// </summary>
        public void SetOffsetMultiple(float offsetMultiple)
        {
            _offsetMultiple = offsetMultiple;
        }

        /// <summary>
        /// 设置初始偏移
        /// </summary>
        public void SetBaseOffset(float baseOffset)
        {
            _baseOffset = baseOffset;
        }

        /// <summary>
        /// 清空当前使用的标签内容
        /// </summary>
        public void Clear()
        {
            _labelContentUseCount = 0;
        }

        /// <summary>
        /// 添加标签内容
        /// </summary>
        public void Add(string text, Color color)
        {
            _labelContentUseCount++;
            LabelContent curLabelContent = null;
            
            if (_labelContents.Count >= _labelContentUseCount)
            {
                curLabelContent = _labelContents[_labelContentUseCount - 1];
            }
            else
            {
                curLabelContent = new LabelContent();
                _labelContents.Add(curLabelContent);
            }
            
            curLabelContent.text = text;
            curLabelContent.color = color;
        }

        /// <summary>
        /// 绘制标签
        /// </summary>
        public void Draw(Vector3 position)
        {
            if (_labelContentUseCount == 0) return;

            Vector3 basePosition = position + _direction * _baseOffset;
            for (int i = 0; i < _labelContentUseCount; i++)
            {
                var content = _labelContents[i];
                ReplicationGraphVisualizerUtils.DrawLabel(
                    basePosition + _direction * _offsetMultiple * i, 
                    content.text, 
                    content.color
                );
            }
        }
    }

	public static class ReplicationGraphVisualizerUtils
	{
		private static int Max(this int[] array)
		{
			int max = array[0];
			for (int i = 1; i < array.Length; i++)
			{
				if (array[i] > max) max = array[i];
			}
			return max;
		}

		private static bool Contains(this int[] array, int value)
		{
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] == value) return true;
			}
			return false;
		}

		// 静态物体用空心方块
		public static void DrawStaticActor(Vector3 position, Color? color = null)
		{
			Color oldColor = Gizmos.color;
			if (color != null)
			{
				Gizmos.color = color.Value;
			}
			Gizmos.DrawWireCube(position, Vector3.one * 0.5f);
			Gizmos.color = oldColor;
		}

		// 动态物体用实心方块
		public static void DrawDynamicActor(Vector3 position, Color? color = null)
		{
			Color oldColor = Gizmos.color;
			if (color != null)
			{
				Gizmos.color = color.Value;
			}
			Gizmos.DrawCube(position, Vector3.one * 0.6f);
			Gizmos.color = oldColor;
		}

		// 玩家用实心圆
		public static void DrawPlayerCharacter(Vector3 position, Color? color = null)
		{
#if UNITY_EDITOR
			Color oldColor = UnityEditor.Handles.color;
			if (color != null)
			{
				UnityEditor.Handles.color = color.Value;
			}
			UnityEditor.Handles.DrawSolidDisc(position, Vector3.up, 0.4f);
			UnityEditor.Handles.color = oldColor;
#endif
		}

		private static Dictionary<Color, GUIStyle> _cachedLabelStyle = new Dictionary<Color, GUIStyle>();

		public static void DrawLabel(Vector3 position, string text, Color? color = null)
		{
#if UNITY_EDITOR
			GUIStyle style = null;
			Color oldColor = UnityEditor.Handles.color;
			if (color != null)
			{
				if (!_cachedLabelStyle.TryGetValue(color.Value, out style))
				{
					style = new GUIStyle();
					style.normal.textColor = color.Value;
					_cachedLabelStyle[color.Value] = style;
				}
				UnityEditor.Handles.color = color.Value;
			}
			UnityEditor.Handles.Label(position, text, style);
			UnityEditor.Handles.color = oldColor;
#endif
		}

		public static void DrawCirclePath(Vector3 position, float radius, Color? color = null)
		{
#if UNITY_EDITOR
			Color oldColor = UnityEditor.Handles.color;
			if (color != null)
			{
				UnityEditor.Handles.color = color.Value;
			}
			UnityEditor.Handles.DrawWireDisc(position, Vector3.up, radius);
			UnityEditor.Handles.color = oldColor;
#endif
		}

		private static void DrawCross(Vector3 position, Color? color = null)
		{
#if UNITY_EDITOR
			Color oldGizmosColor = Gizmos.color;
			if (color != null)
			{
				Gizmos.color = color.Value;
			}
			float crossSize = 0.5f;
			Gizmos.DrawLine(
				position + Vector3.left * crossSize,
				position + Vector3.right * crossSize
			);
			Gizmos.DrawLine(
				position + Vector3.forward * crossSize,
				position + Vector3.back * crossSize
			);
			Gizmos.color = oldGizmosColor;
#endif
		}

		// 绘制观察者
		public static void DrawObserver(Vector3 position, float viewRadius, Color? viewColor = null, Color? borderColor = null)
		{
#if UNITY_EDITOR
			// 绘制半透明圆形
			var oldHandlesColor = UnityEditor.Handles.color;
			if (viewColor != null)
			{
				UnityEditor.Handles.color = viewColor.Value;
			}
			UnityEditor.Handles.DrawSolidDisc(position, Vector3.up, viewRadius);
			// 绘制边界线
			UnityEditor.Handles.DrawWireDisc(position, Vector3.up, viewRadius);
			UnityEditor.Handles.color = oldHandlesColor;
			// 中心十字
			DrawCross(position, borderColor);
#endif
		}

		/// <summary>
		/// 绘制空间化网格
		/// </summary>
		/// <param name="spatialBias">网格原点偏移</param>
		/// <param name="cellSize">单元格大小</param>
		/// <param name="grids">数组长度表示列数量，数组内容表示具体每列行数</param>
		/// <param name="gridActorIndexes">网格上存在Actor的格子所对应的索引</param>
		/// <param name="gridIndex2ActorCount">网格上存在Actor的格子所对应的Actor数量</param>
		public static void DrawGrid2D(Vector2 spatialBias, float cellSize, int[] grids, Dictionary<int,int> gridIndex2ActorCount)
		{
#if UNITY_EDITOR
			if (grids == null || grids.Length == 0) return;

			// 计算网格范围
			int maxRowCount = grids.Max();
			float minX = spatialBias.x;
			float maxX = grids.Length * cellSize + spatialBias.x;
			float minZ = spatialBias.y;
			float maxZ = maxRowCount * cellSize + spatialBias.y;

			// 保存当前颜色
			Color originalColor = Gizmos.color;
			
			// 绘制网格线
			Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
			
			// 垂直线（列）
			for (float x = minX; x <= maxX; x += cellSize)
			{
				Gizmos.DrawLine(
					new Vector3(x, 0, minZ),
					new Vector3(x, 0, maxZ)
				);
			}
			
			// 水平线（行）
			for (float z = minZ; z <= maxZ; z += cellSize)
			{
				Gizmos.DrawLine(
					new Vector3(minX, 0, z),
					new Vector3(maxX, 0, z)
				);
			}

			// 绘制格子编号
			GUIStyle style = new GUIStyle();
			style.normal.textColor = Color.white;
			style.alignment = TextAnchor.MiddleCenter;
			
			for (int x = 0; x < grids.Length; x++)
			{
				for (int z = 0; z < maxRowCount; z++)
				{
					Vector3 cellCenter = new Vector3(
						spatialBias.x + (x + 0.5f) * cellSize,
						0,
						spatialBias.y + (z + 0.5f) * cellSize
					);

					// 如果该格子在实际占用范围内
					// 显示格子坐标
					string coordText = $"({x},{z})";
					if (z < grids[x])
					{
						int index = x * maxRowCount + z;
						if (gridIndex2ActorCount.TryGetValue(index, out int actorCount))
						{
							// 如果存在Actor，显示为绿色
							Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
							coordText += $"({actorCount})";
						}
						else
						{
							// 如果不存在Actor，显示为红色	
							Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
						}
						Gizmos.DrawCube(cellCenter, new Vector3(cellSize * 0.9f, 0.1f, cellSize * 0.9f));
					}
					UnityEditor.Handles.Label(cellCenter, coordText, style);
				}
			}

			// 绘制边界
			Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
			Vector3 boundMin = new Vector3(minX, 0, minZ);
			Vector3 boundMax = new Vector3(maxX, 0, maxZ);
			Vector3 size = boundMax - boundMin;
			Vector3 center = boundMin + size * 0.5f;
			Gizmos.DrawWireCube(center, new Vector3(size.x, 0, size.z));

			// 恢复颜色
			Gizmos.color = originalColor;
#endif
		}


	}
}