using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using Object = UnityEngine.Object;

namespace DreadScripts.VRCSDKPlus
{
    internal static class VRCSDKPlusToolbox
    {
        #region Ready Paths
        internal static string ReadyAssetPath(string path, bool makeUnique = false, PathOption pathOption = PathOption.Normal)
        {
            bool forceFolder = pathOption == PathOption.ForceFolder;
            bool forceFile = pathOption == PathOption.ForceFile;

            path = forceFile ? LegalizeName(path) : forceFolder ? LegalizePath(path) : LegalizeFullPath(path);
            bool isFolder = forceFolder || (!forceFile && string.IsNullOrEmpty(Path.GetExtension(path)));

            if (isFolder)
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    AssetDatabase.ImportAsset(path);
                }
                else if (makeUnique)
                {
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                    Directory.CreateDirectory(path);
                    AssetDatabase.ImportAsset(path);
                }
            }
            else
            {
                const string basePath = "Assets";
                string folderPath = Path.GetDirectoryName(path);
                string fileName = Path.GetFileName(path);

                if (string.IsNullOrEmpty(folderPath))
                    folderPath = basePath;
                else if (!folderPath.StartsWith(Application.dataPath) && !folderPath.StartsWith(basePath))
                    folderPath = $"{basePath}/{folderPath}";

                if (folderPath != basePath && !Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    AssetDatabase.ImportAsset(folderPath);
                }

                path = $"{folderPath}/{fileName}";
                if (makeUnique)
                    path = AssetDatabase.GenerateUniqueAssetPath(path);

            }

            return path;
        }

        internal static string ReadyAssetPath(string folderPath, string fullNameOrExtension, bool makeUnique = false)
        {
            if (string.IsNullOrEmpty(fullNameOrExtension))
                return ReadyAssetPath(LegalizePath(folderPath), makeUnique, PathOption.ForceFolder);
            if (string.IsNullOrEmpty(folderPath))
                return ReadyAssetPath(LegalizeName(fullNameOrExtension), makeUnique, PathOption.ForceFile);

            return ReadyAssetPath($"{LegalizePath(folderPath)}/{LegalizeName(fullNameOrExtension)}", makeUnique);
        }
        internal static string ReadyAssetPath(Object buddyAsset, string fullNameOrExtension = "", bool makeUnique = true)
        {
            string buddyPath = AssetDatabase.GetAssetPath(buddyAsset);
            string folderPath = Path.GetDirectoryName(buddyPath);
            if (string.IsNullOrEmpty(fullNameOrExtension))
                fullNameOrExtension = Path.GetFileName(buddyPath);
            if (fullNameOrExtension.StartsWith("."))
            {
                string assetName = string.IsNullOrWhiteSpace(buddyAsset.name) ? "SomeAsset" : buddyAsset.name;
                fullNameOrExtension = $"{assetName}{fullNameOrExtension}";
            }

            return ReadyAssetPath(folderPath, fullNameOrExtension, makeUnique);
        }

        internal static string LegalizeFullPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("Legalizing empty path! Returned path as 'EmptyPath'");
                return "EmptyPath";
            }

            string ext = Path.GetExtension(path);
            bool isFolder = string.IsNullOrEmpty(ext);
            if (isFolder) return LegalizePath(path);

            string folderPath = Path.GetDirectoryName(path);
            string fileName = LegalizeName(Path.GetFileNameWithoutExtension(path));

            if (string.IsNullOrEmpty(folderPath)) return $"{fileName}{ext}";
            folderPath = LegalizePath(folderPath);

            return $"{folderPath}/{fileName}{ext}";
        }
        internal static string LegalizePath(string path)
        {
            string regexFolderReplace = Regex.Escape(new string(Path.GetInvalidPathChars()));

            path = path.Replace('\\', '/');
            if (path.IndexOf('/') > 0)
                path = string.Join("/", path.Split('/').Select(s => Regex.Replace(s, $@"[{regexFolderReplace}]", "-")));

            return path;

        }
        internal static string LegalizeName(string name)
        {
            string regexFileReplace = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            return string.IsNullOrEmpty(name) ? "Unnamed" : Regex.Replace(name, $@"[{regexFileReplace}]", "-");
        }
        #endregion

        internal static bool TryGetActiveIndex(this ReorderableList orderList, out int index)
        {
            index = orderList.index;
            if (index < orderList.count && index >= 0) return true;
            index = -1;
            return false;
        }
        public static string GenerateUniqueString(string s, Func<string, bool> PassCondition, bool addNumberIfMissing = true)
        {
            if (PassCondition(s)) return s;
            Match match = Regex.Match(s, @"(?=.*)(\d+)$");
            if (!match.Success && !addNumberIfMissing) return s;
            string numberString = match.Success ? match.Groups[1].Value : "1";
            if (!match.Success && !s.EndsWith(" ")) s += " ";
            string newString = Regex.Replace(s, @"(?=.*?)\d+$", string.Empty);
            while (!PassCondition($"{newString}{numberString}"))
                numberString = (int.Parse(numberString) + 1).ToString(new string('0', numberString.Length));

            return $"{newString}{numberString}";
        }

        public static Color BrightnessToColor(float brightness)
        {
            if (brightness > 1) brightness /= 255;
            return new Color(brightness, brightness, brightness, 1);
        }

        private static readonly Texture2D tempTexture = new Texture2D(1, 1) { anisoLevel = 0, filterMode = FilterMode.Point };
        internal static Texture2D GetColorTexture(float rgb, float a = 1)
        {
            return GetColorTexture(rgb, rgb, rgb, a);
        }

        internal static Texture2D GetColorTexture(float r, float g, float b, float a = 1)
        {
            if (r > 1) r /= 255;
            if (g > 1) g /= 255;
            if (b > 1) b /= 255;
            if (a > 1) a /= 255;

            return GetColorTexture(new Color(r, g, b, a));
        }
        internal static Texture2D GetColorTexture(Color color)
        {
            tempTexture.SetPixel(0, 0, color);
            tempTexture.Apply();
            return tempTexture;
        }

        // ReSharper disable once InconsistentNaming
        public static VRCExpressionsMenu.Control.ControlType ToControlType(this SerializedProperty property)
        {
            int value = property.enumValueIndex;
            switch (value)
            {
                case 0:
                    return VRCExpressionsMenu.Control.ControlType.Button;
                case 1:
                    return VRCExpressionsMenu.Control.ControlType.Toggle;
                case 2:
                    return VRCExpressionsMenu.Control.ControlType.SubMenu;
                case 3:
                    return VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
                case 4:
                    return VRCExpressionsMenu.Control.ControlType.FourAxisPuppet;
                case 5:
                    return VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            }

            return VRCExpressionsMenu.Control.ControlType.Button;
        }

        public static int GetEnumValueIndex(this VRCExpressionsMenu.Control.ControlType type)
        {
            switch (type)
            {
                case VRCExpressionsMenu.Control.ControlType.Button:
                    return 0;
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    return 1;
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    return 2;
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    return 3;
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    return 4;
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    return 5;
                default:
                    return -1;
            }
        }

        public static int FindIndex(this IEnumerable array, object target)
        {
            IEnumerator enumerator = array.GetEnumerator();
            int index = 0;
            while (enumerator.MoveNext())
            {
                if (enumerator.Current != null && enumerator.Current.Equals(target))
                    return index;
                index++;
            }

            return -1;
        }
        internal static bool GetPlayableLayer(this VRCAvatarDescriptor avi, VRCAvatarDescriptor.AnimLayerType type, out AnimatorController controller)
        {
            controller = (from l in avi.baseAnimationLayers.Concat(avi.specialAnimationLayers) where l.type == type select l.animatorController).FirstOrDefault() as AnimatorController;
            return controller != null;
        }

        internal static bool IterateArray(this SerializedProperty property, Func<int, SerializedProperty, bool> func, params int[] skipIndex)
        {
            for (int i = property.arraySize - 1; i >= 0; i--)
            {
                if (skipIndex.Contains(i)) continue;
                if (i >= property.arraySize) continue;
                if (func(i, property.GetArrayElementAtIndex(i)))
                    return true;
            }
            return false;
        }

        #region Keyboard Commands
        internal static bool HasReceivedCommand(EventCommands command, string matchFocusControl = "", bool useEvent = true)
        {
            if (!string.IsNullOrEmpty(matchFocusControl) && GUI.GetNameOfFocusedControl() != matchFocusControl) return false;
            Event e = Event.current;
            if (e.type != EventType.ValidateCommand) return false;
            bool received = command.ToString() == e.commandName;
            if (received && useEvent) e.Use();
            return received;
        }

        internal static bool HasReceivedKey(KeyCode key, string matchFocusControl = "", bool useEvent = true)
        {
            if (!string.IsNullOrEmpty(matchFocusControl) && GUI.GetNameOfFocusedControl() != matchFocusControl) return false;
            Event e = Event.current;
            bool received = e.type == EventType.KeyDown && e.keyCode == key;
            if (received && useEvent) e.Use();
            return received;
        }

        internal static bool HasReceivedEnter(string matchFocusControl = "", bool useEvent = true)
        {
            return HasReceivedKey(KeyCode.Return, matchFocusControl, useEvent) || HasReceivedKey(KeyCode.KeypadEnter, matchFocusControl, useEvent);
        }

        internal static bool HasReceivedCancel(string matchFocusControl = "", bool useEvent = true)
        {
            return HasReceivedKey(KeyCode.Escape, matchFocusControl, useEvent);
        }

        internal static bool HasReceivedAnyDelete(string matchFocusControl = "", bool useEvent = true)
        {
            return HasReceivedCommand(EventCommands.SoftDelete, matchFocusControl, useEvent) || HasReceivedCommand(EventCommands.Delete, matchFocusControl, useEvent) || HasReceivedKey(KeyCode.Delete, matchFocusControl, useEvent);
        }

        internal static bool HandleConfirmEvents(string matchFocusControl = "", Action onConfirm = null, Action onCancel = null)
        {
            if (HasReceivedEnter(matchFocusControl))
            {
                onConfirm?.Invoke();
                return true;
            }

            if (HasReceivedCancel(matchFocusControl))
            {
                onCancel?.Invoke();
                return true;
            }
            return false;
        }

        internal static bool HandleTextFocusConfirmCommands(string matchFocusControl, Action onConfirm = null, Action onCancel = null)
        {
            if (!HandleConfirmEvents(matchFocusControl, onConfirm, onCancel)) return false;
            GUI.FocusControl(null);
            return true;
        }
        #endregion
    }
}