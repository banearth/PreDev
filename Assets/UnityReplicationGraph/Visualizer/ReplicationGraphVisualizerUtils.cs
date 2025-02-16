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
		public static void DrawGrid2D(Vector2 spatialBias, float cellSize, Vector2Int gridSize, Rect? gridBounds = null)
		{
	#if UNITY_EDITOR
			// 计算网格范围
			float minX = gridBounds.HasValue ? gridBounds.Value.xMin : spatialBias.x;
			float maxX = gridBounds.HasValue ? gridBounds.Value.xMax : (gridSize.x * cellSize + spatialBias.x);
			float minZ = gridBounds.HasValue ? gridBounds.Value.yMin : spatialBias.y;
			float maxZ = gridBounds.HasValue ? gridBounds.Value.yMax : (gridSize.y * cellSize + spatialBias.y);

			// 保存当前颜色
			Color originalColor = Gizmos.color;
			
			// 绘制网格线
			Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
			
			// 垂直线
			for (float x = minX; x <= maxX; x += cellSize)
			{
				Gizmos.DrawLine(
					new Vector3(x, 0, minZ),
					new Vector3(x, 0, maxZ)
				);
			}
			
			// 水平线
			for (float z = minZ; z <= maxZ; z += cellSize)
			{
				Gizmos.DrawLine(
					new Vector3(minX, 0, z),
					new Vector3(maxX, 0, z)
				);
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