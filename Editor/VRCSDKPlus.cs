using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;
using Object = UnityEngine.Object;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed class VRCSDKPlus
    {
        internal static bool Initialized;
        internal static GUIContent RedWarnIcon;
        internal static GUIContent YellowWarnIcon;
        internal static GUIStyle CenteredLabel
        {
            get
            {
                return new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            }
        }

        internal static readonly string[] AllPlayables =
        {
            "Base",
            "Additive",
            "Gesture",
            "Action",
            "FX",
            "Sitting",
            "TPose",
            "IKPose"
        };

        internal static VRCAvatarDescriptor Avatar;
        internal static VRCAvatarDescriptor[] ValidAvatars;
        internal static AnimatorControllerParameter[] ValidParameters;

        internal static string[] ValidPlayables;
        internal static int[] ValidPlayableIndexes;

        internal static void InitConstants()
        {
            if (Initialized) return;
            RedWarnIcon = new GUIContent(EditorGUIUtility.IconContent("CollabError"));
            //advancedPopupMethod = typeof(EditorGUI).GetMethod("AdvancedPopup", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Rect), typeof(int), typeof(string[]) }, null);
            YellowWarnIcon = new GUIContent(EditorGUIUtility.IconContent("d_console.warnicon.sml"));
            Initialized = true;
        }

        internal static void RefreshAvatar(Func<VRCAvatarDescriptor, bool> favoredAvatar = null)
        {
            Helpers.RefreshAvatar(ref Avatar, ref ValidAvatars, null, favoredAvatar);
            RefreshAvatarInfo();
        }

        internal static void RefreshAvatarInfo()
        {
            RefreshValidParameters();
            RefreshValidPlayables();
        }

        internal static void RefreshValidParameters()
        {
            if (!Avatar)
            {
                ValidParameters = Array.Empty<AnimatorControllerParameter>();
                return;
            }
            List<AnimatorControllerParameter> validParams = new List<AnimatorControllerParameter>();
            foreach (RuntimeAnimatorController r in Avatar.baseAnimationLayers.Concat(Avatar.specialAnimationLayers).Select(p => p.animatorController).Concat(Avatar.GetComponentsInChildren<Animator>(true).Select(a => a.runtimeAnimatorController)).Distinct())
            {
                if (!r) continue;

                AnimatorController c = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(r));
                if (c) validParams.AddRange(c.parameters);
            }

            ValidParameters = validParams.Distinct().OrderBy(p => p.name).ToArray();
        }

        internal static void RefreshValidPlayables()
        {
            if (!Avatar)
            {
                ValidPlayables = Array.Empty<string>();
                ValidPlayableIndexes = Array.Empty<int>();
                return;
            }
            List<(string, int)> myPlayables = new List<(string, int)>();
            for (int i = 0; i < AllPlayables.Length; i++)
            {
                int index = i == 0 ? i : i + 1;
                if (Avatar.GetPlayableLayer((VRCAvatarDescriptor.AnimLayerType)index, out AnimatorController _))
                {
                    myPlayables.Add((AllPlayables[i], index));
                }
            }

            ValidPlayables = new string[myPlayables.Count];
            ValidPlayableIndexes = new int[myPlayables.Count];
            for (int i = 0; i < myPlayables.Count; i++)
            {
                ValidPlayables[i] = myPlayables[i].Item1;
                ValidPlayableIndexes[i] = myPlayables[i].Item2;
            }
        }
    }
}