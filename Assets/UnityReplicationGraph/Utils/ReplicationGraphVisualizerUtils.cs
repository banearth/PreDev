using UnityEngine;
using System.Collections.Generic;

namespace ReplicationGraph
{
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
			Gizmos.DrawCube(position, Vector3.one * 0.5f);
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

		// 标签
		private static Dictionary<Color, GUIStyle> _cachedLabelStyle = new Dictionary<Color, GUIStyle>();

		// 文字
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

		// 绘制空心圆
		public static void DrawWireCircle(Vector3 position, float radius, Color? color = null)
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

		// 绘制实心圆
		public static void DrawSolidCircle(Vector3 position, float radius, Color? color = null)
		{
#if UNITY_EDITOR
			Color oldColor = UnityEditor.Handles.color;
			if (color != null)
			{
				UnityEditor.Handles.color = color.Value;
			}
			UnityEditor.Handles.DrawSolidDisc(position, Vector3.up, radius);
			UnityEditor.Handles.color = oldColor;
#endif
		}



	}
}