using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed class VRCMenuPlus : Editor, IHasCustomMenu
    {
        private static bool editorActive = true;
        private static VRCAvatarDescriptor _avatar;
        private VRCAvatarDescriptor[] _validAvatars;
        private ReorderableList _controlsList;

        private static readonly LinkedList<VRCExpressionsMenu> _menuHistory = new LinkedList<VRCExpressionsMenu>();
        private static LinkedListNode<VRCExpressionsMenu> _currentNode;
        private static VRCExpressionsMenu _lastMenu;

        private static VRCExpressionsMenu moveSourceMenu;
        private static VRCExpressionsMenu.Control moveTargetControl;
        private static bool isMoving;

        #region Initialization
        private void ReInitializeAll()
        {
            CheckAvatar();
            CheckMenu();
            InitializeList();
        }

        private void CheckAvatar()
        {
            _validAvatars = FindObjectsOfType<VRCAvatarDescriptor>();
            if (_validAvatars.Length == 0) _avatar = null;
            else if (!_avatar) _avatar = _validAvatars[0];
        }

        private void CheckMenu()
        {
            VRCExpressionsMenu currentMenu = target as VRCExpressionsMenu;
            if (!currentMenu || currentMenu == _lastMenu) return;

            if (_currentNode != null && _menuHistory.Last != _currentNode)
            {
                LinkedListNode<VRCExpressionsMenu> node = _currentNode.Next;
                while (node != null)
                {
                    LinkedListNode<VRCExpressionsMenu> nextNode = node.Next;
                    _menuHistory.Remove(node);
                    node = nextNode;
                }
            }

            _lastMenu = currentMenu;
            _currentNode = _menuHistory.AddLast(currentMenu);
        }

        private void InitializeList()
        {
            SerializedProperty l = serializedObject.FindProperty("controls");
            _controlsList = new ReorderableList(serializedObject, l, true, true, true, false);
            _controlsList.onCanAddCallback += reorderableList => reorderableList.count < 8;
            _controlsList.onAddCallback = _ =>
            {
                SerializedProperty controlsProp = _controlsList.serializedProperty;
                int index = controlsProp.arraySize++;
                _controlsList.index = index;

                SerializedProperty c = controlsProp.GetArrayElementAtIndex(index);
                c.FindPropertyRelative("name").stringValue = "New Control";
                c.FindPropertyRelative("icon").objectReferenceValue = null;
                c.FindPropertyRelative("parameter").FindPropertyRelative("name").stringValue = "";
                c.FindPropertyRelative("type").enumValueIndex = 1;
                c.FindPropertyRelative("subMenu").objectReferenceValue = null;
                c.FindPropertyRelative("labels").ClearArray();
                c.FindPropertyRelative("subParameters").ClearArray();
                c.FindPropertyRelative("value").floatValue = 1;
            };
            _controlsList.drawHeaderCallback = rect =>
            {
                if (isMoving && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    isMoving = false;
                    Repaint();
                }
                EditorGUI.LabelField(rect, $"Controls ({_controlsList.count} / 8)");

                // Draw copy, paste, duplicate, and move buttons
                #region Rects
                Rect copyRect = new Rect(
                    rect.x + rect.width - rect.height - ((rect.height + Styles.Padding) * 3),
                    rect.y,
                    rect.height,
                    rect.height);

                Rect pasteRect = new Rect(
                    copyRect.x + copyRect.width + Styles.Padding,
                    copyRect.y,
                    copyRect.height,
                    copyRect.height);

                Rect duplicateRect = new Rect(
                    pasteRect.x + pasteRect.width + Styles.Padding,
                    pasteRect.y,
                    pasteRect.height,
                    pasteRect.height);

                Rect moveRect = new Rect(
                    duplicateRect.x + duplicateRect.width + Styles.Padding,
                    duplicateRect.y,
                    duplicateRect.height,
                    duplicateRect.height);

                #endregion

                bool isFull = _controlsList.count >= 8;
                bool isEmpty = _controlsList.count == 0;
                bool hasIndex = _controlsList.TryGetActiveIndex(out int index);
                bool hasFocus = _controlsList.HasKeyboardControl();
                if (!hasIndex) index = _controlsList.count;
                using (new EditorGUI.DisabledScope(isEmpty || !hasFocus || !hasIndex))
                {
                    #region Copy

                    GUIHelpers.MakeRectLinkCursor(copyRect);
                    if (GUI.Button(copyRect, CustomGUIContent.Copy, GUI.skin.label))
                        CopyControl(index);


                    #endregion

                    // This section was also created entirely by GitHub Copilot :3

                    #region Duplicate

                    using (new EditorGUI.DisabledScope(isFull))
                    {
                        GUIHelpers.MakeRectLinkCursor(duplicateRect);
                        if (GUI.Button(duplicateRect, isFull ? new GUIContent(CustomGUIContent.Duplicate) { tooltip = CustomGUIContent.MenuFullTooltip } : CustomGUIContent.Duplicate, GUI.skin.label))
                            DuplicateControl(index);
                    }

                    #endregion
                }

                #region Paste
                using (new EditorGUI.DisabledScope(!CanPasteControl()))
                {
                    GUIHelpers.MakeRectLinkCursor(pasteRect);
                    if (GUI.Button(pasteRect, CustomGUIContent.Paste, GUI.skin.label))
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Paste values"), false, isEmpty || !hasFocus ? (GenericMenu.MenuFunction)null : () => PasteControl(index, false));
                        menu.AddItem(
                            new GUIContent("Insert as new"),
                            false,
                            isFull ? (GenericMenu.MenuFunction)null : () => PasteControl(index, true)
                        );
                        menu.ShowAsContext();
                    }
                }
                #endregion


                #region Move
                using (new EditorGUI.DisabledScope((isMoving && isFull) || (!isMoving && (!hasFocus || isEmpty))))
                {
                    GUIHelpers.MakeRectLinkCursor(moveRect);
                    if (GUI.Button(moveRect, isMoving ? isFull ? new GUIContent(CustomGUIContent.Place) { tooltip = CustomGUIContent.MenuFullTooltip } : CustomGUIContent.Place : CustomGUIContent.Move, GUI.skin.label))
                    {
                        if (!isMoving) MoveControl(index);
                        else PlaceControl(index);
                    }
                }

                #endregion


            };
            _controlsList.drawElementCallback = (rect2, index, _, focused) =>
            {
                if (!(index < l.arraySize && index >= 0)) return;
                SerializedProperty controlProp = l.GetArrayElementAtIndex(index);
                VRCExpressionsMenu.Control.ControlType controlType = controlProp.FindPropertyRelative("type").ToControlType();
                Rect removeRect = new Rect(rect2.width + 3, rect2.y + 1, 32, 18);
                rect2.width -= 48;
                // Draw control type
                EditorGUI.LabelField(rect2, controlType.ToString(), focused
                        ? Styles.Label.TypeFocused
                        : Styles.Label.Type);

                // Draw control name
                GUIContent nameGuiContent = new GUIContent(controlProp.FindPropertyRelative("name").stringValue);
                bool emptyName = string.IsNullOrEmpty(nameGuiContent.text);
                if (emptyName) nameGuiContent.text = "[Unnamed]";

                Rect nameRect = new Rect(rect2.x, rect2.y, Styles.Label.RichText.CalcSize(nameGuiContent).x, rect2.height);

                EditorGUI.LabelField(nameRect,
                    new GUIContent(nameGuiContent),
                    emptyName ? Styles.Label.PlaceHolder : Styles.Label.RichText);
                GUIHelpers.
                                    MakeRectLinkCursor(removeRect);
                if (GUI.Button(removeRect, CustomGUIContent.Remove, Styles.Label.RemoveIcon))
                    DeleteControl(index);

                Event e = Event.current;

                if (controlType == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    if (e.clickCount == 2 && e.type == EventType.MouseDown && rect2.Contains(e.mousePosition))
                    {
                        UnityEngine.Object sm = controlProp.FindPropertyRelative("subMenu").objectReferenceValue;
                        if (sm) Selection.activeObject = sm;
                        e.Use();
                    }
                }

                if (e.type == EventType.ContextClick && rect2.Contains(e.mousePosition))
                {
                    e.Use();
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Cut"), false, () => MoveControl(index));
                    menu.AddItem(new GUIContent("Copy"), false, () => CopyControl(index));
                    if (!CanPasteControl()) menu.AddDisabledItem(new GUIContent("Paste"));
                    else
                    {
                        menu.AddItem(new GUIContent("Paste/Values"), false, () => PasteControl(index, false));
                        menu.AddItem(new GUIContent("Paste/As New"), false, () => PasteControl(index, true));
                    }
                    menu.AddSeparator(string.Empty);
                    menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateControl(index));
                    menu.AddItem(new GUIContent("Delete"), false, () => DeleteControl(index));
                    menu.ShowAsContext();
                }

            };
        }

        private VRCExpressionParameters.Parameter FetchParameter(string name)
        {
            if (!_avatar || !_avatar.expressionParameters) return null;
            VRCExpressionParameters par = _avatar.expressionParameters;
            return par.parameters?.FirstOrDefault(p => p.name == name);
        }
        #endregion

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            HandleControlEvents();
            DrawHistory();
            DrawHead();
            DrawBody();
            DrawFooter();
            serializedObject.ApplyModifiedProperties();

        }

        private void OnEnable()
        {
            ReInitializeAll();
        }

        private void DrawHistory()
        {
            using (new GUILayout.HorizontalScope("helpbox"))
            {
                void CheckHistory()
                {
                    for (LinkedListNode<VRCExpressionsMenu> node = _menuHistory.First; node != null;)
                    {
                        LinkedListNode<VRCExpressionsMenu> next = node.Next;
                        if (node.Value == null) _menuHistory.Remove(node);
                        node = next;
                    }
                }

                void SetCurrentNode(LinkedListNode<VRCExpressionsMenu> node)
                {
                    if (node.Value == null) return;
                    _currentNode = node;
                    Selection.activeObject = _lastMenu = _currentNode.Value;
                }

                using (new EditorGUI.DisabledScope(_currentNode.Previous == null))
                {
                    using (new EditorGUI.DisabledScope(_currentNode.Previous == null))
                    {
                        if (GUIHelpers.ClickableButton("<<", GUILayout.ExpandWidth(false)))
                        {
                            CheckHistory();
                            SetCurrentNode(_menuHistory.First);
                        }

                        if (GUIHelpers.ClickableButton("<", GUILayout.ExpandWidth(false)))
                        {
                            CheckHistory();
                            SetCurrentNode(_currentNode.Previous);
                        }
                    }
                }

                if (GUIHelpers.ClickableButton(_lastMenu.name, Styles.Label.Centered, GUILayout.ExpandWidth(true)))
                    EditorGUIUtility.PingObject(_lastMenu);

                using (new EditorGUI.DisabledScope(_currentNode.Next == null))
                {
                    if (GUIHelpers.ClickableButton(">", GUILayout.ExpandWidth(false)))
                    {
                        CheckHistory();
                        SetCurrentNode(_currentNode.Next);
                    }

                    if (GUIHelpers.ClickableButton(">>", GUILayout.ExpandWidth(false)))
                    {
                        CheckHistory();
                        SetCurrentNode(_menuHistory.Last);
                    }
                }

            }
        }
        private void DrawHead()
        {
            #region Avatar Selector

            // Generate name string array
            string[] targetsAsString = _validAvatars.Select(t => t.gameObject.name).ToArray();

            // Draw selection
            using (new EditorGUI.DisabledScope(_validAvatars.Length <= 1))
            {
                using (new Container.Horizontal())
                {
                    GUIContent content = new GUIContent("Active Avatar", "The auto-fill and warnings will be based on this avatar's expression parameters");
                    if (_validAvatars.Length >= 1)
                    {
                        using (EditorGUI.ChangeCheckScope change = new EditorGUI.ChangeCheckScope())
                        {
                            int targetIndex = EditorGUILayout.Popup(
                                content,
                                _validAvatars.FindIndex(_avatar),
                                targetsAsString);

                            if (targetIndex == -1)
                                ReInitializeAll();
                            else if (change.changed)
                            {
                                _avatar = _validAvatars[targetIndex];
                                ReInitializeAll();
                            }
                        }
                    }
                    else EditorGUILayout.LabelField(content, new GUIContent("No Avatar Descriptors found"), Styles.Label.LabelDropdown);

                    if (_avatar == null || !_avatar.expressionParameters)
                        GUILayout.Label(new GUIContent(CustomGUIContent.Error) { tooltip = CustomGUIContent.MissingParametersTooltip }, GUILayout.Width(18));
                }
            }

            #endregion
        }

        private void DrawBody()
        {

            if (_controlsList == null)
                InitializeList();

            if (_controlsList.index == -1 && _controlsList.count != 0)
                _controlsList.index = 0;

            _controlsList.DoLayoutList();
            if (_controlsList.count == 0)
                _controlsList.index = -1;

            // EditorGUILayout.Separator();

            SerializedProperty control = _controlsList.index < 0 || _controlsList.index >= _controlsList.count ? null : _controlsList.serializedProperty.GetArrayElementAtIndex(_controlsList.index);
            VRCExpressionParameters expressionParameters = _avatar?.expressionParameters;

            if (Preferences.CompactMode)
                ControlRenderer.DrawControlCompact(control, expressionParameters);
            else
                ControlRenderer.DrawControl(control, expressionParameters);

        }

        private void DrawFooter()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Editor by", Styles.Label.Watermark);
                GUIHelpers.Link("@fox_score", "https://github.com/foxscore");
                GUILayout.Label("&", Styles.Label.Watermark);
                GUIHelpers.Link("@Dreadrith", "https://dreadrith.com/links");
            }
        }

        private void HandleControlEvents()
        {
            if (!_controlsList.HasKeyboardControl()) return;
            if (!_controlsList.TryGetActiveIndex(out int index)) return;
            bool fullMenu = _controlsList.count >= 8;

            bool WarnIfFull()
            {
                if (fullMenu)
                {
                    Debug.LogWarning(CustomGUIContent.MenuFullTooltip);
                    return true;
                }

                return false;
            }

            if (VRCSDKPlusToolbox.HasReceivedAnyDelete())
                DeleteControl(index);

            if (VRCSDKPlusToolbox.HasReceivedCommand(EventCommands.Duplicate))
                if (!WarnIfFull()) DuplicateControl(index);

            if (VRCSDKPlusToolbox.HasReceivedCommand(EventCommands.Copy))
                CopyControl(index);

            if (VRCSDKPlusToolbox.HasReceivedCommand(EventCommands.Cut))
                MoveControl(index);

            if (VRCSDKPlusToolbox.HasReceivedCommand(EventCommands.Paste))
                if (isMoving && !WarnIfFull()) PlaceControl(index);
                else if (CanPasteControl() && !WarnIfFull()) PasteControl(index, true);
        }

        #region Control Methods
        private void CopyControl(int index)
        {
            EditorGUIUtility.systemCopyBuffer =
                Strings.ClipboardPrefixControl +
                JsonUtility.ToJson(((VRCExpressionsMenu)target).controls[index]);
        }

        private static bool CanPasteControl()
        {
            return EditorGUIUtility.systemCopyBuffer.StartsWith(Strings.ClipboardPrefixControl);
        }

        private void PasteControl(int index, bool asNew)
        {
            if (!CanPasteControl()) return;
            if (!asNew)
            {
                VRCExpressionsMenu.Control control = JsonUtility.FromJson<VRCExpressionsMenu.Control>(
                    EditorGUIUtility.systemCopyBuffer.Substring(Strings.ClipboardPrefixControl.Length));

                Undo.RecordObject(target, "Paste control values");
                _lastMenu.controls[index] = control;
                EditorUtility.SetDirty(_lastMenu);
            }
            else
            {
                VRCExpressionsMenu.Control newControl = JsonUtility.FromJson<VRCExpressionsMenu.Control>(
                    EditorGUIUtility.systemCopyBuffer.Substring(Strings.ClipboardPrefixControl.Length));

                Undo.RecordObject(target, "Insert control as new");
                if (_lastMenu.controls.Count <= 0)
                {
                    _lastMenu.controls.Add(newControl);
                    _controlsList.index = 0;
                }
                else
                {
                    int insertIndex = index + 1;
                    if (insertIndex < 0) insertIndex = 0;
                    _lastMenu.controls.Insert(insertIndex, newControl);
                    _controlsList.index = insertIndex;
                }
                EditorUtility.SetDirty(_lastMenu);
            }
        }

        private void DuplicateControl(int index)
        {
            SerializedProperty controlsProp = _controlsList.serializedProperty;
            controlsProp.InsertArrayElementAtIndex(index);
            _controlsList.index = index + 1;

            SerializedProperty newElement = controlsProp.GetArrayElementAtIndex(index + 1);
            string lastName = newElement.FindPropertyRelative("name").stringValue;
            newElement.FindPropertyRelative("name").stringValue = VRCSDKPlusToolbox.GenerateUniqueString(lastName, newName => newName != lastName, false);

            if (Event.current.shift) return;
            SerializedProperty menuParameter = newElement.FindPropertyRelative("parameter");
            if (menuParameter == null) return;
            string parName = menuParameter.FindPropertyRelative("name").stringValue;
            if (string.IsNullOrEmpty(parName)) return;
            VRCExpressionParameters.Parameter matchedParameter = FetchParameter(parName);
            if (matchedParameter == null) return;
            VRCExpressionsMenu.Control.ControlType controlType = newElement.FindPropertyRelative("type").ToControlType();
            if (controlType != VRCExpressionsMenu.Control.ControlType.Button && controlType != VRCExpressionsMenu.Control.ControlType.Toggle) return;

            if (matchedParameter.valueType == VRCExpressionParameters.ValueType.Bool)
            {
                menuParameter.FindPropertyRelative("name").stringValue = VRCSDKPlusToolbox.GenerateUniqueString(parName, s => s != parName, false);
            }
            else
            {
                SerializedProperty controlValueProp = newElement.FindPropertyRelative("value");
                if (Mathf.RoundToInt(controlValueProp.floatValue) == controlValueProp.floatValue)
                    controlValueProp.floatValue++;
            }
        }

        private void DeleteControl(int index)
        {
            if (_controlsList.index == index) _controlsList.index--;
            _controlsList.serializedProperty.DeleteArrayElementAtIndex(index);
        }

        private void MoveControl(int index)
        {
            isMoving = true;
            moveSourceMenu = _lastMenu;
            moveTargetControl = _lastMenu.controls[index];
        }

        private void PlaceControl(int index)
        {
            isMoving = false;
            if (moveSourceMenu && moveTargetControl != null)
            {
                Undo.RecordObject(target, "Move control");
                Undo.RecordObject(moveSourceMenu, "Move control");

                if (_lastMenu.controls.Count <= 0)
                    _lastMenu.controls.Add(moveTargetControl);
                else
                {
                    int insertIndex = index + 1;
                    if (insertIndex < 0) insertIndex = 0;
                    _lastMenu.controls.Insert(insertIndex, moveTargetControl);
                    moveSourceMenu.controls.Remove(moveTargetControl);
                }

                EditorUtility.SetDirty(moveSourceMenu);
                EditorUtility.SetDirty(target);

                if (Event.current.shift) Selection.activeObject = moveSourceMenu;
            }
        }

        #endregion

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Compact Mode"), Preferences.CompactMode, ToggleCompactMode);
        }

        private static void ToggleCompactMode()
        {
            Preferences.CompactMode = !Preferences.CompactMode;
        }

        [MenuItem("CONTEXT/VRCExpressionsMenu/[SDK+] Toggle Editor", false, 899)]
        private static void ToggleEditor()
        {
            editorActive = !editorActive;
            Type targetType = Helpers.ExtendedGetType("VRCExpressionsMenu");
            if (targetType == null)
            {
                Debug.LogError("[VRCSDK+] VRCExpressionsMenu was not found! Could not apply custom editor.");
                return;
            }
            if (editorActive) EditorHelpers.OverrideEditor(targetType, typeof(VRCMenuPlus));
            else
            {
                Type menuEditor = Helpers.ExtendedGetType("VRCExpressionsMenuEditor");
                if (menuEditor == null)
                {
                    Debug.LogWarning("[VRCSDK+] VRCExpressionsMenuEditor was not found! Could not apply custom editor.");
                    return;
                }

                EditorHelpers.OverrideEditor(targetType, menuEditor);
            }
            //else OverrideEditor(typeof(VRCExpressionsMenu), Type.GetType("VRCExpressionsMenuEditor, Assembly-CSharp-Editor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"));
        }

        private static class ControlRenderer
        {
            private const float IconSize = 96;
            private const float IconSpace = IconSize + 3;

            private const float CompactIconSize = 60;
            private const float CompactIconSpace = CompactIconSize + 3;

            public static void DrawControl(SerializedProperty property, VRCExpressionParameters parameters)
            {
                MainContainer(property);
                EditorGUILayout.Separator();
                ParameterContainer(property, parameters);

                if (property != null)
                {
                    EditorGUILayout.Separator();

                    switch ((VRCExpressionsMenu.Control.ControlType)property.FindPropertyRelative("type").intValue)
                    {
                        case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                            RadialContainer(property, parameters);
                            break;
                        case VRCExpressionsMenu.Control.ControlType.SubMenu:
                            SubMenuContainer(property);
                            break;
                        case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                            TwoAxisParametersContainer(property, parameters);
                            EditorGUILayout.Separator();
                            AxisCustomisationContainer(property);
                            break;
                        case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                            FourAxisParametersContainer(property, parameters);
                            EditorGUILayout.Separator();
                            AxisCustomisationContainer(property);
                            break;
                    }
                }
            }

            public static void DrawControlCompact(SerializedProperty property, VRCExpressionParameters parameters)
            {
                CompactMainContainer(property, parameters);

                if (property != null)
                {
                    switch ((VRCExpressionsMenu.Control.ControlType)property.FindPropertyRelative("type").intValue)
                    {
                        case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                            RadialContainer(property, parameters);
                            break;
                        case VRCExpressionsMenu.Control.ControlType.SubMenu:
                            SubMenuContainer(property);
                            break;
                        case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                            CompactTwoAxisParametersContainer(property, parameters);
                            //AxisCustomisationContainer(property);
                            break;
                        case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                            CompactFourAxisParametersContainer(property, parameters);
                            //AxisCustomisationContainer(property);
                            break;
                    }
                }
            }

            #region Main container

            private static void MainContainer(SerializedProperty property)
            {
                Rect rect = EditorGUILayout
                    .GetControlRect(false, 147);
                Container.GUIBox(ref rect);

                Rect nameRect = new Rect(rect.x, rect.y, rect.width - IconSpace, 21);
                Rect typeRect = new Rect(rect.x, rect.y + 24, rect.width - IconSpace, 21);
                Rect baseStyleRect = new Rect(rect.x, rect.y + 48, rect.width - IconSpace, 21);
                Rect iconRect = new Rect(rect.x + rect.width - IconSize, rect.y, IconSize, IconSize);
                Rect helpRect = new Rect(rect.x, rect.y + IconSpace, rect.width, 42);

                DrawName(nameRect, property, true);
                DrawType(typeRect, property, true);
                DrawStyle(baseStyleRect, property, true);
                DrawIcon(iconRect, property);
                DrawHelp(helpRect, property);
            }

            private static void CompactMainContainer(SerializedProperty property, VRCExpressionParameters parameters)
            {
                Rect rect = EditorGUILayout.GetControlRect(false, 66);
                Container.GUIBox(ref rect);

                float halfWidth = (rect.width - CompactIconSpace) / 2;
                Rect nameRect = new Rect(rect.x, rect.y, halfWidth - 3, 18);
                Rect typeRect = new Rect(rect.x + halfWidth, rect.y, halfWidth - 19, 18);
                Rect helpRect = new Rect(typeRect.x + typeRect.width + 1, rect.y, 18, 18);
                Rect parameterRect = new Rect(rect.x, rect.y + 21, rect.width - CompactIconSpace, 18);
                Rect styleRect = new Rect(rect.x, rect.y + 42, rect.width - CompactIconSize, 18);
                Rect iconRect = new Rect(rect.x + rect.width - CompactIconSize, rect.y, CompactIconSize, CompactIconSize);

                DrawName(nameRect, property, false);
                DrawType(typeRect, property, false);
                DrawStyle(styleRect, property, false);

                if (property != null)
                    GUI.Label(helpRect, new GUIContent(CustomGUIContent.Help) { tooltip = GetHelpMessage(property) }, GUIStyle.none);

                ParameterContainer(property, parameters, parameterRect);

                DrawIcon(iconRect, property);

                // ToDo Draw error help if Parameter not found
            }

            private static void DrawName(Rect rect, SerializedProperty property, bool drawLabel)
            {
                if (property == null)
                {
                    Placeholder.GUI(rect);
                    return;
                }

                SerializedProperty name = property.FindPropertyRelative("name");

                if (drawLabel)
                {
                    Rect label = new Rect(rect.x, rect.y, 100, rect.height);
                    rect = new Rect(rect.x + 103, rect.y, rect.width - 103, rect.height);

                    GUI.Label(label, "Name");
                }

                name.stringValue = EditorGUI.TextField(rect, name.stringValue);
                if (string.IsNullOrEmpty(name.stringValue)) GUI.Label(rect, "Name", Styles.Label.PlaceHolder);
            }

            private static void DrawType(Rect rect, SerializedProperty property, bool drawLabel)
            {
                if (property == null)
                {
                    Placeholder.GUI(rect);
                    return;
                }

                if (drawLabel)
                {
                    Rect label = new Rect(rect.x, rect.y, 100, rect.height);
                    rect = new Rect(rect.x + 103, rect.y, rect.width - 103, rect.height);

                    GUI.Label(label, "Type");
                }

                VRCExpressionsMenu.Control.ControlType controlType = property.FindPropertyRelative("type").ToControlType();
                VRCExpressionsMenu.Control.ControlType newType = (VRCExpressionsMenu.Control.ControlType)EditorGUI.EnumPopup(rect, controlType);

                if (newType != controlType)
                    ConversionEntry(property, controlType, newType);
            }

            private static void DrawStyle(Rect rect, SerializedProperty property, bool drawLabel)
            {
                const float toggleSize = 21;

                if (property == null)
                {
                    Placeholder.GUI(rect);
                    return;
                }

                if (drawLabel)
                {
                    Rect labelRect = new Rect(rect.x, rect.y, 100, rect.height);
                    rect = new Rect(rect.x + 103, rect.y, rect.width - 103, rect.height);
                    GUI.Label(labelRect, "Style");
                }

                Rect colorRect = new Rect(rect.x, rect.y, rect.width - (toggleSize + 3) * 2, rect.height);
                Rect boldRect = new Rect(colorRect.x + colorRect.width, rect.y, toggleSize, rect.height);
                Rect italicRect = new Rect(boldRect); italicRect.x += italicRect.width + 3; boldRect.width = toggleSize;
                string rawName = property.FindPropertyRelative("name").stringValue;
                Color textColor = Color.white;

                bool isBold = rawName.Contains("<b>") && rawName.Contains("</b>");
                bool isItalic = rawName.Contains("<i>") && rawName.Contains("</i>");
                Match m = Regex.Match(rawName, @"<color=(#[0-9|A-F]{6,8})>");
                if (m.Success)
                {
                    if (rawName.Contains("</color>"))
                    {
                        if (ColorUtility.TryParseHtmlString(m.Groups[1].Value, out Color newColor))
                            textColor = newColor;

                    }
                }


                EditorGUI.BeginChangeCheck();
                textColor = EditorGUI.ColorField(colorRect, textColor);
                if (EditorGUI.EndChangeCheck())
                {
                    rawName = Regex.Replace(rawName, @"</?color=?.*?>", string.Empty);
                    rawName = $"<color=#{ColorUtility.ToHtmlStringRGB(textColor)}>{rawName}</color>";
                }

                void SetCharTag(char c, bool state)
                {
                    rawName = !state ?
                        Regex.Replace(rawName, $@"</?{c}>", string.Empty) :
                        $"<{c}>{rawName}</{c}>";
                }
                GUIHelpers.
                                    MakeRectLinkCursor(boldRect);
                EditorGUI.BeginChangeCheck();
                isBold = GUI.Toggle(boldRect, isBold, new GUIContent("<b>b</b>", "Bold"), Styles.letterButton);
                if (EditorGUI.EndChangeCheck()) SetCharTag('b', isBold);
                GUIHelpers.
                                    MakeRectLinkCursor(italicRect);
                EditorGUI.BeginChangeCheck();
                isItalic = GUI.Toggle(italicRect, isItalic, new GUIContent("<i>i</i>", "Italic"), Styles.letterButton);
                if (EditorGUI.EndChangeCheck()) SetCharTag('i', isItalic);


                property.FindPropertyRelative("name").stringValue = rawName;
            }

            private static void DrawIcon(Rect rect, SerializedProperty property)
            {
                if (property == null)
                    Placeholder.GUI(rect);
                else
                {
                    SerializedProperty value = property.FindPropertyRelative("icon");

                    value.objectReferenceValue = EditorGUI.ObjectField(
                        rect,
                        string.Empty,
                        value.objectReferenceValue,
                        typeof(Texture2D),
                        false
                    );
                }
            }

            private static void DrawHelp(Rect rect, SerializedProperty property)
            {
                if (property == null)
                {
                    Placeholder.GUI(rect);
                    return;
                }

                string message = GetHelpMessage(property);
                EditorGUI.HelpBox(rect, message, MessageType.Info);
            }

            private static string GetHelpMessage(SerializedProperty property)
            {
                switch (property.FindPropertyRelative("type").ToControlType())
                {
                    case VRCExpressionsMenu.Control.ControlType.Button:
                        return "Click or hold to activate. The button remains active for a minimum 0.2s.\nWhile active the (Parameter) is set to (Value).\nWhen inactive the (Parameter) is reset to zero.";
                    case VRCExpressionsMenu.Control.ControlType.Toggle:
                        return "Click to toggle on or off.\nWhen turned on the (Parameter) is set to (Value).\nWhen turned off the (Parameter) is reset to zero.";
                    case VRCExpressionsMenu.Control.ControlType.SubMenu:
                        return "Opens another expression menu.\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                    case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                        return "Puppet menu that maps the joystick to two parameters (-1 to +1).\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                    case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                        return "Puppet menu that maps the joystick to four parameters (0 to 1).\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                    case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                        return "Puppet menu that sets a value based on joystick rotation. (0 to 1)\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                    default:
                        return "ERROR: Unable to load message - Invalid control type";
                }
            }

            #endregion

            #region Type Conversion

            private static void ConversionEntry(SerializedProperty property, VRCExpressionsMenu.Control.ControlType tOld, VRCExpressionsMenu.Control.ControlType tNew)
            {
                // Is old one button / toggle, and new one not?
                if (
                        (tOld == VRCExpressionsMenu.Control.ControlType.Button || tOld == VRCExpressionsMenu.Control.ControlType.Toggle) &&
                        (tNew != VRCExpressionsMenu.Control.ControlType.Button && tNew != VRCExpressionsMenu.Control.ControlType.Toggle)
                    )
                    // Reset parameter
                    property.FindPropertyRelative("parameter").FindPropertyRelative("name").stringValue = "";
                else if (
                    (tOld != VRCExpressionsMenu.Control.ControlType.Button && tOld != VRCExpressionsMenu.Control.ControlType.Toggle) &&
                    (tNew == VRCExpressionsMenu.Control.ControlType.Button || tNew == VRCExpressionsMenu.Control.ControlType.Toggle)
                )
                    SetupSubParameters(property, tNew);

                // Is either a submenu
                if (tOld == VRCExpressionsMenu.Control.ControlType.SubMenu || tNew == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    SetupSubParameters(property, tNew);

                // Is either Puppet)
                if (IsPuppetConversion(tOld, tNew))
                    DoPuppetConversion(property, tNew);
                else if (
                    tNew == VRCExpressionsMenu.Control.ControlType.RadialPuppet ||
                    tNew == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ||
                    tNew == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet
                )
                    SetupSubParameters(property, tNew);

                property.FindPropertyRelative("type").enumValueIndex = tNew.GetEnumValueIndex();
            }

            private static bool IsPuppetConversion(VRCExpressionsMenu.Control.ControlType tOld, VRCExpressionsMenu.Control.ControlType tNew)
            {
                return (
                           tOld == VRCExpressionsMenu.Control.ControlType.RadialPuppet ||
                           tOld == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ||
                           tOld == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet
                       ) &&
                       (
                           tNew == VRCExpressionsMenu.Control.ControlType.RadialPuppet ||
                           tNew == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ||
                           tNew == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet
                       );
            }

            private static void DoPuppetConversion(SerializedProperty property, VRCExpressionsMenu.Control.ControlType tNew)
            {
                SerializedProperty subParameters = property.FindPropertyRelative("subParameters");
                string sub0 = subParameters.GetArrayElementAtIndex(0).FindPropertyRelative("name").stringValue;
                string sub1 = subParameters.arraySize > 1
                    ? subParameters.GetArrayElementAtIndex(1).FindPropertyRelative("name").stringValue
                    : string.Empty;

                subParameters.ClearArray();
                subParameters.InsertArrayElementAtIndex(0);
                subParameters.GetArrayElementAtIndex(0).FindPropertyRelative("name").stringValue = sub0;

                // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                switch (tNew)
                {
                    case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                        subParameters.InsertArrayElementAtIndex(1);
                        subParameters.GetArrayElementAtIndex(1).FindPropertyRelative("name").stringValue = sub1;
                        break;

                    case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                        subParameters.InsertArrayElementAtIndex(1);
                        subParameters.GetArrayElementAtIndex(1).FindPropertyRelative("name").stringValue = sub1;
                        subParameters.InsertArrayElementAtIndex(2);
                        subParameters.GetArrayElementAtIndex(2).FindPropertyRelative("name").stringValue = "";
                        subParameters.InsertArrayElementAtIndex(3);
                        subParameters.GetArrayElementAtIndex(3).FindPropertyRelative("name").stringValue = "";
                        break;
                }
            }

            private static void SetupSubParameters(SerializedProperty property, VRCExpressionsMenu.Control.ControlType type)
            {
                SerializedProperty subParameters = property.FindPropertyRelative("subParameters");
                subParameters.ClearArray();

                switch (type)
                {
                    case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    case VRCExpressionsMenu.Control.ControlType.SubMenu:
                        subParameters.InsertArrayElementAtIndex(0);
                        break;
                    case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                        subParameters.InsertArrayElementAtIndex(0);
                        subParameters.InsertArrayElementAtIndex(1);
                        break;
                    case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                        subParameters.InsertArrayElementAtIndex(0);
                        subParameters.InsertArrayElementAtIndex(1);
                        subParameters.InsertArrayElementAtIndex(2);
                        subParameters.InsertArrayElementAtIndex(3);
                        break;
                }
            }

            #endregion

            /*static void DrawParameterNotFound(string parameter)
            {
                EditorGUILayout.HelpBox(
                    $"Parameter not found on the active avatar descriptor ({parameter})",
                    MessageType.Warning
                );
            }*/



            #region BuildParameterArray

            private static void BuildParameterArray(
                string name,
                VRCExpressionParameters parameters,
                out int index,
                out string[] parametersAsString
            )
            {
                index = -2;
                if (!parameters)
                {
                    parametersAsString = Array.Empty<string>();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    for (int i = 0; i < parameters.parameters.Length; i++)
                    {
                        if (parameters.parameters[i].name != name) continue;

                        index = i + 1;
                        break;
                    }
                }
                else
                    index = -1;

                parametersAsString = new string[parameters.parameters.Length + 1];
                parametersAsString[0] = "[None]";
                for (int i = 0; i < parameters.parameters.Length; i++)
                {
                    switch (parameters.parameters[i].valueType)
                    {
                        case VRCExpressionParameters.ValueType.Int:
                            parametersAsString[i + 1] = $"{parameters.parameters[i].name} [int]";
                            break;
                        case VRCExpressionParameters.ValueType.Float:
                            parametersAsString[i + 1] = $"{parameters.parameters[i].name} [float]";
                            break;
                        case VRCExpressionParameters.ValueType.Bool:
                            parametersAsString[i + 1] = $"{parameters.parameters[i].name} [bool]";
                            break;
                    }
                }
            }

            private static void BuildParameterArray(
                string name,
                VRCExpressionParameters parameters,
                out int index,
                out VRCExpressionParameters.Parameter[] filteredParameters,
                out string[] filteredParametersAsString,
                VRCExpressionParameters.ValueType filter
            )
            {
                index = -2;
                if (!parameters)
                {
                    filteredParameters = Array.Empty<VRCExpressionParameters.Parameter>();
                    filteredParametersAsString = Array.Empty<string>();
                    return;
                }

                filteredParameters = parameters.parameters.Where(p => p.valueType == filter).ToArray();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    for (int i = 0; i < filteredParameters.Length; i++)
                    {
                        if (filteredParameters[i].name != name) continue;

                        index = i + 1;
                        break;
                    }
                }
                else
                    index = -1;

                filteredParametersAsString = new string[filteredParameters.Length + 1];
                filteredParametersAsString[0] = "[None]";
                for (int i = 0; i < filteredParameters.Length; i++)
                {
                    switch (filteredParameters[i].valueType)
                    {
                        case VRCExpressionParameters.ValueType.Int:
                            filteredParametersAsString[i + 1] = $"{filteredParameters[i].name} [int]";
                            break;
                        case VRCExpressionParameters.ValueType.Float:
                            filteredParametersAsString[i + 1] = $"{filteredParameters[i].name} [float]";
                            break;
                        case VRCExpressionParameters.ValueType.Bool:
                            filteredParametersAsString[i + 1] = $"{filteredParameters[i].name} [bool]";
                            break;
                    }
                }
            }

            #endregion

            #region DrawParameterSelector

            private struct ParameterSelectorOptions
            {
                public Action extraGUI;
                public Rect rect;
                public bool required;

                public ParameterSelectorOptions(Rect rect, bool required, Action extraGUI = null)
                {
                    this.required = required;
                    this.rect = rect;
                    this.extraGUI = extraGUI;
                }

                public ParameterSelectorOptions(Rect rect, Action extraGUI = null)
                {
                    required = false;
                    this.rect = rect;
                    this.extraGUI = extraGUI;
                }

                public ParameterSelectorOptions(bool required, Action extraGUI = null)
                {
                    this.required = required;
                    rect = default;
                    this.extraGUI = extraGUI;
                }
            }

            private static bool DrawParameterSelector(
                string label,
                SerializedProperty property,
                VRCExpressionParameters parameters,
                ParameterSelectorOptions options = default
            )
            {
                BuildParameterArray(
                    property.FindPropertyRelative("name").stringValue,
                    parameters,
                    out int index,
                    out string[] parametersAsString
                );
                return DrawParameterSelection__BASE(
                    label,
                    property,
                    index,
                    parameters,
                    parameters?.parameters,
                    parametersAsString,
                    false,
                    options
                );
            }

            private static bool DrawParameterSelector(
                string label,
                SerializedProperty property,
                VRCExpressionParameters parameters,
                VRCExpressionParameters.ValueType filter,
                ParameterSelectorOptions options = default
            )
            {
                BuildParameterArray(
                    property.FindPropertyRelative("name").stringValue,
                    parameters,
                    out int index,
                    out VRCExpressionParameters.Parameter[] filteredParameters,
                    out string[] parametersAsString,
                    filter
                );
                return DrawParameterSelection__BASE(
                    label,
                    property,
                    index,
                    parameters,
                    filteredParameters,
                    parametersAsString,
                    true,
                    options
                );
            }

            private static bool DrawParameterSelection__BASE(
                string label,
                SerializedProperty property,
                int index,
                VRCExpressionParameters targetParameters,
                VRCExpressionParameters.Parameter[] parameters,
                string[] parametersAsString,
                bool isFiltered,
                ParameterSelectorOptions options
            )
            {
                bool isEmpty = index == -1;
                bool isMissing = index == -2;
                bool willWarn = isMissing || options.required && isEmpty;
                string parameterName = property.FindPropertyRelative("name").stringValue;
                string warnMsg = targetParameters ? isMissing ? isFiltered ?
                            $"Parameter ({parameterName}) not found or invalid" :
                            $"Parameter ({parameterName}) not found on the active avatar descriptor" :
                        "Parameter is blank. Control may be dysfunctional." :
                    CustomGUIContent.MissingParametersTooltip;

                bool rectNotProvided = options.rect == default;
                using (new GUILayout.HorizontalScope())
                {
                    const float CONTENT_ADD_WIDTH = 50;
                    const float CONTENT_WARN_WIDTH = 18;
                    const float CONTENT_DROPDOWN_WIDTH = 20;
                    //const float CONTENT_TEXT_FIELD_PORTION = 0.25f;
                    float missingFullWidth = CONTENT_ADD_WIDTH + CONTENT_WARN_WIDTH + 2;

                    bool hasLabel = !string.IsNullOrEmpty(label);

                    if (rectNotProvided) options.rect = EditorGUILayout.GetControlRect(false, 18);

                    SerializedProperty name = property.FindPropertyRelative("name");

                    Rect labelRect = new Rect(options.rect) { width = hasLabel ? 120 : 0 };
                    Rect textfieldRect = new Rect(labelRect) { x = labelRect.x + labelRect.width, width = options.rect.width - labelRect.width - CONTENT_DROPDOWN_WIDTH - 2 };
                    Rect dropdownRect = new Rect(textfieldRect) { x = textfieldRect.x + textfieldRect.width, width = CONTENT_DROPDOWN_WIDTH };
                    Rect addRect = Rect.zero;
                    Rect warnRect = Rect.zero;

                    if (targetParameters && isMissing)
                    {
                        textfieldRect.width -= missingFullWidth;
                        dropdownRect.x -= missingFullWidth;
                        addRect = new Rect(options.rect) { x = textfieldRect.x + textfieldRect.width + CONTENT_DROPDOWN_WIDTH + 2, width = CONTENT_ADD_WIDTH };
                        warnRect = new Rect(addRect) { x = addRect.x + addRect.width, width = CONTENT_WARN_WIDTH };
                    }
                    else if (!targetParameters || options.required && isEmpty)
                    {
                        textfieldRect.width -= CONTENT_WARN_WIDTH;
                        dropdownRect.x -= CONTENT_WARN_WIDTH;
                        warnRect = new Rect(dropdownRect) { x = dropdownRect.x + dropdownRect.width, width = CONTENT_WARN_WIDTH };
                    }

                    if (hasLabel) GUI.Label(labelRect, label);
                    using (new EditorGUI.DisabledScope(!targetParameters || parametersAsString.Length <= 1))
                    {
                        int newIndex = EditorGUI.Popup(dropdownRect, string.Empty, index, parametersAsString);
                        if (index != newIndex)
                            name.stringValue = newIndex == 0 ? string.Empty : parameters[newIndex - 1].name;
                    }

                    name.stringValue = EditorGUI.TextField(textfieldRect, name.stringValue);
                    if (string.IsNullOrEmpty(name.stringValue)) GUI.Label(textfieldRect, "Parameter", Styles.Label.PlaceHolder);
                    if (willWarn) GUI.Label(warnRect, new GUIContent(CustomGUIContent.Warn) { tooltip = warnMsg });

                    if (isMissing)
                    {
                        int dummy;

                        if (!isFiltered)
                        {
                            dummy = EditorGUI.Popup(addRect, -1, Enum.GetNames(typeof(VRCExpressionParameters.ValueType)));

                            addRect.x += 3;
                            GUI.Label(addRect, "Add");
                        }
                        else dummy = GUI.Button(addRect, "Add") ? 1 : -1;

                        if (dummy != -1)
                        {
                            SerializedObject so = new SerializedObject(targetParameters);
                            SerializedProperty param = so.FindProperty("parameters");
                            SerializedProperty prop = param.GetArrayElementAtIndex(param.arraySize++);
                            prop.FindPropertyRelative("valueType").enumValueIndex = dummy;
                            prop.FindPropertyRelative("name").stringValue = name.stringValue;
                            prop.FindPropertyRelative("saved").boolValue = true;
                            try { prop.FindPropertyRelative("networkSynced").boolValue = true; } catch { }
                            so.ApplyModifiedProperties();
                        }
                    }

                    options.extraGUI?.Invoke();
                }

                return isMissing;
            }

            #endregion

            #region Parameter conainer

            private static void ParameterContainer(
                SerializedProperty property,
                VRCExpressionParameters parameters,
                Rect rect = default
            )
            {
                bool rectProvided = rect != default;

                if (property?.FindPropertyRelative("parameter") == null)
                {
                    if (rectProvided)
                        Placeholder.GUI(rect);
                    else
                    {
                        Container.BeginLayout();
                        Placeholder.GUILayout(18);
                        Container.EndLayout();
                    }
                }
                else
                {
                    if (!rectProvided) Container.BeginLayout();

                    float CONTENT_VALUE_SELECTOR_WIDTH = 50;
                    Rect selectorRect = default;
                    Rect valueRect = default;

                    if (rectProvided)
                    {
                        selectorRect = new Rect(rect.x, rect.y, rect.width - CONTENT_VALUE_SELECTOR_WIDTH - 3,
                            rect.height);
                        valueRect = new Rect(selectorRect.x + selectorRect.width + 3, rect.y,
                            CONTENT_VALUE_SELECTOR_WIDTH, rect.height);
                    }

                    SerializedProperty parameter = property.FindPropertyRelative("parameter");

                    VRCExpressionsMenu.Control.ControlType t = (VRCExpressionsMenu.Control.ControlType)property.FindPropertyRelative("type").intValue;
                    bool isRequired = t == VRCExpressionsMenu.Control.ControlType.Button || t == VRCExpressionsMenu.Control.ControlType.Toggle;
                    DrawParameterSelector(rectProvided ? string.Empty : "Parameter", parameter, parameters, new ParameterSelectorOptions()
                    {
                        rect = selectorRect,
                        required = isRequired,
                        extraGUI = () =>
                        {
                            #region Value selector

                            SerializedProperty parameterName = parameter.FindPropertyRelative("name");
                            VRCExpressionParameters.Parameter param = parameters?.parameters.FirstOrDefault(p => p.name == parameterName.stringValue);

                            // Check what type the parameter is

                            SerializedProperty value = property.FindPropertyRelative("value");
                            switch (param?.valueType)
                            {
                                case VRCExpressionParameters.ValueType.Int:
                                    value.floatValue = Mathf.Clamp(rectProvided ?
                                        EditorGUI.IntField(valueRect, (int)value.floatValue) :
                                        EditorGUILayout.IntField((int)value.floatValue, GUILayout.Width(CONTENT_VALUE_SELECTOR_WIDTH)), 0f, 255f);
                                    break;

                                case VRCExpressionParameters.ValueType.Float:
                                    value.floatValue = Mathf.Clamp(rectProvided ?
                                        EditorGUI.FloatField(valueRect, value.floatValue) :
                                        EditorGUILayout.FloatField(value.floatValue, GUILayout.Width(CONTENT_VALUE_SELECTOR_WIDTH)), -1, 1);
                                    break;

                                case VRCExpressionParameters.ValueType.Bool:
                                    using (new EditorGUI.DisabledScope(true))
                                    {
                                        if (rectProvided) EditorGUI.TextField(valueRect, string.Empty);
                                        else EditorGUILayout.TextField(string.Empty, GUILayout.Width(CONTENT_VALUE_SELECTOR_WIDTH));
                                    }

                                    value.floatValue = 1f;
                                    break;

                                default:
                                    value.floatValue = Mathf.Clamp(rectProvided ?
                                        EditorGUI.FloatField(valueRect, value.floatValue) :
                                        EditorGUILayout.FloatField(value.floatValue, GUILayout.Width(CONTENT_VALUE_SELECTOR_WIDTH)), -1, 255);
                                    break;
                            }
                            #endregion
                        }
                    });

                    if (!rectProvided)
                        Container.EndLayout();
                }
            }

            #endregion

            #region Miscellaneous containers

            private static void RadialContainer(SerializedProperty property, VRCExpressionParameters parameters)
            {
                using (new Container.Vertical())
                    DrawParameterSelector(
                        "Rotation",
                        property.FindPropertyRelative("subParameters").GetArrayElementAtIndex(0),
                        parameters,
                        VRCExpressionParameters.ValueType.Float,
                        new ParameterSelectorOptions(true)
                    );
            }

            private static void SubMenuContainer(SerializedProperty property)
            {
                using (new Container.Vertical())
                {
                    SerializedProperty subMenu = property.FindPropertyRelative("subMenu");
                    SerializedProperty nameProperty = property.FindPropertyRelative("name");
                    bool emptySubmenu = subMenu.objectReferenceValue == null;

                    using (new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(subMenu);
                        if (emptySubmenu)
                        {
                            using (new EditorGUI.DisabledScope(_currentNode?.Value == null))
                                if (GUILayout.Button("New", GUILayout.Width(40)))
                                {
                                    VRCExpressionsMenu m = _currentNode.Value;
                                    string path = AssetDatabase.GetAssetPath(m);
                                    if (string.IsNullOrEmpty(path))
                                        path = $"Assets/{m.name}.asset";
                                    string parentPath = Path.GetDirectoryName(path);
                                    string assetName = string.IsNullOrEmpty(nameProperty?.stringValue) ? $"{m.name} SubMenu.asset" : $"{nameProperty.stringValue} Menu.asset";
                                    string newMenuPath = VRCSDKPlusToolbox.ReadyAssetPath(parentPath, assetName, true);

                                    VRCExpressionsMenu newMenu = CreateInstance<VRCExpressionsMenu>();
                                    if (newMenu.controls == null)
                                        newMenu.controls = new List<VRCExpressionsMenu.Control>();

                                    AssetDatabase.CreateAsset(newMenu, newMenuPath);
                                    subMenu.objectReferenceValue = newMenu;
                                }
                            GUILayout.Label(new GUIContent(CustomGUIContent.Warn) { tooltip = "Submenu is empty. This control has no use." }, Styles.icon);
                        }
                        using (new EditorGUI.DisabledScope(emptySubmenu))
                        {
                            if (GUIHelpers.ClickableButton(CustomGUIContent.Folder, Styles.icon))
                                Selection.activeObject = subMenu.objectReferenceValue;
                            if (GUIHelpers.ClickableButton(CustomGUIContent.Clear, Styles.icon))
                                subMenu.objectReferenceValue = null;
                        }
                    }
                }
            }

            private static void CompactTwoAxisParametersContainer(SerializedProperty property, VRCExpressionParameters parameters)
            {
                using (new Container.Vertical())
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        using (new GUILayout.HorizontalScope())
                            GUILayout.Label("Axis Parameters", Styles.Label.Centered);


                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Name -", Styles.Label.Centered);
                            GUILayout.Label("Name +", Styles.Label.Centered);
                        }
                    }

                    SerializedProperty subs = property.FindPropertyRelative("subParameters");
                    SerializedProperty sub0 = subs.GetArrayElementAtIndex(0);
                    SerializedProperty sub1 = subs.GetArrayElementAtIndex(1);

                    SerializedProperty labels = SafeGetLabels(property);

                    using (new GUILayout.HorizontalScope())
                    {
                        Rect rect = EditorGUILayout.GetControlRect();
                        using (new GUILayout.HorizontalScope())
                        {
                            DrawParameterSelector(
                                "Horizontal",
                                sub0,
                                parameters,
                                VRCExpressionParameters.ValueType.Float,
                                new ParameterSelectorOptions(rect, true)
                            );
                        }

                        using (new GUILayout.HorizontalScope())
                        {
                            DrawLabel(labels.GetArrayElementAtIndex(0), "Left");
                            DrawLabel(labels.GetArrayElementAtIndex(1), "Right");
                        }
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        Rect rect = EditorGUILayout.GetControlRect();
                        using (new GUILayout.HorizontalScope())
                        {
                            DrawParameterSelector(
                                "Vertical",
                                sub1,
                                parameters,
                                VRCExpressionParameters.ValueType.Float,
                                new ParameterSelectorOptions(rect, true)
                            );
                        }

                        using (new GUILayout.HorizontalScope())
                        {
                            DrawLabel(labels.GetArrayElementAtIndex(2), "Down");
                            DrawLabel(labels.GetArrayElementAtIndex(3), "Up");
                        }
                    }
                }

            }

            private static void CompactFourAxisParametersContainer(SerializedProperty property, VRCExpressionParameters parameters)
            {
                using (new Container.Vertical())
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        Rect headerRect = EditorGUILayout.GetControlRect();
                        Rect r1 = new Rect(headerRect) { width = headerRect.width / 2 };
                        Rect r2 = new Rect(r1) { x = r1.x + r1.width };
                        GUI.Label(r1, "Axis Parameters", Styles.Label.Centered);
                        GUI.Label(r2, "Name", Styles.Label.Centered);
                    }

                    SerializedProperty subs = property.FindPropertyRelative("subParameters");
                    SerializedProperty sub0 = subs.GetArrayElementAtIndex(0);
                    SerializedProperty sub1 = subs.GetArrayElementAtIndex(1);
                    SerializedProperty sub2 = subs.GetArrayElementAtIndex(2);
                    SerializedProperty sub3 = subs.GetArrayElementAtIndex(3);

                    SerializedProperty labels = SafeGetLabels(property);

                    using (new GUILayout.HorizontalScope())
                    {
                        Rect r = EditorGUILayout.GetControlRect();
                        using (new GUILayout.HorizontalScope())
                        {
                            DrawParameterSelector(
                                "Up",
                                sub0,
                                parameters,
                                VRCExpressionParameters.ValueType.Float,
                                new ParameterSelectorOptions(r, true)
                            );
                        }

                        using (new GUILayout.HorizontalScope())
                            DrawLabel(labels.GetArrayElementAtIndex(0), "Name");
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        Rect r = EditorGUILayout.GetControlRect();
                        using (new GUILayout.HorizontalScope())
                        {
                            DrawParameterSelector(
                                "Right",
                                sub1,
                                parameters,
                                VRCExpressionParameters.ValueType.Float,
                                new ParameterSelectorOptions(r, true)
                            );
                        }

                        using (new GUILayout.HorizontalScope())
                            DrawLabel(labels.GetArrayElementAtIndex(1), "Name");
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        Rect r = EditorGUILayout.GetControlRect();
                        using (new GUILayout.HorizontalScope())
                        {
                            DrawParameterSelector(
                                "Down",
                                sub2,
                                parameters,
                                VRCExpressionParameters.ValueType.Float,
                                new ParameterSelectorOptions(r, true)
                            );
                        }

                        using (new GUILayout.HorizontalScope())
                            DrawLabel(labels.GetArrayElementAtIndex(2), "Name");
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        Rect r = EditorGUILayout.GetControlRect();
                        using (new GUILayout.HorizontalScope())
                        {
                            DrawParameterSelector(
                                "Left",
                                sub3,
                                parameters,
                                VRCExpressionParameters.ValueType.Float,
                                new ParameterSelectorOptions(r, true)
                            );
                        }

                        using (new GUILayout.HorizontalScope())
                            DrawLabel(labels.GetArrayElementAtIndex(3), "Name");
                    }
                }

            }

            private static void TwoAxisParametersContainer(SerializedProperty property, VRCExpressionParameters parameters)
            {
                Container.BeginLayout();

                GUILayout.Label("Axis Parameters", Styles.Label.Centered);

                SerializedProperty subs = property.FindPropertyRelative("subParameters");
                SerializedProperty sub0 = subs.GetArrayElementAtIndex(0);
                SerializedProperty sub1 = subs.GetArrayElementAtIndex(1);

                DrawParameterSelector(
                    "Horizontal",
                    sub0,
                    parameters,
                    VRCExpressionParameters.ValueType.Float,
                    new ParameterSelectorOptions(true)
                );

                DrawParameterSelector(
                    "Vertical",
                    sub1,
                    parameters,
                    VRCExpressionParameters.ValueType.Float,
                    new ParameterSelectorOptions(true)
                );

                Container.EndLayout();
            }

            private static void FourAxisParametersContainer(SerializedProperty property, VRCExpressionParameters parameters)
            {
                Container.BeginLayout("Axis Parameters");

                SerializedProperty subs = property.FindPropertyRelative("subParameters");
                SerializedProperty sub0 = subs.GetArrayElementAtIndex(0);
                SerializedProperty sub1 = subs.GetArrayElementAtIndex(1);
                SerializedProperty sub2 = subs.GetArrayElementAtIndex(2);
                SerializedProperty sub3 = subs.GetArrayElementAtIndex(3);

                DrawParameterSelector(
                    "Up",
                    sub0,
                    parameters,
                    VRCExpressionParameters.ValueType.Float,
                    new ParameterSelectorOptions(true)
                );

                DrawParameterSelector(
                    "Right",
                    sub1,
                    parameters,
                    VRCExpressionParameters.ValueType.Float,
                    new ParameterSelectorOptions(true)
                );

                DrawParameterSelector(
                    "Down",
                    sub2,
                    parameters,
                    VRCExpressionParameters.ValueType.Float,
                    new ParameterSelectorOptions(true)
                );

                DrawParameterSelector(
                    "Left",
                    sub3,
                    parameters,
                    VRCExpressionParameters.ValueType.Float,
                    new ParameterSelectorOptions(true)
                );

                Container.EndLayout();
            }

            private static void AxisCustomisationContainer(SerializedProperty property)
            {
                SerializedProperty labels = SafeGetLabels(property);

                using (new Container.Vertical("Customization"))
                {
                    DrawLabel(labels.GetArrayElementAtIndex(0), "Up");
                    DrawLabel(labels.GetArrayElementAtIndex(1), "Right");
                    DrawLabel(labels.GetArrayElementAtIndex(2), "Down");
                    DrawLabel(labels.GetArrayElementAtIndex(3), "Left");
                }
            }

            private static SerializedProperty SafeGetLabels(SerializedProperty property)
            {
                SerializedProperty labels = property.FindPropertyRelative("labels");

                labels.arraySize = 4;
                SerializedProperty l0 = labels.GetArrayElementAtIndex(0);
                if (l0 == null)
                {
                    VRCExpressionsMenu menu = (VRCExpressionsMenu)labels.serializedObject.targetObject;
                    int index = menu.controls.FindIndex(property.objectReferenceValue);
                    menu.controls[index].labels = new[]
                    {
                            new VRCExpressionsMenu.Control.Label(),
                            new VRCExpressionsMenu.Control.Label(),
                            new VRCExpressionsMenu.Control.Label(),
                            new VRCExpressionsMenu.Control.Label()
                        };
                }

                if (labels.GetArrayElementAtIndex(0) == null)
                    Debug.Log("ITEM IS NULL");

                return labels;
            }

            private static void DrawLabel(SerializedProperty property, string type)
            {
                bool compact = Preferences.CompactMode;
                float imgWidth = compact ? 28 : 58;
                float imgHeight = compact ? EditorGUIUtility.singleLineHeight : 58;

                SerializedProperty imgProperty = property.FindPropertyRelative("icon");
                SerializedProperty nameProperty = property.FindPropertyRelative("name");
                if (!compact) EditorGUILayout.BeginVertical("helpbox");

                using (new GUILayout.HorizontalScope())
                {
                    using (new GUILayout.VerticalScope())
                    {
                        if (!compact)
                            using (new EditorGUI.DisabledScope(true))
                                EditorGUILayout.LabelField("Axis", type, Styles.Label.LabelDropdown);

                        EditorGUILayout.PropertyField(nameProperty, compact ? GUIContent.none : new GUIContent("Name"));
                        Rect nameRect = GUILayoutUtility.GetLastRect();
                        if (compact && string.IsNullOrEmpty(nameProperty.stringValue)) GUI.Label(nameRect, $"{type}", Styles.Label.PlaceHolder);
                    }

                    imgProperty.objectReferenceValue = EditorGUILayout.ObjectField(imgProperty.objectReferenceValue, typeof(Texture2D), false, GUILayout.Width(imgWidth), GUILayout.Height(imgHeight));
                }

                if (!compact) EditorGUILayout.EndHorizontal();

            }

            #endregion
        }
    }
}