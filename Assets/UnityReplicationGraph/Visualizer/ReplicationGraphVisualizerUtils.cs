using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;

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

		// 绘制观察者
		public static void DrawObserver(Vector3 position, float viewRadius, Color? viewColor = null, Color? borderColor = null)
		{
#if UNITY_EDITOR
			// 中心十字
			Color oldGizmosColor = Gizmos.color;
			if (borderColor != null)
			{
				Gizmos.color = borderColor.Value;
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
#endif
		}

		private class LabelContent
		{
			public string text;
			public Color color;
		}

		// 准备标签内容
		private static List<LabelContent> _labelContents = new List<LabelContent>();
		private static int _labelContentUseCount = 0;

		private static void ClearLabelContent()
		{
			_labelContentUseCount = 0;
		}



	}
}