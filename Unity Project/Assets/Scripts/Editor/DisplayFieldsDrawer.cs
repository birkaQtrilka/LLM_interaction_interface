using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(DisplayFieldsAttribute))]
public class DisplayFieldsDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        DisplayFieldsAttribute displayAttr = (DisplayFieldsAttribute)attribute;
        float height = 0f;

        // If attached to an Array or List
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            // List Header height
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            if (property.isExpanded)
            {
                // Size field height
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                for (int i = 0; i < property.arraySize; i++)
                {
                    SerializedProperty element = property.GetArrayElementAtIndex(i);
                    // Element Header height
                    height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                    if (element.isExpanded)
                    {
                        foreach (string fieldName in displayAttr.fields)
                        {
                            SerializedProperty child = element.FindPropertyRelative(fieldName);
                            if (child != null)
                            {
                                height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                            }
                            else
                            {
                                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // HelpBox height
                            }
                        }
                    }
                }
            }
        }
        else
        {
            // If attached to a single standard object
            foreach (string fieldName in displayAttr.fields)
            {
                SerializedProperty child = property.FindPropertyRelative(fieldName);
                if (child != null)
                {
                    height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                }
                else
                {
                    height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                }
            }
        }

        return height > 0 ? height : EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        DisplayFieldsAttribute displayAttr = (DisplayFieldsAttribute)attribute;

        EditorGUI.BeginProperty(position, label, property);

        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            DrawArray(position, property, label, displayAttr);
        }
        else
        {
            DrawObjectFields(position, property, displayAttr);
        }

        EditorGUI.EndProperty();
    }

    private void DrawArray(Rect position, SerializedProperty property, GUIContent label, DisplayFieldsAttribute attr)
    {
        Rect rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        // Draw List Header
        property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);
        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            // Draw Size Field
            rect.height = EditorGUIUtility.singleLineHeight;
            int newSize = EditorGUI.IntField(rect, "Size", property.arraySize);
            if (newSize != property.arraySize) property.arraySize = newSize;
            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);

                // Draw Element Foldout
                rect.height = EditorGUIUtility.singleLineHeight;
                element.isExpanded = EditorGUI.Foldout(rect, element.isExpanded, $"Element {i}", true);
                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // Draw specific fields if element is expanded
                if (element.isExpanded)
                {
                    EditorGUI.indentLevel++;

                    foreach (string fieldName in attr.fields)
                    {
                        SerializedProperty child = element.FindPropertyRelative(fieldName);
                        if (child != null)
                        {
                            float childHeight = EditorGUI.GetPropertyHeight(child, true);
                            rect.height = childHeight;
                            EditorGUI.PropertyField(rect, child, true);
                            rect.y += childHeight + EditorGUIUtility.standardVerticalSpacing;
                        }
                        else
                        {
                            rect.height = EditorGUIUtility.singleLineHeight;
                            EditorGUI.LabelField(rect, $"Field '{fieldName}' not found", EditorStyles.helpBox);
                            rect.y += rect.height + EditorGUIUtility.standardVerticalSpacing;
                        }
                    }

                    EditorGUI.indentLevel--;
                }
            }
            EditorGUI.indentLevel--;
        }
    }

    private void DrawObjectFields(Rect position, SerializedProperty property, DisplayFieldsAttribute attr)
    {
        Rect rect = new Rect(position.x, position.y, position.width, 0);

        foreach (string fieldName in attr.fields)
        {
            SerializedProperty child = property.FindPropertyRelative(fieldName);
            if (child != null)
            {
                float childHeight = EditorGUI.GetPropertyHeight(child, true);
                rect.height = childHeight;
                EditorGUI.PropertyField(rect, child, true);
                rect.y += childHeight + EditorGUIUtility.standardVerticalSpacing;
            }
            else
            {
                rect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(rect, $"Field '{fieldName}' not found", EditorStyles.helpBox);
                rect.y += rect.height + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }
}