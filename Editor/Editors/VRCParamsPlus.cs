using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;
using static DreadScripts.VRCSDKPlus.VRCSDKPlus;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCParamsPlus : Editor
    {
        private static int _MAX_MEMORY_COST;
        private static int MAX_MEMORY_COST
        {
            get
            {
                if (_MAX_MEMORY_COST == 0)
                {
                    try
                    { _MAX_MEMORY_COST = (int)typeof(VRCExpressionParameters).GetField("MAX_PARAMETER_COST", BindingFlags.Static | BindingFlags.Public).GetValue(null); }
                    catch
                    {
                        Debug.LogError("Failed to dynamically get MAX_PARAMETER_COST. Falling back to 256");
                        _MAX_MEMORY_COST = 256;
                    }
                }

                return _MAX_MEMORY_COST;
            }
        }

        private static readonly bool hasSyncingOption = typeof(VRCExpressionParameters.Parameter).GetField("networkSynced") != null;
        private static bool editorActive = true;
        private static bool canCleanup;
        private int currentCost;
        private string searchValue;

        private SerializedProperty parameterList;
        private ReorderableList parametersOrderList;

        private ParameterStatus[] _parameterStatus;

        private static VRCExpressionParameters mergeParams;

        [InitializeOnLoadMethod]
        internal static void DelayCallOverride()
        {
            EditorApplication.delayCall -= EditorHelpers.InitialOverride;
            EditorApplication.delayCall += EditorHelpers.InitialOverride;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            using (new GUILayout.HorizontalScope("helpbox"))
                GUIHelpers.DrawAdvancedAvatarFull(ref VRCSDKPlus.Avatar, ValidAvatars, RefreshValidParameters, false, false, false, "Active Avatar");

            canCleanup = false;
            serializedObject.Update();
            HandleParameterEvents();
            parametersOrderList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();

            if (canCleanup)
            {
                using (new GUILayout.HorizontalScope("helpbox"))
                {
                    GUILayout.Label("Cleanup Invalid, Blank, and Duplicate Parameters");
                    if (GUIHelpers.ClickableButton("Cleanup"))
                    {
                        RefreshValidParameters();
                        parameterList.IterateArray((i, p) =>
                        {
                            string name = p.FindPropertyRelative("name").stringValue;
                            if (string.IsNullOrEmpty(name))
                            {
                                Helpers.GreenLog($"Deleted blank parameter at index {i}");
                                parameterList.DeleteArrayElementAtIndex(i);
                                return false;
                            }

                            if (VRCSDKPlus.Avatar && ValidParameters.All(p2 => p2.name != name))
                            {
                                Helpers.GreenLog($"Deleted invalid parameter {name}");
                                parameterList.DeleteArrayElementAtIndex(i);
                                return false;
                            }

                            parameterList.IterateArray((j, p2) =>
                            {
                                if (name == p2.FindPropertyRelative("name").stringValue)
                                {
                                    Helpers.GreenLog($"Deleted duplicate parameter {name}");
                                    parameterList.DeleteArrayElementAtIndex(j);
                                }

                                return false;
                            }, i);


                            return false;
                        });
                        serializedObject.ApplyModifiedProperties();
                        RefreshValidParameters();
                        Helpers.GreenLog("Finished Cleanup!");
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            using (new GUILayout.HorizontalScope("helpbox"))
                mergeParams = (VRCExpressionParameters)EditorGUILayout.ObjectField("Merge Parameters", null, typeof(VRCExpressionParameters), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (mergeParams)
                {
                    if (mergeParams.parameters != null)
                    {
                        VRCExpressionParameters myParams = (VRCExpressionParameters)target;
                        Undo.RecordObject(myParams, "Merge Parameters");
                        myParams.parameters = myParams.parameters.Concat(mergeParams.parameters.Select(p =>
                            new VRCExpressionParameters.Parameter()
                            {
                                defaultValue = p.defaultValue,
                                name = p.name,
                                networkSynced = p.networkSynced,
                                valueType = p.valueType
                            })).ToArray();
                        EditorUtility.SetDirty(myParams);
                    }
                    mergeParams = null;
                }
            }

            CalculateTotalCost();
            try
            {
                using (new EditorGUILayout.HorizontalScope("helpbox"))
                {
                    GUILayout.FlexibleSpace();
                    using (new GUILayout.VerticalScope())
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.Label("Total Memory");
                            GUILayout.FlexibleSpace();

                        }

                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.Label($"{currentCost} / {MAX_MEMORY_COST}");
                            if (currentCost > MAX_MEMORY_COST)
                                GUILayout.Label(RedWarnIcon, GUILayout.Width(20));
                            GUILayout.FlexibleSpace();

                        }
                    }

                    GUILayout.FlexibleSpace();
                }
            }
            catch { }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUIHelpers.Link("Made By @Dreadrith ♡", "https://dreadrith.com/links");
            }

            if (EditorGUI.EndChangeCheck()) RefreshAllParameterStatus();
        }

        private void OnEnable()
        {
            InitConstants();
            RefreshAvatar(a => a.expressionParameters == target);

            parameterList = serializedObject.FindProperty("parameters");
            RefreshParametersOrderList();
            RefreshAllParameterStatus();
        }


        //private static float guitest = 10;
        private void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            if (!(index < parameterList.arraySize && index >= 0)) return;

            Rect screenRect = GUIUtility.GUIToScreenRect(rect);
            if (screenRect.y > Screen.currentResolution.height || screenRect.y + screenRect.height < 0) return;

            SerializedProperty parameter = parameterList.GetArrayElementAtIndex(index);
            SerializedProperty name = parameter.FindPropertyRelative("name");
            SerializedProperty valueType = parameter.FindPropertyRelative("valueType");
            SerializedProperty defaultValue = parameter.FindPropertyRelative("defaultValue");
            SerializedProperty saved = parameter.FindPropertyRelative("saved");
            SerializedProperty synced = hasSyncingOption ? parameter.FindPropertyRelative("networkSynced") : null;

            ParameterStatus status = _parameterStatus[index];
            bool parameterEmpty = status.parameterEmpty;
            bool parameterAddable = status.parameterAddable;
            bool parameterIsDuplicate = status.parameterIsDuplicate;
            bool hasWarning = status.hasWarning;
            string warnMsg = parameterEmpty ? "Blank Parameter" : parameterIsDuplicate ? "Duplicate Parameter! May cause issues!" : "Parameter not found in any playable controller of Active Avatar";
            AnimatorControllerParameter matchedParameter = status.matchedParameter;

            canCleanup |= hasWarning;

            #region Rects
            rect.y += 1;
            rect.height = 18;


            Rect UseNext(float width, bool fixedWidth = false, float position = -1, bool fixedPosition = false)
            {
                Rect currentRect = rect;
                currentRect.width = fixedWidth ? width : width * rect.width / 100;
                currentRect.height = rect.height;
                currentRect.x = position == -1 ? rect.x : fixedPosition ? position : rect.x + position * rect.width / 100;
                currentRect.y = rect.y;
                rect.x += currentRect.width;
                return currentRect;
            }

            Rect UseEnd(ref Rect r, float width, bool fixedWidth = false, float positionOffset = -1, bool fixedPosition = false)
            {
                Rect returnRect = r;
                returnRect.width = fixedWidth ? width : width * r.width / 100;
                float positionAdjust = positionOffset == -1 ? 0 : fixedPosition ? positionOffset : positionOffset * r.width / 100;
                returnRect.x = r.x + r.width - returnRect.width - positionAdjust;
                r.width -= returnRect.width + positionAdjust;
                return returnRect;
            }

            Rect contextRect = rect;
            contextRect.x -= 20;
            contextRect.width = 20;

            Rect removeRect = UseEnd(ref rect, 32, true, 4, true);
            Rect syncedRect = hasSyncingOption ? UseEnd(ref rect, 18, true, 16f, true) : Rect.zero;
            Rect savedRect = UseEnd(ref rect, 18, true, hasSyncingOption ? 34f : 16, true);
            Rect defaultRect = UseEnd(ref rect, 85, true, 32, true);
            Rect typeRect = UseEnd(ref rect, 85, true, 12, true);
            Rect warnRect = UseEnd(ref rect, 18, true, 4, true);
            Rect addRect = hasWarning && parameterAddable ? UseEnd(ref rect, 55, true, 4, true) : Rect.zero;
            Rect dropdownRect = UseEnd(ref rect, 21, true, 1, true);
            dropdownRect.x -= 3;
            Rect nameRect = UseNext(100);

            //Rect removeRect = new Rect(rect.x + rect.width - 36, rect.y, 32, 18);
            //Rect syncedRect = new Rect(rect.x + rect.width - 60, rect.y, 14, 18);
            #endregion

            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(searchValue) && !Regex.IsMatch(name.stringValue, $@"(?i){searchValue}")))
            {
                //Hacky way to avoid proper UI Layout 
                string parameterFieldName = $"namefield{index}";

                using (new EditorGUI.DisabledScope(ValidParameters.Length == 0))
                    if (GUI.Button(dropdownRect, GUIContent.none, EditorStyles.popup))
                    {

                        AnimatorControllerParameter[] filteredParameters = ValidParameters.Where(conParam => !parameterList.IterateArray((_, prop) => prop.FindPropertyRelative("name").stringValue == conParam.name)).ToArray();
                        if (filteredParameters.Any())
                        {
                            CustomDropdown<AnimatorControllerParameter> textDropdown = new CustomDropdown<AnimatorControllerParameter>(null, filteredParameters, item =>
                            {
                                using (new GUILayout.HorizontalScope())
                                {
                                    GUILayout.Label(item.value.name);
                                    GUILayout.Label(item.value.type.ToString(), Styles.Label.TypeLabel, GUILayout.ExpandWidth(false));
                                }
                            }, (_, conParam) =>
                            {
                                name.stringValue = conParam.name;
                                name.serializedObject.ApplyModifiedProperties();
                                RefreshAllParameterStatus();
                            });
                            textDropdown.EnableSearch((conParameter, search) => Regex.IsMatch(conParameter.name, $@"(?i){search}"));
                            textDropdown.Show(nameRect);
                        }
                    }

                GUI.SetNextControlName(parameterFieldName);
                EditorGUI.PropertyField(nameRect, name, GUIContent.none);
                EditorGUI.PropertyField(typeRect, valueType, GUIContent.none);
                EditorGUI.PropertyField(savedRect, saved, GUIContent.none);

                GUI.Label(nameRect, matchedParameter != null ? $"({matchedParameter.type})" : "(?)", Styles.Label.RightPlaceHolder);

                if (hasSyncingOption) EditorGUI.PropertyField(syncedRect, synced, GUIContent.none);

                if (parameterAddable)
                {
                    using (EditorGUI.ChangeCheckScope change = new EditorGUI.ChangeCheckScope())
                    {
                        GUIHelpers.MakeRectLinkCursor(addRect);
                        int dummy = EditorGUI.IntPopup(addRect, -1, ValidPlayables, ValidPlayableIndexes);
                        if (change.changed)
                        {
                            VRCAvatarDescriptor.AnimLayerType playable = (VRCAvatarDescriptor.AnimLayerType)dummy;
                            if (VRCSDKPlus.Avatar.GetPlayableLayer(playable, out AnimatorController c))
                            {
                                if (c.parameters.All(p => p.name != name.stringValue))
                                {
                                    AnimatorControllerParameterType paramType;
                                    switch (valueType.enumValueIndex)
                                    {
                                        case 0:
                                            paramType = AnimatorControllerParameterType.Int;
                                            break;
                                        case 1:
                                            paramType = AnimatorControllerParameterType.Float;
                                            break;
                                        default:
                                        case 2:
                                            paramType = AnimatorControllerParameterType.Bool;
                                            break;
                                    }

                                    c.AddParameter(new AnimatorControllerParameter()
                                    {
                                        name = name.stringValue,
                                        type = paramType,
                                        defaultFloat = defaultValue.floatValue,
                                        defaultInt = (int)defaultValue.floatValue,
                                        defaultBool = defaultValue.floatValue > 0
                                    });
                                    Helpers.GreenLog($"Added {paramType} {name.stringValue} to {playable} Playable Controller");
                                }

                                RefreshValidParameters();
                            }
                        }
                    }

                    addRect.x += 3;
                    GUI.Label(addRect, "Add");
                }

                if (hasWarning) GUI.Label(warnRect, new GUIContent(YellowWarnIcon) { tooltip = warnMsg });

                switch (valueType.enumValueIndex)
                {
                    case 2:
                        EditorGUI.BeginChangeCheck();
                        int dummy = EditorGUI.Popup(defaultRect, defaultValue.floatValue == 0 ? 0 : 1, new[] { "False", "True" });
                        if (EditorGUI.EndChangeCheck())
                            defaultValue.floatValue = dummy;
                        break;
                    default:
                        EditorGUI.PropertyField(defaultRect, defaultValue, GUIContent.none);
                        break;
                }

                GUIHelpers.MakeRectLinkCursor(removeRect);
                if (GUI.Button(removeRect, CustomGUIContent.Remove, Styles.Label.RemoveIcon))
                    DeleteParameter(index);
            }

            Event e = Event.current;
            if (e.type == EventType.ContextClick && contextRect.Contains(e.mousePosition))
            {
                e.Use();
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateParameter(index));
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Delete"), false, () => DeleteParameter(index));
                menu.ShowAsContext();
            }
        }


        private void DrawHeader(Rect rect)
        {
            #region Rects
            /*rect.y += 1;
            rect.height = 18;

            Rect baseRect = rect;

            Rect UseNext(float width, bool fixedWidth = false, float position = -1, bool fixedPosition = false)
            {
                Rect currentRect = baseRect;
                currentRect.width = fixedWidth ? width : width * baseRect.width / 100;
                currentRect.height = baseRect.height;
                currentRect.x = position == -1 ? baseRect.x : fixedPosition ? position : rect.x + position * baseRect.width / 100; ;
                currentRect.y = baseRect.y;
                baseRect.x += currentRect.width;
                return currentRect;
            }

            Rect UseEnd(ref Rect r, float width, bool fixedWidth = false, float positionOffset = -1, bool fixedPosition = false)
            {
                Rect returnRect = r;
                returnRect.width = fixedWidth ? width : width * r.width / 100;
                float positionAdjust = positionOffset == -1 ? 0 : fixedPosition ? positionOffset : positionOffset * r.width / 100;
                returnRect.x = r.x + r.width - returnRect.width - positionAdjust;
                r.width -= returnRect.width + positionAdjust;
                return returnRect;
            }

            UseEnd(ref rect, 32, true, 4, true);
            Rect syncedRect = UseEnd(ref rect, 55, true);
            Rect savedRect = UseEnd(ref rect, 55, true);
            Rect defaultRect = UseEnd(ref rect, 60, true, 30, true);
            Rect typeRect = UseNext(16.66f);
            Rect nameRect = UseNext(rect.width * 0.4f, true);
            Rect searchIconRect = nameRect;
            searchIconRect.x += searchIconRect.width / 2 - 40;
            searchIconRect.width = 18;
            Rect searchRect = Rect.zero;
            Rect searchClearRect = Rect.zero;

            UseNext(canCleanup ? 12 : 26, true);
            UseNext(12, true);*/

            rect.y += 1;
            rect.height = 18;


            Rect UseNext(float width, bool fixedWidth = false, float position = -1, bool fixedPosition = false)
            {
                Rect currentRect = rect;
                currentRect.width = fixedWidth ? width : width * rect.width / 100;
                currentRect.height = rect.height;
                currentRect.x = position == -1 ? rect.x : fixedPosition ? position : rect.x + position * rect.width / 100;
                currentRect.y = rect.y;
                rect.x += currentRect.width;
                return currentRect;
            }

            Rect UseEnd(ref Rect r, float width, bool fixedWidth = false, float positionOffset = -1, bool fixedPosition = false)
            {
                Rect returnRect = r;
                returnRect.width = fixedWidth ? width : width * r.width / 100;
                float positionAdjust = positionOffset == -1 ? 0 : fixedPosition ? positionOffset : positionOffset * r.width / 100;
                returnRect.x = r.x + r.width - returnRect.width - positionAdjust;
                r.width -= returnRect.width + positionAdjust;
                return returnRect;
            }

            UseEnd(ref rect, 32, true, 4, true);
            Rect syncedRect = hasSyncingOption ? UseEnd(ref rect, 54, true) : Rect.zero;
            Rect savedRect = UseEnd(ref rect, 54, true);
            Rect defaultRect = UseEnd(ref rect, 117, true);
            Rect typeRect = UseEnd(ref rect, 75, true);
            UseEnd(ref rect, 48, true);
            Rect nameRect = UseNext(100);

            //guitest = EditorGUILayout.FloatField(guitest);

            Rect searchIconRect = nameRect;
            searchIconRect.x += searchIconRect.width / 2 - 40;
            searchIconRect.width = 18;
            Rect searchRect = Rect.zero;
            Rect searchClearRect = Rect.zero;
            #endregion

            const string controlName = "VRCSDKParameterSearch";
            if (VRCSDKPlusToolbox.HasReceivedCommand(EventCommands.Find)) GUI.FocusControl(controlName);
            VRCSDKPlusToolbox.HandleTextFocusConfirmCommands(controlName, onCancel: () => searchValue = string.Empty);
            bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
            bool isSearching = isFocused || !string.IsNullOrEmpty(searchValue);
            if (isSearching)
            {
                searchRect = nameRect; searchRect.x += 14; searchRect.width -= 14;
                searchClearRect = searchRect; searchClearRect.x += searchRect.width - 18; searchClearRect.y -= 1; searchClearRect.width = 16;
            }
            GUIHelpers.MakeRectLinkCursor(searchIconRect);
            if (GUI.Button(searchIconRect, CustomGUIContent.Search, CenteredLabel))
                EditorGUI.FocusTextInControl(controlName);

            GUI.Label(nameRect, new GUIContent("Name", "Name of the Parameter. This must match the name of the parameter that it is controlling in the playable layers. Case sensitive."), CenteredLabel);

            GUIHelpers.MakeRectLinkCursor(searchClearRect);
            if (GUI.Button(searchClearRect, string.Empty, GUIStyle.none))
            {
                searchValue = string.Empty;
                if (isFocused) GUI.FocusControl(string.Empty);
            }
            GUI.SetNextControlName(controlName);
            searchValue = GUI.TextField(searchRect, searchValue, "SearchTextField");
            GUI.Button(searchClearRect, CustomGUIContent.Clear, CenteredLabel);
            GUI.Label(typeRect, new GUIContent("Type", "Type of the Parameter."), CenteredLabel);
            GUI.Label(defaultRect, new GUIContent("Default", "The default/start value of this parameter."), CenteredLabel);
            GUI.Label(savedRect, new GUIContent("Saved", "Value will stay when loading avatar or changing worlds"), CenteredLabel);

            if (hasSyncingOption)
                GUI.Label(syncedRect, new GUIContent("Synced", "Value will be sent over the network to remote users. This is needed if this value should be the same locally and remotely. Synced parameters count towards the total memory usage."), CenteredLabel);

        }

        private void HandleParameterEvents()
        {
            if (!parametersOrderList.HasKeyboardControl()) return;
            if (!parametersOrderList.TryGetActiveIndex(out int index)) return;
            if (VRCSDKPlusToolbox.HasReceivedCommand(EventCommands.Duplicate))
                DuplicateParameter(index);

            if (VRCSDKPlusToolbox.HasReceivedAnyDelete())
                DeleteParameter(index);
        }


        #region Automated Methods
        [MenuItem("CONTEXT/VRCExpressionParameters/[SDK+] Toggle Editor", false, 899)]
        private static void ToggleEditor()
        {
            editorActive = !editorActive;

            Type targetType = Helpers.ExtendedGetType("VRCExpressionParameters");
            if (targetType == null)
            {
                Debug.LogError("[VRCSDK+] VRCExpressionParameters was not found! Could not apply custom editor.");
                return;
            }
            if (editorActive) EditorHelpers.OverrideEditor(targetType, typeof(VRCParamsPlus));
            else
            {
                Type expressionsEditor = Helpers.ExtendedGetType("VRCExpressionParametersEditor");
                if (expressionsEditor == null)
                {
                    Debug.LogWarning("[VRCSDK+] VRCExpressionParametersEditor was not found! Could not apply custom editor");
                    return;
                }

                EditorHelpers.OverrideEditor(targetType, expressionsEditor);
            }

        }

        private void RefreshAllParameterStatus()
        {
            VRCExpressionParameters expressionParameters = (VRCExpressionParameters)target;
            if (expressionParameters.parameters == null)
            {
                expressionParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
                EditorUtility.SetDirty(expressionParameters);
            }
            VRCExpressionParameters.Parameter[] parameters = expressionParameters.parameters;
            _parameterStatus = new ParameterStatus[parameters.Length];

            for (int index = 0; index < parameters.Length; index++)
            {
                VRCExpressionParameters.Parameter exParameter = expressionParameters.parameters[index];
                AnimatorControllerParameter matchedParameter = ValidParameters.FirstOrDefault(conParam => conParam.name == exParameter.name);
                bool parameterEmpty = string.IsNullOrEmpty(exParameter.name);
                bool parameterIsValid = matchedParameter != null;
                bool parameterAddable = VRCSDKPlus.Avatar && !parameterIsValid && !parameterEmpty;
                bool parameterIsDuplicate = !parameterEmpty && expressionParameters.parameters.Where((p2, i) => index != i && exParameter.name == p2.name).Any(); ;
                bool hasWarning = (VRCSDKPlus.Avatar && !parameterIsValid) || parameterEmpty || parameterIsDuplicate;
                _parameterStatus[index] = new ParameterStatus()
                {
                    parameterEmpty = parameterEmpty,
                    parameterAddable = parameterAddable,
                    parameterIsDuplicate = parameterIsDuplicate,
                    hasWarning = hasWarning,
                    matchedParameter = matchedParameter
                };
            }
        }

        private void CalculateTotalCost()
        {
            currentCost = 0;
            for (int i = 0; i < parameterList.arraySize; i++)
            {
                SerializedProperty p = parameterList.GetArrayElementAtIndex(i);
                SerializedProperty synced = p.FindPropertyRelative("networkSynced");
                if (synced != null && !synced.boolValue) continue;
                currentCost += p.FindPropertyRelative("valueType").enumValueIndex == 2 ? 1 : 8;
            }
        }

        private void RefreshParametersOrderList()
        {
            parametersOrderList = new ReorderableList(serializedObject, parameterList, true, true, true, false)
            {
                drawElementCallback = DrawElement,
                drawHeaderCallback = DrawHeader
            };
            parametersOrderList.onReorderCallback += _ => RefreshAllParameterStatus();
            parametersOrderList.onAddCallback = _ =>
            {
                parameterList.InsertArrayElementAtIndex(parameterList.arraySize);
                MakeParameterUnique(parameterList.arraySize - 1);
            };
        }

        private void DuplicateParameter(int index)
        {
            parameterList.InsertArrayElementAtIndex(index);
            MakeParameterUnique(index + 1);
            parameterList.serializedObject.ApplyModifiedProperties();
            RefreshAllParameterStatus();
        }

        private void DeleteParameter(int index)
        {
            parameterList.DeleteArrayElementAtIndex(index);
            parameterList.serializedObject.ApplyModifiedProperties();
            RefreshAllParameterStatus();
        }
        private void MakeParameterUnique(int index)
        {
            SerializedProperty newElement = parameterList.GetArrayElementAtIndex(index);
            SerializedProperty nameProp = newElement.FindPropertyRelative("name");
            nameProp.stringValue = VRCSDKPlusToolbox.GenerateUniqueString(nameProp.stringValue, newName =>
            {
                for (int i = 0; i < parameterList.arraySize; i++)
                {
                    if (i == index) continue;
                    SerializedProperty p = parameterList.GetArrayElementAtIndex(i);
                    if (p.FindPropertyRelative("name").stringValue == newName) return false;
                }
                return true;
            });
        }

#endregion
    }
}