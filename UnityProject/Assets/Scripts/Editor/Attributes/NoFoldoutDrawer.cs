using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(NoFoldoutAttribute))]
public class NoFoldoutDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // If the property is a simple type with no children, return default height
        if (!property.hasVisibleChildren)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        float height = 0f;
        SerializedProperty iterator = property.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();

        // Iterate through all children to sum up their heights
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
        {
            height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
            enterChildren = false; // Only step into the first level of children
        }

        // Subtract the trailing vertical spacing
        return height > 0 ? height - EditorGUIUtility.standardVerticalSpacing : EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!property.hasVisibleChildren)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        SerializedProperty iterator = property.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();

        bool enterChildren = true;
        Rect currentRect = position;

        // Iterate through all children and draw them sequentially 
        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
        {
            float propHeight = EditorGUI.GetPropertyHeight(iterator, true);
            currentRect.height = propHeight;

            // Draw the current child property
            EditorGUI.PropertyField(currentRect, iterator, true);

            // Move the rectangle down for the next property
            currentRect.y += propHeight + EditorGUIUtility.standardVerticalSpacing;
            enterChildren = false;
        }
    }
}
