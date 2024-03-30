using UnityEditor;
using UnityEngine;

namespace DreadScripts.VRCSDKPlus
{
    internal abstract class CustomDropdownBase : PopupWindowContent
    {
        internal static readonly GUIStyle backgroundStyle = new GUIStyle()
        {
            hover = { background = VRCSDKPlusToolbox.GetColorTexture(new Color(0.3020f, 0.3020f, 0.3020f)) },
            active = { background = VRCSDKPlusToolbox.GetColorTexture(new Color(0.1725f, 0.3647f, 0.5294f)) }
        };

        internal static readonly GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
    }
}