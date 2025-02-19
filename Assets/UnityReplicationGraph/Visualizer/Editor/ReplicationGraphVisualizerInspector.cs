using UnityEngine;
using UnityEditor;
using System.Linq;

namespace ReplicationGraphVisualizer
{
	[CustomEditor(typeof(VisualizerInstance))]
	public class ReplicationGraphVisualizerInspector : Editor
	{
		private static readonly string[] _nameDisplayOptions = new[]
		{
			"静态物体",
			"动态物体",
			"玩家角色"
		};

		public override void OnInspectorGUI()
		{
			var instance = (VisualizerInstance)target;
			
			// 开始隐藏指定属性
			serializedObject.Update();
			SerializedProperty iterator = serializedObject.GetIterator();
			bool enterChildren = true;
			while (iterator.NextVisible(enterChildren))
			{
				enterChildren = false;
				// 跳过 _nameDisplayMask 属性的显示
				if (iterator.name == "_nameDisplayMask") continue;
				EditorGUILayout.PropertyField(iterator, true);
			}
			serializedObject.ApplyModifiedProperties();

			// 添加名字显示控制
			EditorGUILayout.Space(10);
			EditorGUILayout.LabelField("名字显示控制", EditorStyles.boldLabel);

			var nameDisplayMaskProp = serializedObject.FindProperty("_nameDisplayMask");
			EditorGUI.BeginChangeCheck();
			
			// 使用 LayerMask 风格的多选框
			int maskValue = nameDisplayMaskProp.intValue;
			maskValue = EditorGUILayout.MaskField("显示名字", maskValue, _nameDisplayOptions);
			
			if (EditorGUI.EndChangeCheck())
			{
				nameDisplayMaskProp.intValue = maskValue;
				serializedObject.ApplyModifiedProperties();
			}

			EditorGUILayout.Space(10);
			EditorGUILayout.LabelField("视角切换", EditorStyles.boldLabel);

			// 获取当前模式
			var currentMode = instance.GetType().GetField("_currentMode",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(instance);
			var targetId = instance.GetType().GetField("_targetObserverId",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(instance) as string;

			// 服务器和全局视角按钮
			EditorGUILayout.BeginHorizontal();

			GUI.backgroundColor = (int)currentMode == (int)VisualizerInstance.ObserverMode.Server ?
				new Color(0.7f, 0.3f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);

			if (GUILayout.Button("服务器视角", GUILayout.Height(30)))
			{
				Visualizer.SwitchObserver(Visualizer.MODE_SERVER);
			}

			GUI.backgroundColor = (int)currentMode == (int)VisualizerInstance.ObserverMode.AllClients ?
				new Color(0.3f, 0.7f, 0.7f) : new Color(0.3f, 0.3f, 0.3f);

			if (GUILayout.Button("所有客户端", GUILayout.Height(30)))
			{
				Visualizer.SwitchObserver(Visualizer.MODE_ALL_CLIENTS);
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(5);

			// 获取所有客户端观察者
			var observers = instance.GetObserversExceptServer();
			if (observers != null && observers.Any())
			{
				for (int i = 0; i < observers.Count; i += 2)
				{
					EditorGUILayout.BeginHorizontal();

					// 第一个按钮
					GUI.backgroundColor = ((int)currentMode == (int)VisualizerInstance.ObserverMode.SingleClient
						&& targetId == observers[i]) ? new Color(0.3f, 0.7f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);

					if (GUILayout.Button(observers[i], GUILayout.Height(30)))
					{
						Visualizer.SwitchObserver(observers[i]);
					}

					// 第二个按钮（如果存在）
					if (i + 1 < observers.Count)
					{
						GUI.backgroundColor = ((int)currentMode == (int)VisualizerInstance.ObserverMode.SingleClient
							&& targetId == observers[i + 1]) ? new Color(0.3f, 0.7f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);

						if (GUILayout.Button(observers[i + 1], GUILayout.Height(30)))
						{
							Visualizer.SwitchObserver(observers[i + 1]);
						}
					}

					EditorGUILayout.EndHorizontal();
				}
			}

			// 恢复原始背景色
			GUI.backgroundColor = Color.white;
		}
	}

}