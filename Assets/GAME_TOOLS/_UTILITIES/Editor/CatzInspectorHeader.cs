#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace CatzTools.Editor
{
    /// <summary>
    /// CatzTools 系列 Custom Inspector 通用頂部工具列
    /// 提供重設、移除、複製、上移、下移五顆快速操作按鈕
    /// 使用方式：在任何 CustomEditor 的 OnInspectorGUI() 最頂端呼叫
    ///   if (CatzInspectorHeader.Draw(target, serializedObject)) return;
    /// </summary>
    public static class CatzInspectorHeader
    {
        #region 語系（沿用 CatzTools 統一 EditorPrefs 鍵值）

        private const string LANG_PREF_KEY = "CatzTools_AudioManager_Lang";

        private static bool IsZH => EditorPrefs.GetInt(LANG_PREF_KEY, 0) == 0;

        private static string LabelReset  => IsZH ? "↺ 重設"  : "↺ Reset";
        private static string LabelRemove => IsZH ? "✕ 移除"  : "✕ Remove";
        private static string LabelCopy   => IsZH ? "⊡ 複製"  : "⊡ Copy";
        private static string TooltipReset  => IsZH ? "將所有欄位重設回預設值" : "Reset all fields to default";
        private static string TooltipRemove => IsZH ? "從 GameObject 移除此元件" : "Remove this component";
        private static string TooltipCopy   => IsZH ? "複製元件數值" : "Copy component values";
        private static string TooltipUp     => IsZH ? "在 Inspector 往上移一格" : "Move component up";
        private static string TooltipDown   => IsZH ? "在 Inspector 往下移一格" : "Move component down";

        #endregion 語系

        #region 公開 API

        /// <summary>
        /// 繪製頂部工具列。
        /// 回傳 true 代表元件已被移除，呼叫端應立即 return 結束 OnInspectorGUI。
        /// </summary>
        public static bool Draw(Object target, SerializedObject so = null)
        {
            var component = target as Component;
            if (component == null) return false;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // ↑ 往上（最左）
            if (GUILayout.Button(
                new GUIContent("↑", TooltipUp),
                EditorStyles.toolbarButton, GUILayout.Width(26)))
            {
                ComponentUtility.MoveComponentUp(component);
            }

            // ↓ 往下
            if (GUILayout.Button(
                new GUIContent("↓", TooltipDown),
                EditorStyles.toolbarButton, GUILayout.Width(26)))
            {
                ComponentUtility.MoveComponentDown(component);
            }

            // ↺ 重設
            if (GUILayout.Button(
                new GUIContent(LabelReset, TooltipReset),
                EditorStyles.toolbarButton, GUILayout.Width(64)))
            {
                ResetComponent(component, so);
            }

            // ⊡ 複製
            if (GUILayout.Button(
                new GUIContent(LabelCopy, TooltipCopy),
                EditorStyles.toolbarButton, GUILayout.Width(64)))
            {
                ComponentUtility.CopyComponent(component);
            }

            GUILayout.FlexibleSpace();

            // ✕ 移除（最右、紅字）
            var removeStyle = new GUIStyle(EditorStyles.toolbarButton);
            removeStyle.normal.textColor  = new Color(0.9f, 0.4f, 0.4f);
            removeStyle.hover.textColor   = new Color(1.0f, 0.3f, 0.3f);
            removeStyle.focused.textColor = new Color(1.0f, 0.3f, 0.3f);
            if (GUILayout.Button(
                new GUIContent(LabelRemove, TooltipRemove),
                removeStyle, GUILayout.Width(64)))
            {
                EditorGUILayout.EndHorizontal();
                Undo.DestroyObjectImmediate(component);
                GUIUtility.ExitGUI();
                return true;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            return false;
        }

        #endregion 公開 API

        #region 私有工具

        /// <summary>
        /// 建立同類型暫存元件，將預設值複製回目標元件
        /// </summary>
        private static void ResetComponent(Component component, SerializedObject so)
        {
            var tempGo = new GameObject("_CatzResetTemp");
            tempGo.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var tempComp = tempGo.AddComponent(component.GetType());
                Undo.RecordObject(component, "Reset Component");
                EditorUtility.CopySerialized(tempComp, component);
                so?.Update();
            }
            finally
            {
                Object.DestroyImmediate(tempGo);
            }
        }

        #endregion 私有工具
    }
}
#endif
