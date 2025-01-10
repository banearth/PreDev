using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public struct DebugLine
{
    public Vector3 a;
    public Vector3 b;
    public Color color;
    public float duration;
    public float timestamp;
}

public struct DebugSphere
{
    public Vector3 center;
    public float radius;
    public Color color;
    public float duration;
    public float timestamp;
    public bool isSolid;
}
public struct DebugBox
{
    public Vector3 center;
    public Vector3 size;
    public Color color;
    public float duration;
    public float timestamp;
}
public struct DebugLabel
{
    public Vector3 center;
    public string label;
    public Color color;
    public float duration;
    public float timestamp;
}
public struct DebugBezier
{
    public Vector3 a;
    public Vector3 b;
    public Vector3 ctrl;
    public Color color;
    public float duration;
    public float timestamp;
}

public struct DebugCapsule
{
    public Vector3 center;
    public float radius;
    public float height;
    public Color color;
    public float duration;
    public float timestamp;
}

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
    private List<DebugLine> lines = new List<DebugLine>();
    private List<DebugLine> arrows = new List<DebugLine>();
    private List<DebugSphere> spheres = new List<DebugSphere>();
    private List<DebugBox> boxes = new List<DebugBox>();
    private List<DebugLabel> labels = new List<DebugLabel>();
    private List<DebugBezier> beziers = new List<DebugBezier>();
    private List<DebugCapsule> capsules = new List<DebugCapsule>();
    private List<DebugArc> arcs = new List<DebugArc>();
    private List<DebugDisc> discs = new List<DebugDisc>();
    private void Awake()
    {
        CPL.DebugDraw.Instance = this;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        int last = lines.Count - 1;
        for (int i = last; i >= 0; i--)
        {
            Gizmos.color = lines[i].color;
            Gizmos.DrawLine(lines[i].a, lines[i].b);
            if((Time.realtimeSinceStartup - lines[i].timestamp) >= lines[i].duration)
            {
                lines.RemoveAt(i);
            }
        }
        last = arrows.Count - 1;
        for (int i = last; i >= 0; i--)
        {
            Gizmos.color = arrows[i].color;
            float arrowSize = 0.1f;
            Vector3 direction = (arrows[i].b - arrows[i].a).normalized;
            Vector3 cross = Vector3.Cross(direction, Vector3.up);
            Gizmos.DrawLine(arrows[i].a, arrows[i].b);
            Gizmos.DrawLine(arrows[i].b, arrows[i].b - direction * arrowSize + cross * arrowSize);
            Gizmos.DrawLine(arrows[i].b, arrows[i].b - direction * arrowSize - cross * arrowSize);
            if ((Time.realtimeSinceStartup - arrows[i].timestamp) >= arrows[i].duration)
            {
                arrows.RemoveAt(i);
            }
        }
        last = spheres.Count - 1;
        for (int i = last; i >= 0; i--)
        {
            Gizmos.color = spheres[i].color;
            if (spheres[i].isSolid)
            {
                Gizmos.DrawSphere(spheres[i].center, spheres[i].radius);
            }
            else
            {
                Gizmos.DrawWireSphere(spheres[i].center, spheres[i].radius);
            }
            if ((Time.realtimeSinceStartup - spheres[i].timestamp) >= spheres[i].duration)
            {
                spheres.RemoveAt(i);
            }
        }
        last = boxes.Count - 1;
        for (int i = last; i >= 0; i--)
        {
            Gizmos.color = boxes[i].color;
            Gizmos.DrawWireCube(boxes[i].center, boxes[i].size);
            if ((Time.realtimeSinceStartup - boxes[i].timestamp) >= boxes[i].duration)
            {
                boxes.RemoveAt(i);
            }
        }
        last = labels.Count - 1;
        for (int i = last; i >= 0; i--)
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = labels[i].color;
            Handles.Label(labels[i].center, labels[i].label,style);
            if ((Time.realtimeSinceStartup - labels[i].timestamp) >= labels[i].duration)
            {
                labels.RemoveAt(i);
            }
        }

        last = beziers.Count - 1;
        for (int i = last; i >= 0; i--)
        {
            var bezier = beziers[i];
            _DrawBezier(ref bezier);
            if ((Time.realtimeSinceStartup - beziers[i].timestamp) >= beziers[i].duration)
            {
                beziers.RemoveAt(i);
            }
        }

        last = capsules.Count - 1;
        for (int i = last; i >= 0; i--)
        {
            var _capsule = capsules[i];
            Gizmos.color = _capsule.color;
            Vector3 topSphereCenter = _capsule.center + Vector3.up * (_capsule.height / 2 - _capsule.radius);
            Vector3 bottomSphereCenter = _capsule.center - Vector3.up * (_capsule.height / 2 - _capsule.radius);

            Gizmos.DrawWireSphere(topSphereCenter, _capsule.radius);
            Gizmos.DrawWireSphere(bottomSphereCenter, _capsule.radius);

            Gizmos.DrawLine(topSphereCenter + Vector3.left * _capsule.radius, bottomSphereCenter + Vector3.left * _capsule.radius);
            Gizmos.DrawLine(topSphereCenter + Vector3.right * _capsule.radius, bottomSphereCenter + Vector3.right * _capsule.radius);
            Gizmos.DrawLine(topSphereCenter + Vector3.forward * _capsule.radius, bottomSphereCenter + Vector3.forward * _capsule.radius);
            Gizmos.DrawLine(topSphereCenter + Vector3.back * _capsule.radius, bottomSphereCenter + Vector3.back * _capsule.radius);

            if ((Time.realtimeSinceStartup - _capsule.timestamp) >= _capsule.duration)
            {
                capsules.RemoveAt(i);
            }
        }

        last = arcs.Count - 1;
        for (int i = last; i >= 0; i--)
        {
            var arc = arcs[i];
            _DrawArc(ref arc);
            if ((Time.realtimeSinceStartup - arcs[i].timestamp) >= arcs[i].duration)
            {
                arcs.RemoveAt(i);
            }
        }

        last = discs.Count - 1;
        for (int i = last; i >= 0; i--)
        {
            var disc = discs[i];
            _DrawDisc(ref disc);
            if ((Time.realtimeSinceStartup - discs[i].timestamp) >= discs[i].duration)
            {
                discs.RemoveAt(i);
            }
        }

#endif
    }

    private void _DrawBezier(ref DebugBezier bezier)
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

    public void _DrawArc(ref DebugArc arc)
    {
#if UNITY_EDITOR
        var thickness = 3;
        UnityEditor.Handles.color = arc.color;
        Vector3 from = arc.direction.Rotate(arc.normal, -arc.angle * 0.5f);
        UnityEditor.Handles.DrawWireArc(arc.center, arc.normal, from, arc.angle, arc.radius, thickness);
#endif
    }

    public void _DrawDisc(ref DebugDisc disc)
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
        line.timestamp = Time.realtimeSinceStartup;
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
        line.timestamp = Time.realtimeSinceStartup;
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
        sphere.timestamp = Time.realtimeSinceStartup;
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
        sphere.timestamp = Time.realtimeSinceStartup;
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
        box.timestamp = Time.realtimeSinceStartup;
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
        box.timestamp = Time.realtimeSinceStartup;
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
        bezier.timestamp = Time.realtimeSinceStartup;
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
        capsule.timestamp = Time.realtimeSinceStartup;
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
        arc.timestamp = Time.realtimeSinceStartup;
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
        disc.timestamp = Time.realtimeSinceStartup;
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
		disc.timestamp = Time.realtimeSinceStartup;
		disc.isSolid = true;
		discs.Add(disc);
#endif
	}
}
