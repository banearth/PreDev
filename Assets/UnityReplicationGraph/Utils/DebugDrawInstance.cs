using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

[Serializable]
public struct DebugLine
{
    public Vector3 a;
    public Vector3 b;
    public Color color;
    public float duration;
    public float timestamp;
}

[Serializable]
public struct DebugSphere
{
    public Vector3 center;
    public float radius;
    public Color color;
    public float duration;
    public float timestamp;
    public bool isSolid;
}

[Serializable]
public struct DebugBox
{
    public Vector3 center;
    public Vector3 size;
    public Color color;
    public float duration;
    public float timestamp;
    public bool isSolid;
}

[Serializable]
public struct DebugLabel
{
    public Vector3 center;
    public string label;
    public Color color;
    public float duration;
    public float timestamp;
}

[Serializable]
public struct DebugBezier
{
    public Vector3 a;
    public Vector3 b;
    public Vector3 ctrl;
    public Color color;
    public float duration;
    public float timestamp;
}

[Serializable]
public struct DebugCapsule
{
    public Vector3 center;
    public float radius;
    public float height;
    public Color color;
    public float duration;
    public float timestamp;
}

[Serializable]
public struct DebugArc
{
    public Vector3 center;
    public Vector3 direction;
    public Vector3 normal;
    public float radius;
    public float angle;
    public Color color;
    public float duration;
    public float timestamp;
}

[Serializable]
public struct DebugDisc
{
    public Vector3 center;
    public Vector3 normal;
    public float radius;
    public Color color;
    public float duration;
    public float timestamp;
    public bool isSolid;
}

public class DebugDrawInstance : MonoBehaviour
{
    public List<DebugLine> lines = new List<DebugLine>();
	public List<DebugLine> arrows = new List<DebugLine>();
	public List<DebugSphere> spheres = new List<DebugSphere>();
	public List<DebugBox> boxes = new List<DebugBox>();
	public List<DebugLabel> labels = new List<DebugLabel>();
	public List<DebugBezier> beziers = new List<DebugBezier>();
	public List<DebugCapsule> capsules = new List<DebugCapsule>();
	public List<DebugArc> arcs = new List<DebugArc>();
	public List<DebugDisc> discs = new List<DebugDisc>();
    public bool useRealtimeClock = true;

    private void Awake()

    {
        CPL.DebugDraw.Instance = this;
    }

    private float GetAdjustedTime()
    {
        if (useRealtimeClock)
        {
            return Time.realtimeSinceStartup;

        }
        else
        {
            return Time.time;
        }
    }


    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        float currentTime = GetAdjustedTime();
        
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if ((currentTime - lines[i].timestamp) > lines[i].duration)
            {
                lines.RemoveAt(i);
            }
        }
        
        for (int i = arrows.Count - 1; i >= 0; i--)
        {
            if ((currentTime - arrows[i].timestamp) > arrows[i].duration)
            {
                arrows.RemoveAt(i);
            }
        }
        
        for (int i = spheres.Count - 1; i >= 0; i--)
        {
            if ((currentTime - spheres[i].timestamp) > spheres[i].duration)
            {
                spheres.RemoveAt(i);
            }
        }
        
        for (int i = boxes.Count - 1; i >= 0; i--)
        {
            if ((currentTime - boxes[i].timestamp) > boxes[i].duration)
            {
                boxes.RemoveAt(i);
            }
        }

        for (int i = labels.Count - 1; i >= 0; i--)
        {
            if ((currentTime - labels[i].timestamp) > labels[i].duration)
            {
                labels.RemoveAt(i);
            }
        }
        
        for (int i = beziers.Count - 1; i >= 0; i--)
        {
            if ((currentTime - beziers[i].timestamp) > beziers[i].duration)
            {
                beziers.RemoveAt(i);
            }
        }
        
        for (int i = capsules.Count - 1; i >= 0; i--)
        {
            if ((currentTime - capsules[i].timestamp) > capsules[i].duration)
            {
                capsules.RemoveAt(i);
            }
        }
        
        for (int i = arcs.Count - 1; i >= 0; i--)
        {
            if ((currentTime - arcs[i].timestamp) > arcs[i].duration)
            {
                arcs.RemoveAt(i);
            }
        }
        
        for (int i = discs.Count - 1; i >= 0; i--)
        {
            if ((currentTime - discs[i].timestamp) > discs[i].duration)
            {
                discs.RemoveAt(i);
            }
        }

        foreach (var line in lines)
        {
            Gizmos.color = line.color;
            Gizmos.DrawLine(line.a, line.b);
        }
        
        foreach (var arrow in arrows)
        {
            Gizmos.color = arrow.color;
            float arrowSize = 0.1f;
            Vector3 direction = (arrow.b - arrow.a).normalized;
            Vector3 cross = Vector3.Cross(direction, Vector3.up);
            Gizmos.DrawLine(arrow.a, arrow.b);
            Gizmos.DrawLine(arrow.b, arrow.b - direction * arrowSize + cross * arrowSize);
            Gizmos.DrawLine(arrow.b, arrow.b - direction * arrowSize - cross * arrowSize);
        }
        
        foreach (var sphere in spheres)
        {
            Gizmos.color = sphere.color;
            if (sphere.isSolid)
            {
                Gizmos.DrawSphere(sphere.center, sphere.radius);
            }
            else
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
        
        foreach (var box in boxes)
        {
            Gizmos.color = box.color;
            if (box.isSolid)
            {
                Gizmos.DrawCube(box.center, box.size);
            }
            else
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
        
        foreach (var label in labels)
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = label.color;
            Handles.Label(label.center, label.label,style);
        }
        
        foreach (var bezier in beziers)
        {
            _DrawBezier(in bezier);
        }
        
        foreach (var capsule in capsules)
        {
            _DrawCapsure(in capsule);
        }
        
        foreach (var arc in arcs)
        {
            _DrawArc(in arc);
        }
        
        foreach (var disc in discs)
        {
            _DrawDisc(in disc);
        }
#endif
    }

    private void _DrawBezier(in DebugBezier bezier)
    {
#if UNITY_EDITOR
        Vector3 ctrlPos = bezier.ctrl;
        int segment = 30;
        Handles.color = bezier.color;
        var thickness = 3;
        for (int i = 0; i < segment; i++)
        {
            if (i + 1 <= segment)
            {
                float current = (1.0f / 30.0f) * i;
                float next = (1.0f / 30.0f) * (i + 1);
                Vector3 vecPosA = bezier.a + (ctrlPos - bezier.a) * current;
                Vector3 vecPosB = ctrlPos + (bezier.b - ctrlPos) * current;
                Vector3 vecSegStart = vecPosA + (vecPosB - vecPosA) * current;

                vecPosA = bezier.a + (ctrlPos - bezier.a) * next;
                vecPosB = ctrlPos + (bezier.b - ctrlPos) * next;
                Vector3 vecSegEnd = vecPosA + (vecPosB - vecPosA) * next;
                
                Handles.DrawLine(vecSegStart, vecSegEnd, thickness);
            }
        }
#endif
    }

    private void _DrawCapsure(in DebugCapsule capsule)
    {
#if UNITY_EDITOR
		Gizmos.color = capsule.color;
		Vector3 topSphereCenter = capsule.center + Vector3.up * (capsule.height / 2 - capsule.radius);
		Vector3 bottomSphereCenter = capsule.center - Vector3.up * (capsule.height / 2 - capsule.radius);

		Gizmos.DrawWireSphere(topSphereCenter, capsule.radius);
		Gizmos.DrawWireSphere(bottomSphereCenter, capsule.radius);

		Gizmos.DrawLine(topSphereCenter + Vector3.left * capsule.radius, bottomSphereCenter + Vector3.left * capsule.radius);
		Gizmos.DrawLine(topSphereCenter + Vector3.right * capsule.radius, bottomSphereCenter + Vector3.right * capsule.radius);
		Gizmos.DrawLine(topSphereCenter + Vector3.forward * capsule.radius, bottomSphereCenter + Vector3.forward * capsule.radius);
		Gizmos.DrawLine(topSphereCenter + Vector3.back * capsule.radius, bottomSphereCenter + Vector3.back * capsule.radius);
#endif
	}

    private void _DrawArc(in DebugArc arc)
    {
#if UNITY_EDITOR
        var thickness = 3;
        UnityEditor.Handles.color = arc.color;
        Vector3 from = arc.direction.Rotate(arc.normal, -arc.angle * 0.5f);
        UnityEditor.Handles.DrawWireArc(arc.center, arc.normal, from, arc.angle, arc.radius, thickness);
#endif
    }

    private void _DrawDisc(in DebugDisc disc)
    {
#if UNITY_EDITOR
        var thickness = 3;
		UnityEditor.Handles.color = disc.color;
		if (disc.isSolid)
		{
			UnityEditor.Handles.DrawSolidDisc(disc.center, disc.normal, disc.radius);
		}
        else
        {
			UnityEditor.Handles.DrawWireDisc(disc.center, disc.normal, disc.radius, thickness);
		}
#endif
    }

	public void DrawArrow(Vector3 a, Vector3 b, Color color, float duration)
    {
#if UNITY_EDITOR
        DebugLine line = new DebugLine();
        line.a = a;
        line.b = b;
        line.color = color;
        line.duration = duration;
        line.timestamp = GetAdjustedTime();
        arrows.Add(line);
#endif
    }
    public void DrawLine(Vector3 a, Vector3 b, Color color, float duration)
    {
#if UNITY_EDITOR
        DebugLine line = new DebugLine();
        line.a = a;
        line.b = b;
        line.color = color;
        line.duration = duration;
        line.timestamp = GetAdjustedTime();
        lines.Add(line);
#endif
    }
    public void DrawSolidSphere(Vector3 center, float radius, Color color, float duration)
    {
#if UNITY_EDITOR
        DebugSphere sphere = new DebugSphere();
        sphere.center = center;
        sphere.radius = radius;
        sphere.color = color;
        sphere.duration = duration;
        sphere.timestamp = GetAdjustedTime();
        sphere.isSolid = true;
        spheres.Add(sphere);
#endif
    }
    public void DrawSphere(Vector3 center, float radius, Color color, float duration)
    {
#if UNITY_EDITOR
        DebugSphere sphere = new DebugSphere();
        sphere.center = center;
        sphere.radius = radius;
        sphere.color = color;
        sphere.duration = duration;
        sphere.timestamp = GetAdjustedTime();
        sphere.isSolid = false;
        spheres.Add(sphere);
#endif
    }
    public void DrawBox(Vector3 center, Vector3 size, Color color, float duration)
    {
#if UNITY_EDITOR
        DebugBox box = new DebugBox();
        box.center = center;
        box.size = size;
        box.color = color;
        box.duration = duration;
        box.timestamp = GetAdjustedTime();
        box.isSolid = false;
        boxes.Add(box);
#endif
    }
	public void DrawSolidBox(Vector3 center, Vector3 size, Color color, float duration)
	{
#if UNITY_EDITOR
		DebugBox box = new DebugBox();
		box.center = center;
		box.size = size;
		box.color = color;
		box.duration = duration;
		box.timestamp = GetAdjustedTime();
		box.isSolid = true;
		boxes.Add(box);
#endif
	}
	public void DrawLabel(Vector3 center, string label, Color color, float duration)
    {
#if UNITY_EDITOR
        DebugLabel box = new DebugLabel();
        box.center = center;
        box.label = label;
        box.color = color;
        box.duration = duration;
        box.timestamp = GetAdjustedTime();
        labels.Add(box);
#endif
    }
    public void DrawBezier(Vector3 a, Vector3 b, Vector3 ctrl, Color color, float duration)
    {
#if UNITY_EDITOR
        DebugBezier bezier = new DebugBezier();
        bezier.a = a;
        bezier.b = b;
        bezier.ctrl = ctrl;
        bezier.color = color;
        bezier.duration = duration;
        bezier.timestamp = GetAdjustedTime();
        beziers.Add(bezier);
#endif
    }

    public void DebugCapsule(Vector3 center, float radius, float height, Color color, float duration)
    {
#if UNITY_EDITOR
        DebugCapsule capsule = new DebugCapsule();
        capsule.center = center;
        capsule.radius = radius;
        capsule.height = height;
        capsule.color = color;
        capsule.duration = duration;
        capsule.timestamp = GetAdjustedTime();
        capsules.Add(capsule);
#endif
    }
    public void DrawArc(Vector3 center, Vector3 direction, Vector3 normal, float angle, float radius, Color color, float duration)
    {
#if UNITY_EDITOR
        DebugArc arc = new DebugArc();
        arc.center = center;
        arc.normal = normal;
        arc.radius = radius;
        arc.direction = direction;
        arc.angle = angle;
        arc.color = color;
        arc.duration = duration;
        arc.timestamp = GetAdjustedTime();
        arcs.Add(arc);
#endif
    }
    public void DrawDisc(Vector3 center, Vector3 normal, float radius, Color color, float duration)
    {
#if UNITY_EDITOR
        DebugDisc disc = new DebugDisc();
        disc.center = center;
        disc.normal = normal;
        disc.radius = radius;
        disc.color = color;
        disc.duration = duration;
        disc.timestamp = GetAdjustedTime();
		disc.isSolid = false;
		discs.Add(disc);
#endif
    }
    public void DrawSolidDisc(Vector3 center, Vector3 normal, float radius, Color color, float duration)
    {
#if UNITY_EDITOR
		DebugDisc disc = new DebugDisc();
		disc.center = center;
		disc.normal = normal;
		disc.radius = radius;
		disc.color = color;
		disc.duration = duration;
		disc.timestamp = GetAdjustedTime();
		disc.isSolid = true;
		discs.Add(disc);
#endif
	}
}

#if UNITY_EDITOR
[CustomEditor(typeof(DebugDrawInstance))]
public class DebugDrawInstanceEditor : Editor
{
    private bool drawDefault = false;
    private bool showLines = true;
    private bool showArrows = true;
    private bool showSpheres = true;
    private bool showBoxes = true;
    private bool showLabels = true;
    private bool showBeziers = true;
    private bool showCapsules = true;
    private bool showArcs = true;
    private bool showDiscs = true;

    public override void OnInspectorGUI()
    {
        drawDefault = EditorGUILayout.Foldout(drawDefault, "Default Inspector", true);
        if (drawDefault)
        {
            DrawDefaultInspector();
        }
        
        DebugDrawInstance instance = (DebugDrawInstance)target;
        
        EditorGUILayout.Space();
        
        // 新增全局聚焦按钮
        bool hasAnyElements = instance.lines.Count > 0 || instance.arrows.Count > 0 || 
                            instance.spheres.Count > 0 || instance.boxes.Count > 0 ||
                            instance.labels.Count > 0 || instance.beziers.Count > 0 ||
                            instance.capsules.Count > 0 || instance.arcs.Count > 0 || 
                            instance.discs.Count > 0;

        using (new EditorGUI.DisabledGroupScope(!hasAnyElements))
        {
            if (GUILayout.Button("Focus View All"))
            {
                Bounds totalBounds = new Bounds();
                bool initialized = false;

                // 合并所有元素的包围盒
                Action<Vector3, float> addPoint = (pos, size) => {
                    if (!initialized) {
                        totalBounds = new Bounds(pos, Vector3.one * size);
                        initialized = true;
                    } else {
                        totalBounds.Encapsulate(new Bounds(pos, Vector3.one * size));
                    }
                };

                // 处理所有类型元素
                foreach (var line in instance.lines) 
                    addPoint((line.a + line.b)/2f, Vector3.Distance(line.a, line.b));
                
                foreach (var arrow in instance.arrows)
                    addPoint((arrow.a + arrow.b)/2f, Vector3.Distance(arrow.a, arrow.b));
                
                foreach (var sphere in instance.spheres)
                    addPoint(sphere.center, sphere.radius * 2f);

                foreach (var box in instance.boxes)
                    addPoint(box.center, Mathf.Max(box.size.x, box.size.y, box.size.z));

                foreach (var label in instance.labels)
                    addPoint(label.center, 5f);

                foreach (var bezier in instance.beziers)
                    addPoint((bezier.a + bezier.b + bezier.ctrl)/3f, Mathf.Max(
                        Vector3.Distance(bezier.a, bezier.b),
                        Vector3.Distance(bezier.a, bezier.ctrl),
                        Vector3.Distance(bezier.b, bezier.ctrl)));

                foreach (var capsule in instance.capsules)
                    addPoint(capsule.center, Mathf.Max(capsule.radius * 2, capsule.height));

                foreach (var arc in instance.arcs)
                    addPoint(arc.center, arc.radius * 2f);

                foreach (var disc in instance.discs)
                    addPoint(disc.center, disc.radius * 2f);

                if (initialized) {
                    SceneView.lastActiveSceneView.Frame(totalBounds, false);
                    SceneView.lastActiveSceneView.Repaint();
                }
            }
        }
        
        if (instance.lines.Count > 0)
        {
            showLines = EditorGUILayout.Foldout(showLines, $"Lines ({instance.lines.Count})", true);
            if (showLines)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < instance.lines.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Line {i}");
                    if (GUILayout.Button("Focus", GUILayout.Width(60)))
                    {
                        Vector3 center = (instance.lines[i].a + instance.lines[i].b) * 0.5f;
                        float size = Vector3.Distance(instance.lines[i].a, instance.lines[i].b);
                        SceneView.lastActiveSceneView.Frame(new Bounds(center, Vector3.one * size));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        if (instance.arrows.Count > 0)
        {
            showArrows = EditorGUILayout.Foldout(showArrows, $"Arrows ({instance.arrows.Count})", true);
            if (showArrows)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < instance.arrows.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Arrow {i}");
                    if (GUILayout.Button("Focus", GUILayout.Width(60)))
                    {
                        Vector3 center = (instance.arrows[i].a + instance.arrows[i].b) * 0.5f;
                        float size = Vector3.Distance(instance.arrows[i].a, instance.arrows[i].b);
                        SceneView.lastActiveSceneView.Frame(new Bounds(center, Vector3.one * size));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        if (instance.spheres.Count > 0)
        {
            showSpheres = EditorGUILayout.Foldout(showSpheres, $"Spheres ({instance.spheres.Count})", true);
            if (showSpheres)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < instance.spheres.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Sphere {i}");
                    if (GUILayout.Button("Focus", GUILayout.Width(60)))
                    {
                        SceneView.lastActiveSceneView.Frame(
                            new Bounds(instance.spheres[i].center, 
                            Vector3.one * instance.spheres[i].radius * 2));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        if (instance.boxes.Count > 0)
        {
            showBoxes = EditorGUILayout.Foldout(showBoxes, $"Boxes ({instance.boxes.Count})", true);
            if (showBoxes)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < instance.boxes.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Box {i}");
                    if (GUILayout.Button("Focus", GUILayout.Width(60)))
                    {
                        SceneView.lastActiveSceneView.Frame(
                            new Bounds(instance.boxes[i].center, instance.boxes[i].size));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        if (instance.labels.Count > 0)
        {
            showLabels = EditorGUILayout.Foldout(showLabels, $"Labels ({instance.labels.Count})", true);
            if (showLabels)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < instance.labels.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Label {i}: {instance.labels[i].label}");
                    if (GUILayout.Button("Focus", GUILayout.Width(60)))
                    {
                        SceneView.lastActiveSceneView.Frame(
                            new Bounds(instance.labels[i].center, Vector3.one * 5f));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        if (instance.beziers.Count > 0)
        {
            showBeziers = EditorGUILayout.Foldout(showBeziers, $"Beziers ({instance.beziers.Count})", true);
            if (showBeziers)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < instance.beziers.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Bezier {i}");
                    if (GUILayout.Button("Focus", GUILayout.Width(60)))
                    {
                        Vector3 center = (instance.beziers[i].a + instance.beziers[i].b + instance.beziers[i].ctrl) / 3f;
                        float size = Mathf.Max(
                            Vector3.Distance(instance.beziers[i].a, instance.beziers[i].b),
                            Vector3.Distance(instance.beziers[i].a, instance.beziers[i].ctrl),
                            Vector3.Distance(instance.beziers[i].b, instance.beziers[i].ctrl));
                        SceneView.lastActiveSceneView.Frame(new Bounds(center, Vector3.one * size));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        if (instance.capsules.Count > 0)
        {
            showCapsules = EditorGUILayout.Foldout(showCapsules, $"Capsules ({instance.capsules.Count})", true);
            if (showCapsules)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < instance.capsules.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Capsule {i}");
                    if (GUILayout.Button("Focus", GUILayout.Width(60)))
                    {
                        SceneView.lastActiveSceneView.Frame(
                            new Bounds(instance.capsules[i].center, 
                            new Vector3(instance.capsules[i].radius * 2, instance.capsules[i].height, instance.capsules[i].radius * 2)));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        if (instance.arcs.Count > 0)
        {
            showArcs = EditorGUILayout.Foldout(showArcs, $"Arcs ({instance.arcs.Count})", true);
            if (showArcs)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < instance.arcs.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Arc {i}");
                    if (GUILayout.Button("Focus", GUILayout.Width(60)))
                    {
                        SceneView.lastActiveSceneView.Frame(
                            new Bounds(instance.arcs[i].center, 
                            Vector3.one * instance.arcs[i].radius * 2));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        if (instance.discs.Count > 0)
        {
            showDiscs = EditorGUILayout.Foldout(showDiscs, $"Discs ({instance.discs.Count})", true);
            if (showDiscs)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < instance.discs.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Disc {i}");
                    if (GUILayout.Button("Focus", GUILayout.Width(60)))
                    {
                        SceneView.lastActiveSceneView.Frame(
                            new Bounds(instance.discs[i].center, 
                            Vector3.one * instance.discs[i].radius * 2));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
    }
}
#endif
