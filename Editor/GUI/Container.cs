using System;
using UnityEditor;
using UnityEngine;

namespace DreadScripts.VRCSDKPlus
{
    public static class Container
    {
        public class Vertical : IDisposable
        {
            public Vertical(params GUILayoutOption[] options)
            {
                EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);
            }

            public Vertical(string title, params GUILayoutOption[] options)
            {
                EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);

                EditorGUILayout.LabelField(title, Styles.Label.Centered);
            }

            public void Dispose()
            {
                EditorGUILayout.EndVertical();
            }
        }
        public class Horizontal : IDisposable
        {
            public Horizontal(params GUILayoutOption[] options)
            {
                EditorGUILayout.BeginHorizontal(GUI.skin.GetStyle("helpbox"), options);
            }

            public Horizontal(string title, params GUILayoutOption[] options)
            {
                EditorGUILayout.BeginHorizontal(GUI.skin.GetStyle("helpbox"), options);

                EditorGUILayout.LabelField(title, Styles.Label.Centered);
            }

            public void Dispose()
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        public static void BeginLayout(params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);
        }

        public static void BeginLayout(string title, params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);

            EditorGUILayout.LabelField(title, Styles.Label.Centered);
        }
        public static void EndLayout()
        {
            EditorGUILayout.EndVertical();
        }

        public static Rect GUIBox(float height)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            return GUIBox(ref rect);
        }

        public static Rect GUIBox(ref Rect rect)
        {
            GUI.Box(rect, "", GUI.skin.GetStyle("helpbox"));

            rect.x += 4;
            rect.width -= 8;
            rect.y += 3;
            rect.height -= 6;

            return rect;
        }
    }
}