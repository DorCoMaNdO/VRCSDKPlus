using UnityEditor;

namespace DreadScripts.VRCSDKPlus
{
    public static class CustomGUIContent
    {
        public const string MissingParametersTooltip = "No Expression Parameters targeted. Auto-fill and warnings are disabled.";
        public const string MenuFullTooltip = "Menu's controls are already maxed out. (8/8)";
        public static readonly UnityEngine.GUIContent Copy
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconCopy))
            {
                tooltip = "Copy"
            };

        public static readonly UnityEngine.GUIContent Paste
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconPaste))
            {
                tooltip = "Paste"
            };

        public static readonly UnityEngine.GUIContent Move
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconMove))
            {
                tooltip = "Move"
            };
        public static readonly UnityEngine.GUIContent Place
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconPlace))
            {
                tooltip = "Place"
            };

        public static readonly UnityEngine.GUIContent Duplicate
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconDuplicate))
            {
                tooltip = "Duplicate"
            };

        public static readonly UnityEngine.GUIContent Help
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconHelp));

        public static readonly UnityEngine.GUIContent Warn
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconWarn));
        public static readonly UnityEngine.GUIContent Error
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconError));

        public static readonly UnityEngine.GUIContent Clear
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconClear))
            {
                tooltip = "Clear"
            };

        public static readonly UnityEngine.GUIContent Folder
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconFolder))
            {
                tooltip = "Open"
            };

        public static readonly UnityEngine.GUIContent Remove
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconRemove)) { tooltip = "Remove element from list" };

        public static readonly UnityEngine.GUIContent Search
            = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Strings.IconSearch)) { tooltip = "Search" };
    }
}