using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace DreadScripts.VRCSDKPlus
{
    internal static class QuickSetup
    {
        [MenuItem("CONTEXT/VRCAvatarDescriptor/[SDK+] Quick Setup", false, 650)]
#pragma warning disable IDE0051 // Remove unused private members
        private static void MenuSetup(MenuCommand command)
#pragma warning restore IDE0051 // Remove unused private members
        {
            VRCAvatarDescriptor desc = (VRCAvatarDescriptor)command.context;
            Animator ani = desc.GetComponent<Animator>();
            SerializedObject serialized = new SerializedObject(desc);

            if (ani)
            {
                Transform leftEye = ani.GetBoneTransform(HumanBodyBones.LeftEye);
                Transform rightEye = ani.GetBoneTransform(HumanBodyBones.RightEye);

                Transform root = desc.transform;
                float worldXPosition;
                float worldYPosition;
                float worldZPosition;
                #region View Position
                if (leftEye && rightEye)
                {
                    Transform betterLeft = leftEye.parent.Find("LeftEye");
                    Transform betterRight = rightEye.parent.Find("RightEye");
                    leftEye = betterLeft ? betterLeft : leftEye;
                    rightEye = betterRight ? betterRight : rightEye;
                    Vector3 added = (leftEye.position + rightEye.position) / 2;
                    worldXPosition = added.x;
                    worldYPosition = added.y;
                    worldZPosition = added.z;
                }
                else
                {
                    Vector3 headPosition = ani.GetBoneTransform(HumanBodyBones.Head).position;
                    worldXPosition = headPosition.x;
                    worldYPosition = headPosition.y + ((headPosition.y - root.position.y) * 1.0357f - (headPosition.y - root.position.y));
                    worldZPosition = 0;
                }

                Vector3 realView = root.InverseTransformPoint(new Vector3(worldXPosition, worldYPosition, worldZPosition));
                realView = new Vector3(Mathf.Approximately(realView.x, 0) ? 0 : realView.x, realView.y, (realView.z + 0.0547f * realView.y) / 2);

                serialized.FindProperty("ViewPosition").vector3Value = realView;
                #endregion

                #region Eyes

                if (leftEye && rightEye)
                {
                    SerializedProperty eyes = serialized.FindProperty("customEyeLookSettings");
                    serialized.FindProperty("enableEyeLook").boolValue = true;

                    eyes.FindPropertyRelative("leftEye").objectReferenceValue = leftEye;
                    eyes.FindPropertyRelative("rightEye").objectReferenceValue = rightEye;

                    #region Rotation Values
                    const float axisValue = 0.1305262f;
                    const float wValue = 0.9914449f;

                    Quaternion upValue = new Quaternion(-axisValue, 0, 0, wValue);
                    Quaternion downValue = new Quaternion(axisValue, 0, 0, wValue);
                    Quaternion rightValue = new Quaternion(0, axisValue, 0, wValue);
                    Quaternion leftValue = new Quaternion(0, -axisValue, 0, wValue);

                    SerializedProperty up = eyes.FindPropertyRelative("eyesLookingUp");
                    SerializedProperty right = eyes.FindPropertyRelative("eyesLookingRight");
                    SerializedProperty down = eyes.FindPropertyRelative("eyesLookingDown");
                    SerializedProperty left = eyes.FindPropertyRelative("eyesLookingLeft");

                    void SetLeftAndRight(SerializedProperty p, Quaternion v)
                    {
                        p.FindPropertyRelative("left").quaternionValue = v;
                        p.FindPropertyRelative("right").quaternionValue = v;
                    }

                    SetLeftAndRight(up, upValue);
                    SetLeftAndRight(right, rightValue);
                    SetLeftAndRight(down, downValue);
                    SetLeftAndRight(left, leftValue);
                    #endregion

                    #region Blinking
                    SkinnedMeshRenderer body = null;
                    for (int i = 0; i < desc.transform.childCount; i++)
                    {
                        if (body = desc.transform.GetChild(i).GetComponent<SkinnedMeshRenderer>())
                            break;
                    }

                    if (body && body.sharedMesh)
                    {
                        for (int i = 0; i < body.sharedMesh.blendShapeCount; i++)
                        {
                            if (body.sharedMesh.GetBlendShapeName(i) != "Blink") continue;

                            eyes.FindPropertyRelative("eyelidType").enumValueIndex = 2;
                            eyes.FindPropertyRelative("eyelidsSkinnedMesh").objectReferenceValue = body;

                            SerializedProperty blendShapes = eyes.FindPropertyRelative("eyelidsBlendshapes");
                            blendShapes.arraySize = 3;
                            blendShapes.FindPropertyRelative("Array.data[0]").intValue = i;
                            blendShapes.FindPropertyRelative("Array.data[1]").intValue = -1;
                            blendShapes.FindPropertyRelative("Array.data[2]").intValue = -1;
                            break;
                        }
                    }
                    #endregion
                }
                #endregion
            }

            serialized.ApplyModifiedProperties();
            EditorApplication.delayCall -= ForceCallAutoLipSync;
            EditorApplication.delayCall += ForceCallAutoLipSync;
        }

        private static void ForceCallAutoLipSync()
        {
            EditorApplication.delayCall -= ForceCallAutoLipSync;

            Type descriptorEditor = Type.GetType("AvatarDescriptorEditor3, Assembly-CSharp-Editor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null") ??
                                   Type.GetType("AvatarDescriptorEditor3, VRC.SDK3A.Editor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");

            if (descriptorEditor == null)
            {
                Debug.LogWarning("AvatarDescriptorEditor3 Type couldn't be found!");
                return;
            }

            Editor tempEditor = (Editor)Resources.FindObjectsOfTypeAll(descriptorEditor)[0];
            descriptorEditor.GetMethod("AutoDetectLipSync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tempEditor, null);
        }
    }
}