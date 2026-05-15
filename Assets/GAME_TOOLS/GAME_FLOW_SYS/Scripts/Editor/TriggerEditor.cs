#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using UnityEditor;
using CatzTools.GameFlow;

namespace CatzTools.GameFlow.Editor
{
    #region SceneTransitionTrigger 共用基底 Editor

    /// <summary>
    /// SceneTransitionTrigger 系列共用基底 Editor
    /// 負責繪製基底類別的三個欄位：目標場景、允許重複觸發、Debug Log
    /// </summary>
    public abstract class SceneTransitionTriggerEditorBase : UnityEditor.Editor
    {
        #region SerializedProperty

        protected SerializedProperty _targetScene;
        protected SerializedProperty _allowRetrigger;

        #endregion SerializedProperty

        protected virtual void OnEnable()
        {
            _targetScene     = serializedObject.FindProperty("_targetScene");
            _allowRetrigger  = serializedObject.FindProperty("_allowRetrigger");
        }

        /// <summary>繪製基底欄位（轉場設定 + Log 狀態）</summary>
        protected void DrawBaseFields()
        {
            EditorGUILayout.LabelField(SceneFlowLocale.TrigSectionTransition, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_targetScene,    new GUIContent(SceneFlowLocale.TrigFieldTargetScene));
            EditorGUILayout.PropertyField(_allowRetrigger, new GUIContent(SceneFlowLocale.TrigFieldAllowRetrigger));

            EditorGUILayout.Space(6);
            CatzTools.Editor.CatzEditorStyles.DrawLogChannelStatus("FlowManager");
        }
    }

    #endregion SceneTransitionTrigger 共用基底 Editor

    #region ButtonTransitionTrigger Editor

    /// <summary>UI 按鈕轉場觸發器 Inspector</summary>
    [CustomEditor(typeof(ButtonTransitionTrigger))]
    public class ButtonTransitionTriggerEditor : SceneTransitionTriggerEditorBase
    {
        public override void OnInspectorGUI()
        {
            if (CatzTools.Editor.CatzInspectorHeader.Draw(target, serializedObject)) return;

            serializedObject.Update();
            DrawBaseFields();
            serializedObject.ApplyModifiedProperties();
        }
    }

    #endregion ButtonTransitionTrigger Editor

    #region ColliderTransitionTrigger Editor

    /// <summary>碰撞轉場觸發器 Inspector</summary>
    [CustomEditor(typeof(ColliderTransitionTrigger))]
    public class ColliderTransitionTriggerEditor : SceneTransitionTriggerEditorBase
    {
        #region SerializedProperty

        private SerializedProperty _triggerTag;

        #endregion SerializedProperty

        protected override void OnEnable()
        {
            base.OnEnable();
            _triggerTag = serializedObject.FindProperty("_triggerTag");
        }

        public override void OnInspectorGUI()
        {
            if (CatzTools.Editor.CatzInspectorHeader.Draw(target, serializedObject)) return;

            serializedObject.Update();
            DrawBaseFields();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(SceneFlowLocale.TrigSectionCollision, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_triggerTag, new GUIContent(SceneFlowLocale.TrigFieldTriggerTag));

            serializedObject.ApplyModifiedProperties();
        }
    }

    #endregion ColliderTransitionTrigger Editor

    // InteractionTransitionTrigger Editor 已於 v0.7.7b 移除（違反跨工具零源碼耦合鐵律）。
    // 改用 InputSys.InputActionTrigger（場景上掛）+ Inspector 拉 UnityEvent
    // → SceneTransitionTrigger.Transition() 公開接點。
    // 詳見 GAME_FLOW_SYS/CHANGELOG.md v0.7.7b 與 ToolsReadMe「跨工具零源碼耦合鐵律」章節。

    #region AutoTransitionTrigger Editor

    /// <summary>自動轉場觸發器 Inspector</summary>
    [CustomEditor(typeof(AutoTransitionTrigger))]
    public class AutoTransitionTriggerEditor : SceneTransitionTriggerEditorBase
    {
        #region SerializedProperty

        private SerializedProperty _autoFire;
        private SerializedProperty _delay;

        #endregion SerializedProperty

        protected override void OnEnable()
        {
            base.OnEnable();
            _autoFire = serializedObject.FindProperty("_autoFire");
            _delay    = serializedObject.FindProperty("_delay");
        }

        public override void OnInspectorGUI()
        {
            if (CatzTools.Editor.CatzInspectorHeader.Draw(target, serializedObject)) return;

            serializedObject.Update();
            DrawBaseFields();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(SceneFlowLocale.TrigSectionAutoTrigger, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoFire, new GUIContent(SceneFlowLocale.TrigFieldAutoFire));
            EditorGUILayout.PropertyField(_delay,    new GUIContent(SceneFlowLocale.TrigFieldDelay));

            if (!_autoFire.boolValue)
                EditorGUILayout.HelpBox(SceneFlowLocale.TrigAutoFireHint, MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }

    #endregion AutoTransitionTrigger Editor

    #region CoverTrigger 共用基底 Editor

    /// <summary>
    /// CoverTrigger 系列共用基底 Editor
    /// 負責繪製基底類別的三個欄位：Cover 名稱、觸發動作、Debug Log
    /// </summary>
    public abstract class CoverTriggerEditorBase : UnityEditor.Editor
    {
        #region SerializedProperty

        protected SerializedProperty _coverName;
        protected SerializedProperty _action;

        #endregion SerializedProperty

        #region Lazy Loading
        private SceneBlueprintData _blueprintData;
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

        protected virtual void OnEnable()
        {
            _coverName     = serializedObject.FindProperty("_coverName");
            _action        = serializedObject.FindProperty("_action");
        }

        /// <summary>繪製基底欄位（Cover 設定 + Log 狀態）</summary>
        protected void DrawBaseFields()
        {
            EditorGUILayout.LabelField(SceneFlowLocale.TrigSectionCover, EditorStyles.boldLabel);

            // Cover 名稱：Dropdown 選擇（附 fallback 手動輸入）
            DrawCoverNameDropdown();

            EditorGUILayout.PropertyField(_action, new GUIContent(SceneFlowLocale.TrigFieldAction));

            EditorGUILayout.Space(6);
            CatzTools.Editor.CatzEditorStyles.DrawLogChannelStatus("FlowManager");
        }

        /// <summary>Cover 名稱欄位 — 下拉選擇已知 Cover，支援手動輸入未知名稱</summary>
        private void DrawCoverNameDropdown()
        {
            var coverNames = new System.Collections.Generic.List<string>();
            if (BlueprintData != null)
            {
                coverNames = BlueprintData.nodes
                    .Where(n => n.nodeType == SceneNodeType.PopCover)
                    .OrderBy(n => n.sortOrder)
                    .Select(n => n.sceneName)
                    .ToList();
            }

            string current = _coverName.stringValue;

            if (coverNames.Count > 0)
            {
                // 找到目前值在清單中的 index（不在清單中 = -1）
                int currentIdx = coverNames.IndexOf(current);

                // 加一個「（手動輸入）」選項在最後
                var displayNames = coverNames.ToArray();

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                int newIdx = EditorGUILayout.Popup(SceneFlowLocale.TrigFieldCoverName,
                    Mathf.Max(currentIdx, 0), displayNames);
                if (EditorGUI.EndChangeCheck() && newIdx >= 0 && newIdx < coverNames.Count)
                {
                    _coverName.stringValue = coverNames[newIdx];
                }
                EditorGUILayout.EndHorizontal();

                // 如果目前值不在清單中，額外顯示警告 + 手動輸入框
                if (currentIdx < 0 && !string.IsNullOrEmpty(current))
                {
                    EditorGUILayout.HelpBox($"「{current}」不在已知 Cover 清單中", MessageType.Warning);
                    EditorGUILayout.PropertyField(_coverName, new GUIContent(" "));
                }
            }
            else
            {
                // 沒有 Cover 資料，退回純文字輸入
                EditorGUILayout.PropertyField(_coverName, new GUIContent(SceneFlowLocale.TrigFieldCoverName));
            }
        }
    }

    #endregion CoverTrigger 共用基底 Editor

    #region ButtonCoverTrigger Editor

    /// <summary>UI 按鈕 Cover 觸發器 Inspector</summary>
    [CustomEditor(typeof(ButtonCoverTrigger))]
    public class ButtonCoverTriggerEditor : CoverTriggerEditorBase
    {
        public override void OnInspectorGUI()
        {
            if (CatzTools.Editor.CatzInspectorHeader.Draw(target, serializedObject)) return;

            serializedObject.Update();
            DrawBaseFields();
            serializedObject.ApplyModifiedProperties();
        }
    }

    #endregion ButtonCoverTrigger Editor
}
#endif
