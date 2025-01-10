using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using CPL;

//为UnityEngine提供的扩展方法
public static class UnityEngineObjectExtension
{
    public static bool IsNull(this UnityEngine.Object o)
    {
        return o == null;
    }
}

public static class UnityEngineVector3Extension
{
    public static Vector3 Rotate(this Vector3 vec1, Vector3 vec2,  float angle)
    {
        return Quaternion.AngleAxis(angle, vec2) * vec1;
    }
}

public static class UnityEngineVector4Extension
{
    public static Vector4 ToVector4(this Rect rect)
    {
        return new Vector4(rect.x, rect.y, rect.width, rect.height);
    }

    public static Rect ToRect(this Vector4 vec)
    {
        return new Rect(vec.x, vec.y, vec.z, vec.w);
    }

    public static Vector4 ToVector4(this RectOffset rectOffset)
    {
        return new Vector4(rectOffset.top, rectOffset.right, rectOffset.bottom, rectOffset.left);
    }

    public static RectOffset ToRectOffset(this Vector4 vec)
    {
        return new RectOffset((int)vec.w, (int)vec.y, (int)vec.x, (int)vec.z);
    }
}

public static class stringExtension
{
    public static string FirstToUpper(this string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }
        Span<char> a = stackalloc char[s.Length];
        s.AsSpan(1).CopyTo(a.Slice(1));
        a[0] = char.ToUpper(s[0]);
        return new string(a);
    }
}

public static class TransformExtension
{
    public static Transform Find(this Transform transform, string boneName, bool recursively)
    {
        if (transform.name == boneName)
        {
            return transform;
        }
        if (recursively)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var result = child.Find(boneName, recursively);
                if (result != null)
                {
                    return result;
                }
            }
        }
        return null;
    }
}

public static class UnityEventExtension
{
    public static bool HasConnected(this UnityEventBase unityEvent, UnityAction unityAction)
    {
        var eventCount = unityEvent.GetPersistentEventCount();
        for (int i = 0; i < eventCount; i++)
        {
            if (unityEvent.GetPersistentMethodName(i) == unityAction.Method.Name)
            {
                return true;
            }
        }
        return false;
    }
}

/*
public static class UnityEngineVector2Extension
{
    public static Vector2 Project(this Vector2 vec1, Vector2 vec2)
    {
        return vec1 * Vector2.Dot(vec1, vec2.normalized);
    }
}
*/