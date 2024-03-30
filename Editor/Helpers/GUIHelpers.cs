using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace DreadScripts.VRCSDKPlus
{
    internal static class GUIHelpers
    {
        internal static bool ClickableButton(string label, GUIStyle style = null, params GUILayoutOption[] options)
        {
            return ClickableButton(new GUIContent(label), style, options);
        }

        internal static bool ClickableButton(string label, params GUILayoutOption[] options)
        {
            return ClickableButton(new GUIContent(label), null, options);
        }

        internal static bool ClickableButton(GUIContent label, params GUILayoutOption[] options)
        {
            return ClickableButton(label, null, options);
        }

        internal static bool ClickableButton(GUIContent label, GUIStyle style = null, params GUILayoutOption[] options)
        {
            if (style == null)
                style = GUI.skin.button;
            bool clicked = GUILayout.Button(label, style, options);
            if (GUI.enabled) MakeRectLinkCursor();
            return clicked;
        }

        internal static bool DrawAdvancedAvatarFull(ref VRCAvatarDescriptor avatar, VRCAvatarDescriptor[] validAvatars, Action OnAvatarChanged = null, bool warnNonHumanoid = true, bool warnPrefab = true, bool warnDoubleFX = true, string label = "Avatar", string tooltip = "The Targeted VRCAvatar", Action ExtraGUI = null)
        {
            return DrawAdvancedAvatarField(ref avatar, validAvatars, OnAvatarChanged, label, tooltip, ExtraGUI) && DrawAdvancedAvatarWarning(avatar, warnNonHumanoid, warnPrefab, warnDoubleFX);
        }

        internal static bool MakeRectClickable(Rect rect = default)
        {
            if (rect == default) rect = GUILayoutUtility.GetLastRect();
            MakeRectLinkCursor(rect);
            Event e = Event.current;
            return e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
        }

        internal static void MakeRectLinkCursor(Rect rect = default)
        {
            if (!GUI.enabled) return;
            if (Event.current.type == EventType.Repaint)
            {
                if (rect == default) rect = GUILayoutUtility.GetLastRect();
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }
        }

        internal static void UnderlineLastRectOnHover(Color? color = null)
        {
            if (color == null) color = new Color(0.3f, 0.7f, 1);
            if (Event.current.type == EventType.Repaint)
            {
                Rect rect = GUILayoutUtility.GetLastRect();
                Vector2 mp = Event.current.mousePosition;
                if (rect.Contains(mp)) EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color.Value);
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }
        }

        internal static VRCAvatarDescriptor DrawAdvancedAvatarField(ref VRCAvatarDescriptor avatar, VRCAvatarDescriptor[] validAvatars, Action OnAvatarChanged = null, string label = "Avatar", string tooltip = "The Targeted VRCAvatar", Action ExtraGUI = null)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUIContent avatarContent = new GUIContent(label, tooltip);
                if (validAvatars == null || validAvatars.Length <= 0) EditorGUILayout.LabelField(avatarContent, new GUIContent("No Avatar Descriptors Found"));
                else
                {
                    using (EditorGUI.ChangeCheckScope change = new EditorGUI.ChangeCheckScope())
                    {
                        int dummy = EditorGUILayout.Popup(avatarContent, avatar ? Array.IndexOf(validAvatars, avatar) : -1, validAvatars.Where(a => a).Select(x => x.name).ToArray());
                        if (change.changed)
                        {
                            avatar = validAvatars[dummy];
                            EditorGUIUtility.PingObject(avatar);
                            OnAvatarChanged?.Invoke();
                        }
                    }
                }

                ExtraGUI?.Invoke();
            }
            return avatar;
        }

        internal static bool DrawAdvancedAvatarWarning(VRCAvatarDescriptor avatar, bool warnNonHumanoid = true, bool warnPrefab = true, bool warnDoubleFX = true)
        {
            return (!warnPrefab || !DrawPrefabWarning(avatar)) && (!warnDoubleFX || !DrawDoubleFXWarning(avatar, warnNonHumanoid));
        }

        internal static bool DrawDoubleFXWarning(VRCAvatarDescriptor avatar, bool warnNonHumanoid = true)
        {
            if (!avatar) return false;
            VRCAvatarDescriptor.CustomAnimLayer[] layers = avatar.baseAnimationLayers;

            if (layers.Length > 3)
            {
                bool isDoubled = layers[3].type == layers[4].type;
                if (isDoubled)
                {
                    EditorGUILayout.HelpBox("Your Avatar's Action playable layer is set as FX. This is an uncommon bug.", MessageType.Error);
                    if (GUILayout.Button("Fix"))
                    {
                        avatar.baseAnimationLayers[3].type = VRCAvatarDescriptor.AnimLayerType.Action;
                        EditorUtility.SetDirty(avatar);
                    }
                }

                return isDoubled;
            }

            if (warnNonHumanoid)
                EditorGUILayout.HelpBox("Your Avatar's descriptor is set as Non-Humanoid! Please make sure that your Avatar's rig is Humanoid.", MessageType.Error);
            return warnNonHumanoid;

        }

        internal static bool DrawPrefabWarning(VRCAvatarDescriptor avatar)
        {
            if (!avatar) return false;
            bool isPrefab = PrefabUtility.IsPartOfAnyPrefab(avatar.gameObject);
            if (isPrefab)
            {
                EditorGUILayout.HelpBox("Target Avatar is a part of a prefab. Prefab unpacking is required.", MessageType.Error);
                if (GUILayout.Button("Unpack")) PrefabUtility.UnpackPrefabInstance(avatar.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            return isPrefab;
        }

        internal static void Link(string label, string url)
        {
            Color bgcolor = GUI.backgroundColor;
            GUI.backgroundColor = Color.clear;

            if (GUILayout.Button(new GUIContent(label, url), Styles.Label.faintLinkLabel))
                Application.OpenURL(url);
            UnderlineLastRectOnHover();

            GUI.backgroundColor = bgcolor;
        }
    }
}