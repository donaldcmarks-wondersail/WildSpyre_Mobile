using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws <see cref="InlineSOAttribute"/> fields: the normal object slot, plus a
/// foldout that exposes the referenced ScriptableObject's serialized fields for
/// direct editing. Edits are written back to the referenced asset.
/// </summary>
[CustomPropertyDrawer(typeof(InlineSOAttribute))]
public class InlineSODrawer : PropertyDrawer
{
    private const float InnerPad = 6f;
    private const float Indent = 12f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight;

        if (property.propertyType != SerializedPropertyType.ObjectReference ||
            property.objectReferenceValue == null ||
            !property.isExpanded)
            return h;

        h += EditorGUIUtility.standardVerticalSpacing + InnerPad * 2f;

        var so = new SerializedObject(property.objectReferenceValue);
        var it = so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.propertyPath == "m_Script") continue;
            h += EditorGUI.GetPropertyHeight(it, true) + EditorGUIUtility.standardVerticalSpacing;
        }
        so.Dispose();
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        if (property.propertyType != SerializedPropertyType.ObjectReference)
        {
            EditorGUI.PropertyField(line, property, label);
            return;
        }

        EditorGUI.PropertyField(line, property, label);

        if (property.objectReferenceValue == null)
        {
            property.isExpanded = false;
            return;
        }

        // Foldout arrow over the label area (drawn last so it wins the click).
        property.isExpanded = EditorGUI.Foldout(
            new Rect(line.x, line.y, EditorGUIUtility.labelWidth, line.height),
            property.isExpanded, GUIContent.none, true);

        if (!property.isExpanded)
            return;

        var so = new SerializedObject(property.objectReferenceValue);
        so.UpdateIfRequiredOrScript();

        float top = line.yMax + EditorGUIUtility.standardVerticalSpacing;
        var box = new Rect(position.x, top, position.width, position.yMax - top);
        GUI.Box(box, GUIContent.none, EditorStyles.helpBox);

        float y = top + InnerPad;
        float x = position.x + InnerPad + Indent;
        float w = position.width - InnerPad * 2f - Indent;

        var it = so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.propertyPath == "m_Script") continue;

            float ph = EditorGUI.GetPropertyHeight(it, true);
            EditorGUI.PropertyField(new Rect(x, y, w, ph), it, true);
            y += ph + EditorGUIUtility.standardVerticalSpacing;
        }

        so.ApplyModifiedProperties();
        so.Dispose();
    }
}
