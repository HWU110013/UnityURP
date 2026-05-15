using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatzTools.GameFlow.Editor
{
    /// <summary>
    /// Edit Mode 守衛：偵測到 SceneEvent 場景內出現重複的共用單例
    /// （EventSystem / AudioListener / InputModule）立即清除。
    ///
    /// 處理路徑：使用者建立 Canvas（Unity 自動補 EventSystem）、手動拖組件、
    /// 從其他場景複製 GameObject 過來等任何會帶入重複組件的操作。
    ///
    /// 不處理 runtime（Play Mode）— 那是 SceneEvent.Awake 的職責。
    /// 尊重 SceneEvent._autoCleanupSharedSingletons 開關，false 時不動。
    /// </summary>
    [InitializeOnLoad]
    public static class SceneEventGuardEditor
    {
        #region 常數

        /// <summary>hierarchyChanged 觸發後的 debounce 延遲，避免操作中途反覆檢查</summary>
        private const double DEBOUNCE_SEC = 0.3;

        #endregion 常數

        #region 狀態

        private static double s_lastChangeTime;
        private static bool s_pending;

        #endregion 狀態

        #region 初始化

        static SceneEventGuardEditor()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.update += OnUpdate;
        }

        #endregion 初始化

        #region 事件處理

        private static void OnHierarchyChanged()
        {
            s_pending = true;
            s_lastChangeTime = EditorApplication.timeSinceStartup;
        }

        private static void OnUpdate()
        {
            if (!s_pending) return;
            // Play Mode 由 SceneEvent.Awake 處理，Editor 守衛不重複動作
            if (Application.isPlaying) { s_pending = false; return; }
            if (EditorApplication.timeSinceStartup - s_lastChangeTime < DEBOUNCE_SEC) return;

            s_pending = false;
            ScanAndClean();
        }

        #endregion 事件處理

        #region 掃描清理

        /// <summary>
        /// 掃描所有已載入場景：找到 SceneEvent → 清掉本場景重複的共用單例。
        /// 找不到 SceneEvent 的場景跳過（FlowManager 場景 / 純 Editor 工作場景不動）。
        /// </summary>
        private static void ScanAndClean()
        {
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var sceneEvent = FindSceneEvent(scene);
                if (sceneEvent == null) continue;
                if (!sceneEvent.AutoCleanupSharedSingletons) continue;

                int removed = sceneEvent.CleanupSharedSingletons(silent: false);
                if (removed > 0)
                    EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static SceneEvent FindSceneEvent(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var se = root.GetComponentInChildren<SceneEvent>(true);
                if (se != null) return se;
            }
            return null;
        }

        #endregion 掃描清理
    }
}
