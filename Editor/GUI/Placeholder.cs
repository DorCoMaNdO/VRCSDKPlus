using UnityEditor;
using UnityEngine;

namespace DreadScripts.VRCSDKPlus
{
    public static class Placeholder
    {
        public static void GUILayout(float height)
        {
            GUI(EditorGUILayout.GetControlRect(false, height));
        }

        public static void GUI(Rect rect)
        {
            GUI(rect, EditorGUIUtility.isProSkin ? 53 : 182);
        }

        private static void GUI(Rect rect, float color)
        {
            EditorGUI.DrawTextureTransparent(rect, VRCSDKPlusToolbox.GetColorTexture(color));
        }
    }
}