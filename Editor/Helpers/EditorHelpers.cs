using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace DreadScripts.VRCSDKPlus
{
    internal static class EditorHelpers
    {
        internal static void InitialOverride()
        {
            EditorApplication.delayCall -= InitialOverride;

            Type attributeType = Type.GetType("UnityEditor.CustomEditorAttributes, UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            FieldInfo editorsInitializedField = attributeType.GetField("s_Initialized", BindingFlags.Static | BindingFlags.NonPublic);

            try
            {
                if (!(bool)editorsInitializedField.GetValue(null))
                {
                    MethodInfo rebuildEditorsMethod = attributeType.GetMethod("Rebuild", BindingFlags.Static | BindingFlags.NonPublic);
                    rebuildEditorsMethod.Invoke(null, null);
                    editorsInitializedField.SetValue(null, true);
                }

                OverrideEditor(typeof(VRCExpressionParameters), typeof(VRCParamsPlus));
                OverrideEditor(typeof(VRCExpressionsMenu), typeof(VRCMenuPlus));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[VRCSDK+] Failed to override editors!");
            }
        }

        internal static void OverrideEditor(Type componentType, Type editorType)
        {
            Type attributeType = Type.GetType("UnityEditor.CustomEditorAttributes, UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            Type monoEditorType = Type.GetType("UnityEditor.CustomEditorAttributes+MonoEditorType, UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            FieldInfo editorsField = attributeType.GetField("kSCustomEditors", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo inspectorField = monoEditorType.GetField("m_InspectorType", BindingFlags.Public | BindingFlags.Instance);
            IDictionary editorDictionary = editorsField.GetValue(null) as IDictionary;
            IList editorsList = editorDictionary[componentType] as IList;
            inspectorField.SetValue(editorsList[0], editorType);

            Type inspectorType = Type.GetType("UnityEditor.InspectorWindow, UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            MethodInfo myTestMethod = inspectorType.GetMethod("RefreshInspectors", BindingFlags.NonPublic | BindingFlags.Static);
            myTestMethod.Invoke(null, null);
        }
    }
}