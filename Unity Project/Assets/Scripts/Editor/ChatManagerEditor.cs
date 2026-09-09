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
        // Find the preparedMessages array in the ChatManager script
        preparedMessagesProp = serializedObject.FindProperty("preparedMessages");

        // Initialize the ReorderableList (allows drag-and-drop reordering, adding, and removing)
        reorderableList = new ReorderableList(serializedObject, preparedMessagesProp, true, true, true, true);

        // Draw the Header
        reorderableList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Prepared Messages");
        };

        // Draw each element in the list
        reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            SerializedProperty elementProp = preparedMessagesProp.GetArrayElementAtIndex(index);

            // Calculate layout for the text field and the button
            float buttonWidth = 60f;
            float spacing = 5f;

            // Rect for the string field
            Rect textFieldRect = new Rect(rect.x, rect.y + 2, rect.width - buttonWidth - spacing, EditorGUIUtility.singleLineHeight);

            // Rect for the "Send" button
            Rect buttonRect = new Rect(rect.x + rect.width - buttonWidth, rect.y + 2, buttonWidth, EditorGUIUtility.singleLineHeight);

            // Draw the editable string field
            EditorGUI.PropertyField(textFieldRect, elementProp, GUIContent.none);

            // Draw the Send button
            if (GUI.Button(buttonRect, "Send"))
            {
                if (Application.isPlaying)
                {
                    ChatManager chatManager = (ChatManager)target;

                    // Option 1: Just add to the UI using your public method
                    // chatManager.AddChat(elementProp.stringValue);

                    // Option 2 (Better): Use Reflection to call your private "OnSubmit" method 
                    // This ensures the input text is cleared AND the OnTextSent event is properly invoked!
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
                }
                else
                {
                    Debug.LogWarning("You must be in Play Mode to send chat messages!");
                }
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw all standard variables in ChatManager EXCEPT "preparedMessages"
        DrawPropertiesExcluding(serializedObject, "m_Script", "preparedMessages");

        EditorGUILayout.Space();

        // Draw our custom Reorderable List
        reorderableList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }
}