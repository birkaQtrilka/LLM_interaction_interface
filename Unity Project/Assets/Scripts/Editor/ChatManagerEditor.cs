using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Reflection;

[CustomEditor(typeof(ChatManager))]
public class ChatManagerEditor : Editor
{
    private SerializedProperty preparedMessagesProp;
    private ReorderableList reorderableList;

    private void OnEnable()
    {
        preparedMessagesProp = serializedObject.FindProperty("preparedMessages");

        reorderableList = new ReorderableList(serializedObject, preparedMessagesProp, true, true, true, true);

        reorderableList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Prepared Messages");
        };

        reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            SerializedProperty elementProp = preparedMessagesProp.GetArrayElementAtIndex(index);

            float buttonWidth = 60f;
            float spacing = 5f;

            Rect textFieldRect = new Rect(rect.x, rect.y + 2, rect.width - buttonWidth - spacing, EditorGUIUtility.singleLineHeight);
            Rect buttonRect = new Rect(rect.x + rect.width - buttonWidth, rect.y + 2, buttonWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(textFieldRect, elementProp, GUIContent.none);

            if (!GUI.Button(buttonRect, "Send")) return;
            if (!Application.isPlaying)
            {
                Debug.LogWarning("You must be in Play Mode to send chat messages!");
                return;
            }
            ChatManager chatManager = (ChatManager)target;

            MethodInfo onSubmitMethod = typeof(ChatManager).GetMethod("OnSubmit", BindingFlags.NonPublic | BindingFlags.Instance);
            if (onSubmitMethod != null)
            {
                onSubmitMethod.Invoke(chatManager, new object[] { elementProp.stringValue });
            }
            else
            {
                // Fallback if the method name changes in the future
                chatManager.AddChat(elementProp.stringValue);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "preparedMessages");
        EditorGUILayout.Space();
        reorderableList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }
}