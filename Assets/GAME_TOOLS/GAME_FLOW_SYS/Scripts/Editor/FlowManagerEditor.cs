#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace CatzTools
{
    #region FlowManager 自訂 Inspector
    /// <summary>
    /// FlowManager 自訂 Inspector — 顯示藍圖總覽、起始場景、全部場景節點、運行狀態
    /// </summary>
    [CustomEditor(typeof(FlowManager))]
    public class FlowManagerEditor : UnityEditor.Editor
    {
        #region 私有變數
        private FlowManager _target;
        private SceneBlueprintData _blueprintData;
        private bool _foldoutScenes = true;
        private bool _foldoutRuntime = true;
        #endregion 私有變數

        #region Lazy Loading
        private SceneBlueprintData BlueprintData
        {
            get
            {
                if (_blueprintData == null)
                    _blueprintData = Resources.Load<SceneBlueprintData>("SceneBlueprintData");
                return _blueprintData;
            }
        }
        #endregion Lazy Loading

        #region Unity Editor
        private void OnEnable()
        {
            _target = (FlowManager)target;
        }

        public override void OnInspectorGUI()
        {
            // NotEditable 會鎖死 Inspector，手動解鎖讓元件可編輯
            GUI.enabled = true;
            serializedObject.Update();

            // ── 標題 ──
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("⚙ FlowManager", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── 起始場景（唯讀，從藍圖讀取）──
            DrawStartSceneInfo();

            EditorGUILayout.Space(4);

            // ── Debug Log 開關 ──
            var debugProp = serializedObject.FindProperty("_showDebugLogs");
            EditorGUILayout.PropertyField(debugProp, new GUIContent("顯示 Debug Log"));

            EditorGUILayout.Space(8);

            // ── 場景總覽 ──
            DrawSceneOverview();

            EditorGUILayout.Space(4);

            // ── 運行時資訊（Play Mode）──
            if (Application.isPlaying)
            {
                DrawRuntimeInfo();
                EditorGUILayout.Space(4);
            }

            // ── 工具按鈕 ──
            DrawToolButtons();

            serializedObject.ApplyModifiedProperties();

            // Play Mode 時持續刷新
            if (Application.isPlaying)
                Repaint();
        }
        #endregion Unity Editor

        #region 起始場景資訊
        private void DrawStartSceneInfo()
        {
            string startScene = BlueprintData != null ? BlueprintData.startSceneName : "";
            bool hasStart = !string.IsNullOrEmpty(startScene);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("起始場景");

            if (hasStart)
            {
                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
                style.fontStyle = FontStyle.Bold;
                EditorGUILayout.LabelField($"▶ {startScene}", style);
            }
            else
            {
                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = new Color(0.9f, 0.6f, 0.2f);
                EditorGUILayout.LabelField("⚠ 未設定（請在場景流程圖中設定）", style);
            }

            EditorGUILayout.EndHorizontal();
        }
        #endregion 起始場景資訊

        #region 場景總覽
        private void DrawSceneOverview()
        {
            if (BlueprintData == null)
            {
                EditorGUILayout.HelpBox("找不到 SceneBlueprintData。", MessageType.Warning);
                return;
            }

            int nodeCount = BlueprintData.nodes?.Count ?? 0;
            int edgeCount = BlueprintData.edges?.Count ?? 0;
            int linkedCount = 0;

            if (BlueprintData.nodes != null)
                linkedCount = BlueprintData.nodes.Count(n => n.sceneAsset != null);

            _foldoutScenes = EditorGUILayout.Foldout(_foldoutScenes,
                $"場景總覽（{nodeCount} 節點 / {edgeCount} 連線 / {linkedCount} 已建立）", true);

            if (!_foldoutScenes || BlueprintData.nodes == null) return;

            EditorGUI.indentLevel++;

            foreach (var node in BlueprintData.nodes)
            {
                EditorGUILayout.BeginHorizontal();

                // 狀態圖示
                bool hasAsset = node.sceneAsset != null;
                string icon = node.isStartNode ? "▶" : hasAsset ? "✓" : "○";
                Color iconColor = node.isStartNode
                    ? new Color(0.3f, 0.8f, 0.3f)
                    : hasAsset
                        ? new Color(0.5f, 0.7f, 0.5f)
                        : new Color(0.6f, 0.6f, 0.6f);

                var iconStyle = new GUIStyle(EditorStyles.label);
                iconStyle.normal.textColor = iconColor;
                iconStyle.fixedWidth = 18;
                EditorGUILayout.LabelField(icon, iconStyle, GUILayout.Width(18));

                // 場景名稱
                EditorGUILayout.LabelField(node.sceneName);

                // 連線數
                var targets = BlueprintData.GetReachableTargets(node.id);
                if (targets.Count > 0)
                {
                    var connStyle = new GUIStyle(EditorStyles.miniLabel);
                    connStyle.normal.textColor = new Color(0.5f, 0.5f, 0.6f);
                    string targetNames = string.Join(", ",
                        targets.Select(id => BlueprintData.FindNodeById(id)?.sceneName ?? "?"));
                    EditorGUILayout.LabelField($"→ {targetNames}", connStyle);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
        }
        #endregion 場景總覽

        #region 運行時資訊
        private void DrawRuntimeInfo()
        {
            _foldoutRuntime = EditorGUILayout.Foldout(_foldoutRuntime,
                "運行時狀態", true);

            if (!_foldoutRuntime) return;

            EditorGUI.indentLevel++;

            // 當前場景
            string current = _target.CurrentSceneName;
            EditorGUILayout.LabelField("當前場景",
                string.IsNullOrEmpty(current) ? "—" : current);

            // 轉場狀態
            EditorGUILayout.LabelField("轉場中",
                _target.IsTransitioning ? "是" : "否");

            // 歷史
            var history = _target.SceneHistory;
            if (history != null && history.Count > 0)
            {
                EditorGUILayout.LabelField("場景歷史",
                    string.Join(" → ", history));
            }

            // 當前 SceneEvent
            EditorGUILayout.LabelField("SceneEvent",
                _target.CurrentSceneEvent != null
                    ? _target.CurrentSceneEvent.SceneName
                    : "無");

            EditorGUI.indentLevel--;
        }
        #endregion 運行時資訊

        #region 工具按鈕
        private void DrawToolButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("開啟場景流程圖"))
            {
                SceneFlowEditorWindow.ShowWindow();
            }

            if (GUILayout.Button("刷新"))
            {
                _blueprintData = null;
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }
        #endregion 工具按鈕
    }
    #endregion FlowManager 自訂 Inspector
}
#endif
