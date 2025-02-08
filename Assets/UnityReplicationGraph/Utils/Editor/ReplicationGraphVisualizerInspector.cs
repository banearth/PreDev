using UnityEngine;
using UnityEditor;
using System.Linq;

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
        
        // 服务器和全局视角按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("服务器视角", GUILayout.Height(30)))
        {
            ReplicationGraphVisualizer.SwitchObserver(ReplicationGraphVisualizer.MODE_SERVER);
        }
        
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
            // 每行显示2个按钮
            for (int i = 0; i < observers.Count; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                
                // 第一个按钮
                if (GUILayout.Button(observers[i], GUILayout.Height(30)))
                {
                    ReplicationGraphVisualizer.SwitchObserver(observers[i]);
                }
                
                // 第二个按钮（如果存在）
                if (i + 1 < observers.Count)
                {
                    if (GUILayout.Button(observers[i + 1], GUILayout.Height(30)))
                    {
                        ReplicationGraphVisualizer.SwitchObserver(observers[i + 1]);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
    }
} 