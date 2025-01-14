using UnityEngine;
using UnityEditor;

class LabelAttribute : PropertyAttribute
{
    public string NewName { get; private set; }
    public LabelAttribute(string name)
    {
        NewName = name;
    }
}

[CustomPropertyDrawer(typeof(LabelAttribute))]
class LabelEditor : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.PropertyField(position, property, new GUIContent((attribute as LabelAttribute).NewName));
    }
}