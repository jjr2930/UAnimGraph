using UAnimGraph.Runtime;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Editor
{

    //[CustomPropertyDrawer(typeof(State))]
    public class StatePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            State targetState = property.boxedValue as State;

            Rect labelRect = new Rect(position);
            labelRect.height = EditorGUIUtility.singleLineHeight;
            GUI.Label(labelRect, label);
            EditorGUI.indentLevel++;
            {
                Rect mixer1dRect = new Rect(position);
                mixer1dRect.y += EditorGUIUtility.singleLineHeight;
                mixer1dRect.height = EditorGUIUtility.singleLineHeight;

                Rect mixer2dRect = new Rect(mixer1dRect);
                mixer2dRect.y += EditorGUIUtility.singleLineHeight;

                Rect clipPlayerRect = new Rect(mixer2dRect);
                clipPlayerRect.y += EditorGUIUtility.singleLineHeight;

                if (GUI.Button(mixer1dRect, "Create New Mixer1D"))
                {
                    var playableElements = property.FindPropertyRelative("playableElements");
                    playableElements.InsertArrayElementAtIndex(playableElements.arraySize);
                    property.serializedObject.ApplyModifiedProperties();
                }

                if (GUI.Button(mixer2dRect, "Create New mixer2d"))
                {
                    Debug.Log("Not supported yet");
                }


                if (GUI.Button(clipPlayerRect, "Create New Clip Player"))
                {
                }
            }
            EditorGUI.indentLevel--;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return 100;
        }
    }
}