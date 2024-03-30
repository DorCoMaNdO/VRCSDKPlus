using UnityEditor;

namespace DreadScripts.VRCSDKPlus
{
    public static class Preferences
    {
        public static bool CompactMode
        {
            get
            {
                return EditorPrefs.GetBool(Strings.SettingsCompact, false);
            }

            set
            {
                EditorPrefs.SetBool(Strings.SettingsCompact, value);
            }
        }
    }
}