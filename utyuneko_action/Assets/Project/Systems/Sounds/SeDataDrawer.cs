#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// SoundManagerの中にあるSeData構造体の見た目を、カスタムする宣言
[CustomPropertyDrawer(typeof(SoundManager.SeData))]
public class SeDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 構造体の中から "seType"（Enum）の変数を裏側で探し出す
        SerializedProperty seTypeProp = property.FindPropertyRelative("seType");

        if (seTypeProp != null)
        {
            // 現在インスペクターで選ばれているEnumの文字を吸い取る
            string enumName = seTypeProp.enumDisplayNames[seTypeProp.enumValueIndex];

            // Enumの文字で上書き
            label.text = enumName;
        }

        // 上書きした名前を使って、インスペクターに描画する
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 畳まれている時と、開いている時のインスペクターの高さを自動で正しく計算する
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif