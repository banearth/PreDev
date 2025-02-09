using UnityEngine;
using UnityEditor;
using System.Linq;

namespace ReplicationGraph
{
	[CustomEditor(typeof(ReplicationGraphVisualizerInstance))]
	public class ReplicationGraphVisualizerInspector : Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			var instance = (ReplicationGraphVisualizerInstance)target;
			if (instance == null) return;

			EditorGUILayout.Space(10);
			EditorGUILayout.LabelField("视角切换", EditorStyles.boldLabel);

			// 获取当前模式
			var currentMode = instance.GetType().GetField("_currentMode",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(instance);
			var targetId = instance.GetType().GetField("_targetObserverId",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(instance) as string;

			// 服务器和全局视角按钮
			EditorGUILayout.BeginHorizontal();

			GUI.backgroundColor = (int)currentMode == (int)ReplicationGraphVisualizerInstance.ObserverMode.Server ?
				new Color(0.7f, 0.3f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);

			if (GUILayout.Button("服务器视角", GUILayout.Height(30)))
			{
				ReplicationGraphVisualizer.SwitchObserver(ReplicationGraphVisualizer.MODE_SERVER);
			}

			GUI.backgroundColor = (int)currentMode == (int)ReplicationGraphVisualizerInstance.ObserverMode.AllClients ?
				new Color(0.3f, 0.7f, 0.7f) : new Color(0.3f, 0.3f, 0.3f);

			if (GUILayout.Button("所有客户端", GUILayout.Height(30)))
			{
				ReplicationGraphVisualizer.SwitchObserver(ReplicationGraphVisualizer.MODE_ALL_CLIENTS);
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(5);

			// 获取所有客户端观察者
			var observers = instance.GetObservers();
			if (observers != null && observers.Any())
			{
				for (int i = 0; i < observers.Count; i += 2)
				{
					EditorGUILayout.BeginHorizontal();

					// 第一个按钮
					GUI.backgroundColor = ((int)currentMode == (int)ReplicationGraphVisualizerInstance.ObserverMode.SingleClient
						&& targetId == observers[i]) ? new Color(0.3f, 0.7f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);

					if (GUILayout.Button(observers[i], GUILayout.Height(30)))
					{
						ReplicationGraphVisualizer.SwitchObserver(observers[i]);
					}

					// 第二个按钮（如果存在）
					if (i + 1 < observers.Count)
					{
						GUI.backgroundColor = ((int)currentMode == (int)ReplicationGraphVisualizerInstance.ObserverMode.SingleClient
							&& targetId == observers[i + 1]) ? new Color(0.3f, 0.7f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);

						if (GUILayout.Button(observers[i + 1], GUILayout.Height(30)))
						{
							ReplicationGraphVisualizer.SwitchObserver(observers[i + 1]);
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