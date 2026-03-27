#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CatzTools
{
    #region SceneFlow 刪除保護
    /// <summary>
    /// 雙重保護 SceneFlow 系統核心物件，防止在編輯器中被誤刪。
    /// 第一層：HideFlags.NotEditable 鎖定 GameObject。
    /// 第二層：追蹤快取 + ObjectChangeEvents，偵測到刪除立即 Undo。
    /// 保護對象：FlowManager、TransitionController、SceneEvent。
    /// </summary>
    [InitializeOnLoad]
    public static class SceneFlowProtector
    {
        #region 保護清單
        /// <summary>受保護的元件類型（僅系統核心，不含觸發器）</summary>
        private static readonly Type[] ProtectedTypes =
        {
            typeof(FlowManager),
            typeof(TransitionController),
            typeof(SceneEvent)
        };

        /// <summary>追蹤中的受保護物件 InstanceID → 名稱（用於 Undo 提示）</summary>
        private static readonly Dictionary<int, string> TrackedObjects = new();
        #endregion 保護清單

        #region 初始化
        static SceneFlowProtector()
        {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            EditorApplication.hierarchyChanged += RefreshTracking;
            EditorSceneManager.sceneOpened += (_, _) => RefreshTracking();
            EditorApplication.delayCall += RefreshTracking;
        }
        #endregion 初始化

        #region 追蹤與鎖定
        /// <summary>
        /// 掃描場景，鎖定受保護物件並更新追蹤快取
        /// </summary>
        private static void RefreshTracking()
        {
            if (Application.isPlaying) return;

            TrackedObjects.Clear();

            foreach (var type in ProtectedTypes)
            {
                var components = UnityEngine.Object.FindObjectsByType(
                    type, FindObjectsSortMode.None);

                foreach (var comp in components)
                {
                    var go = ((Component)comp).gameObject;
                    int id = go.GetInstanceID();

                    // 第一層：HideFlags 鎖定
                    if ((go.hideFlags & HideFlags.NotEditable) == 0)
                        go.hideFlags |= HideFlags.NotEditable;

                    // 加入追蹤
                    TrackedObjects[id] = go.name;
                }
            }
        }
        #endregion 追蹤與鎖定

        #region 刪除攔截
        /// <summary>
        /// 偵測物件銷毀事件，若為受保護物件則立即 Undo
        /// </summary>
        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (Application.isPlaying) return;

            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.DestroyGameObjectHierarchy)
                    continue;

                stream.GetDestroyGameObjectHierarchyEvent(i,
                    out var destroyEvent);

                int id = (int)destroyEvent.instanceId;
                if (!TrackedObjects.TryGetValue(id, out string objName))
                    continue;

                // 第二層：Undo 還原
                Undo.PerformUndo();
                Debug.LogWarning(
                    $"[SceneFlow] 已阻止刪除受保護物件「{objName}」。" +
                    "如需移除請透過 SceneFlow 編輯器操作。");

                // 還原後重新鎖定
                EditorApplication.delayCall += RefreshTracking;
                return;
            }
        }
        #endregion 刪除攔截
    }
    #endregion SceneFlow 刪除保護
}
#endif
