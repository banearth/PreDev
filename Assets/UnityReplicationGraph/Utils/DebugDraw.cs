using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
namespace CPL
{
    public static class DebugDraw
    {
        public static DebugDrawInstance Instance;
        public static void DrawArrow(Vector3 a, Vector3 b, Color color, float duration)
        {
#if UNITY_EDITOR
            if (Instance)
            {
                Instance.DrawArrow(a, b, color, duration);
            }
#endif
        }

        public static void DrawLine(Vector3 a, Vector3 b, Color color, float duration)
        {
#if UNITY_EDITOR
            if (Instance)
            {
                Instance.DrawLine(a, b, color, duration);
            }
#endif
        }
        public static void DrawSphere(Vector3 center, float radius, Color color, float duration)
        {
#if UNITY_EDITOR
            if (Instance)
            {
                Instance.DrawSphere(center, radius, color, duration);
            }
#endif
        }
        public static void DrawBox(Vector3 center, Vector3 size, Color color, float duration)
        {
#if UNITY_EDITOR
            if (Instance)
            {
                Instance.DrawBox(center, size, color, duration);
            }
#endif
        }
        public static void DrawLabel(Vector3 center, string label, Color color, float duration)
        {
#if UNITY_EDITOR
            if (Instance)
            {
                Instance.DrawLabel(center, label, color, duration);
            }
#endif
        }

        public static void DrawBezier(Vector3 a, Vector3 b, Vector3 ctrl, Color color, float duration)
        {
#if UNITY_EDITOR
            if (Instance)
            {
                Instance.DrawBezier(a, b, ctrl, color, duration);
            }
#endif
        }

        public static void DrawCapsule(Vector3 location, float radius, float height, Color color, float duration)
        {
#if UNITY_EDITOR
            if (Instance) 
            {
                Instance.DebugCapsule(location, radius, height, color, duration);
            }
#endif
        }

        public static void DrawArc(Vector3 center, Vector3 direction, Vector3 normal, float angle, float radius, Color color, float duration)
        {
#if UNITY_EDITOR
            if (Instance)
            {
                Instance.DrawArc(center, direction, normal, angle, radius, color, duration);
            }
#endif
        }
        public static void DrawDisc(Vector3 center, Vector3 normal, float radius, Color color, float duration)
        {
#if UNITY_EDITOR
            if (Instance)
            {
                Instance.DrawDisc(center, normal, radius, color, duration);
            }
#endif
        }

        public static void PauseEditor()
        {
#if UNITY_EDITOR
            EditorApplication.isPaused = true;
#endif
        }
        public static void ResumeEditor()
        {
#if UNITY_EDITOR
            EditorApplication.isPaused = true;
#endif
        }
    }
}

