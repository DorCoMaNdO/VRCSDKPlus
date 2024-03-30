using UnityEditor;
using UnityEngine;

namespace DreadScripts.VRCSDKPlus
{
    public static class Styles
    {
        public const float Padding = 3;

        public static class Label
        {
            internal static readonly GUIStyle Centered = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter
            };

            internal static readonly GUIStyle RichText = new GUIStyle(GUI.skin.label)
            {
                richText = true
            };


            internal static readonly GUIStyle Type = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                normal =
                {
                        textColor = EditorGUIUtility.isProSkin ? Color.gray : VRCSDKPlusToolbox.BrightnessToColor(91),
                },
                fontStyle = FontStyle.Italic,
            };

            internal static readonly GUIStyle PlaceHolder = new GUIStyle(Type)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                contentOffset = new Vector2(2.5f, 0)
            };

            internal static readonly GUIStyle faintLinkLabel = new GUIStyle(PlaceHolder)
            {
                name = "Toggle",
                hover =
                {
                    textColor = new Color(0.3f, 0.7f, 1)
                }
            };

            internal static readonly GUIStyle TypeFocused = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black,
                },
                fontStyle = FontStyle.Italic,
            };

            internal static readonly GUIStyle TypeLabel = new GUIStyle(PlaceHolder) { contentOffset = new Vector2(-2.5f, 0) };
            internal static readonly GUIStyle RightPlaceHolder = new GUIStyle(TypeLabel) { alignment = TextAnchor.MiddleRight };
            internal static readonly GUIStyle Watermark = new GUIStyle(PlaceHolder)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 10,
            };

            internal static readonly GUIStyle LabelDropdown = new GUIStyle(GUI.skin.GetStyle("DropDownButton"))
            {
                alignment = TextAnchor.MiddleLeft,
                contentOffset = new Vector2(2.5f, 0)
            };

            internal static readonly GUIStyle RemoveIcon = new GUIStyle(GUI.skin.GetStyle("RL FooterButton"));
        }

        internal static readonly GUIStyle icon = new GUIStyle(GUI.skin.label)
        {
            fixedWidth = 18,
            fixedHeight = 18
        };

        internal static readonly GUIStyle letterButton = new GUIStyle(GUI.skin.button)
        {
            padding = new RectOffset(),
            margin = new RectOffset(1, 1, 1, 1),
            richText = true
        };
    }
}