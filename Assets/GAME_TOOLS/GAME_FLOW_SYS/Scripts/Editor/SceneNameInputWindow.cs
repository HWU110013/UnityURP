#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;

namespace CatzTools
{
    #region 場景命名輸入視窗
    /// <summary>
    /// 場景命名輸入視窗 — 新增場景時彈出，輸入名稱後確認建立
    /// </summary>
    public class SceneNameInputWindow : EditorWindow
    {
        #region 私有變數
        private string _sceneName = "";
        private Action<string> _onConfirm;
        private bool _focusTextField = true;
        #endregion 私有變數

        #region 開啟視窗
        /// <summary>
        /// 顯示命名視窗
        /// </summary>
        public static void Show(Action<string> onConfirm, string defaultName = "")
        {
            var window = CreateInstance<SceneNameInputWindow>();
            window.titleContent = new GUIContent("新增場景");
            window._onConfirm = onConfirm;
            window._sceneName = string.IsNullOrEmpty(defaultName) ? "NewScene" : defaultName;
            window.minSize = new Vector2(320, 90);
            window.maxSize = new Vector2(320, 90);
            window.ShowUtility();
            window.CenterOnMainWin();
        }
        #endregion 開啟視窗

        #region GUI
        private void OnGUI()
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("場景名稱：", EditorStyles.boldLabel);

            GUI.SetNextControlName("SceneNameField");
            _sceneName = EditorGUILayout.TextField(_sceneName);

            // 首次開啟自動聚焦並全選
            if (_focusTextField)
            {
                EditorGUI.FocusTextInControl("SceneNameField");
                _focusTextField = false;
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Enter 鍵確認
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                Confirm();
                Event.current.Use();
            }

            // Escape 鍵取消
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
            }

            if (GUILayout.Button("取消", GUILayout.Width(80)))
            {
                Close();
            }

            if (GUILayout.Button("確認", GUILayout.Width(80)))
            {
                Confirm();
            }

            EditorGUILayout.EndHorizontal();
        }
        #endregion GUI

        #region 操作
        /// <summary>
        /// 確認建立
        /// </summary>
        private void Confirm()
        {
            string trimmed = _sceneName?.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                EditorUtility.DisplayDialog("錯誤", "場景名稱不能為空！", "確定");
                return;
            }

            // 檢查非法字元
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                if (trimmed.Contains(c))
                {
                    EditorUtility.DisplayDialog("錯誤", $"場景名稱包含非法字元：{c}", "確定");
                    return;
                }
            }

            _onConfirm?.Invoke(trimmed);
            Close();
        }

        /// <summary>
        /// 置中視窗
        /// </summary>
        private void CenterOnMainWin()
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            var pos = position;
            pos.x = main.x + (main.width - pos.width) * 0.5f;
            pos.y = main.y + (main.height - pos.height) * 0.5f;
            position = pos;
        }
        #endregion 操作
    }
    #endregion 場景命名輸入視窗
}
#endif
