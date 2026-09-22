using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FolderPathAttribute))]
public class FolderPathDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float buttonWidth = 70f;
        Rect fieldRect = new Rect(position.x, position.y, position.width - buttonWidth - 4f, position.height);
        Rect buttonRect = new Rect(position.xMax - buttonWidth, position.y, buttonWidth, position.height);

        EditorGUI.PropertyField(fieldRect, property, label);
        if (!GUI.Button(buttonRect, "Browse")) return;

        string start = property.stringValue;
        if (string.IsNullOrEmpty(start))
        {
            start = UserTestLogger.DefaultFolder();
        }

        string picked = EditorUtility.OpenFolderPanel("User test logs", start, "");
        if (string.IsNullOrEmpty(picked)) return;
        property.stringValue = picked;
    }
}
