#if UNITY_EDITOR
using UnityEditor;
using CatzTools.GameFlow;

namespace CatzTools.GameFlow.Editor
{
    #region 語系切換
    /// <summary>
    /// 場景流程圖 i18n — 集中管理所有 Editor UI 文字
    /// </summary>
    public static class SceneFlowLocale
    {
        /// <summary>支援語系</summary>
        public enum Lang { ZH, EN }

        private const string PREF_KEY = "CatzTools_SceneFlow_Lang";

        /// <summary>目前語系</summary>
        public static Lang Current
        {
            get => (Lang)EditorPrefs.GetInt(PREF_KEY, 0);
            set => EditorPrefs.SetInt(PREF_KEY, (int)value);
        }

        /// <summary>切換語系</summary>
        public static void Toggle()
        {
            Current = Current == Lang.ZH ? Lang.EN : Lang.ZH;
        }

        /// <summary>目前是否為中文</summary>
        public static bool IsZH => Current == Lang.ZH;

        // ─────────────────────────────────
        //  資訊列 / FlowManager Bar
        // ─────────────────────────────────
        public static string FlowManagerTitle => IsZH ? "⚙ 流程管理器" : "⚙ FlowManager";
        public static string BtnOpen => IsZH ? "開啟" : "Open";
        public static string BtnRebuild => IsZH ? "重建" : "Rebuild";
        public static string BtnPlay => IsZH ? "▶ 播放" : "▶ Play";
        public static string BtnStop => IsZH ? "■ 停止" : "■ Stop";
        public static string StatusReady(string start) => IsZH ? $"✓ 就緒 — 起始場景: {start}" : $"✓ Ready — Start: {start}";
        public static string StatusMissing => IsZH ? "⚠ 場景不存在，點擊「重建」建立" : "⚠ Scene missing, click Rebuild";

        // ─────────────────────────────────
        //  工具列
        // ─────────────────────────────────
        public static string ToolAddScene => IsZH ? "新增場景" : "Add Scene";
        public static string ToolGenerateAll => IsZH ? "產生全部場景" : "Generate All";
        public static string ToolSyncBuild => IsZH ? "同步到建置" : "Sync → Build";
        public static string ToolLoadBuild => IsZH ? "從建置同步" : "Build → Sync";
        public static string ToolBuildSection => IsZH ? "建置設定:" : "Build:";

        // 確認對話框
        public static string DlgSyncToBuildTitle => IsZH ? "同步到建置設定" : "Sync to Build Settings";
        public static string DlgSyncToBuildMsg => IsZH ? "將覆蓋 Unity Build Settings 的場景清單與順序。\n繼續？" : "Will overwrite Unity Build Settings scene list and order.\nContinue?";
        public static string DlgLoadFromBuildTitle => IsZH ? "從建置同步" : "Sync from Build";
        public static string DlgLoadFromBuildMsg => IsZH ? "將依 Build Settings 新增場景節點（既有不刪）。\n繼續？" : "Will add scene nodes from Build Settings (existing kept).\nContinue?";
        public static string DlgYes => IsZH ? "確定" : "OK";
        public static string DlgNo => IsZH ? "取消" : "Cancel";
        // ToolImport / ToolExport / ToolSavePreset / ToolLoadPreset 於 v0.7.9b 移除（方法同步刪除）
        public static string ToolAutoLayout => IsZH ? "自動排列" : "Auto Layout";
        public static string ToolCenter => IsZH ? "置中" : "Center";
        public static string ToolReload => IsZH ? "重新載入" : "Reload";
        public static string ToolSceneList => IsZH ? "場景清單" : "Scenes";
        public static string ToolCover => IsZH ? "彈出層" : "Cover";

        // ─────────────────────────────────
        //  InfoBar — GDS 匯入
        // ─────────────────────────────────
        public static string InfoBarImportGDS => IsZH ? "從 GDS 匯入" : "Import from GDS";
        public static string DlgGdsNotImplemented => IsZH ? "GDS 場景流程匯入尚未實作。" : "GDS scene flow import not yet implemented.";

        // ─────────────────────────────────
        //  狀態列
        // ─────────────────────────────────
        public static string StatusNodes => IsZH ? "場景節點" : "Nodes";
        public static string StatusEdges => IsZH ? "連線" : "Edges";
        public static string StatusStart => IsZH ? "起始" : "Start";
        public static string StatusNone => IsZH ? "未設定" : "None";
        public static string LangToggle => IsZH ? "EN" : "中";

        // ─────────────────────────────────
        //  屬性面板
        // ─────────────────────────────────
        public static string InspTitle => IsZH ? "屬性" : "Inspector";
        public static string InspSelectHint => IsZH ? "選取節點或連線" : "Select a node or edge";
        public static string InspStartPoint => IsZH ? "▶ 起始點" : "▶ Start";
        public static string InspStartDesc => IsZH ? "遊戲啟動入口\n連線到起始場景，在連線上設定開場轉場效果" : "Game entry point\nConnect to the first scene, set opening transition on the edge";

        // ── FlowManager 場景設定（v0.7.4b 新增）──
        public static string InspFlowManagerSection => IsZH ? "FlowManager 場景設定" : "FlowManager Scene Settings";
        public static string InspFlowCameraTag      => IsZH ? "相機 Tag" : "Camera Tag";
        public static string InspFlowCameraTagHint  => IsZH
            ? "FlowManager 的 UI overlay 相機 Tag。預設 Untagged 避免和遊戲主相機衝突。純測試場景可設為 MainCamera 讓 Camera.main 有值。變更後需按上方 Rebuild 才會套用到場景檔。"
            : "Tag for FlowManager's UI overlay camera. Default Untagged to avoid conflict with game camera. For test scenes without a real main camera, set to MainCamera so Camera.main is not null. Changes take effect after clicking Rebuild above.";

        // ── ServiceLocator 啟動清單（v0.x 新增）──
        public static string InspServicesTitle    => IsZH ? "ServiceLocator 啟動清單" : "ServiceLocator Boot List";
        public static string InspServicesHint     => IsZH ? "勾選要在遊戲啟動時初始化的服務；上方順序就是初始化順序，未勾選 = 不啟動" : "Check services to initialize on game start; order matters, unchecked = skipped";
        public static string InspServicesAutoDetect => IsZH ? "自動偵測" : "Auto Detect";
        public static string InspServicesAutoDetectTip => IsZH ? "用反射重掃所有 [AutoRegister] 服務並依 priority 順序填入" : "Re-scan all [AutoRegister] services and fill by priority";
        public static string InspServicesClear     => IsZH ? "清空" : "Clear";
        public static string InspServicesAddBtn    => IsZH ? "+ 新增服務" : "+ Add Service";
        public static string InspServicesEmpty     => IsZH ? "（空 — 將沿用全自動反射發現）" : "(empty — will fall back to full auto-discover)";
        public static string InspServicesMissing(string name) => IsZH ? $"⚠ 找不到型別：{name}" : $"⚠ Type not found: {name}";
        public static string InspServicesMoveUp    => "↑";
        public static string InspServicesMoveDown  => "↓";
        public static string InspServicesRemove    => "✕";
        public static string InspServicesNoneAvailable => IsZH ? "（沒有可新增的服務）" : "(no services available to add)";

        // ── 多選版（最新）──
        public static string InspServicesSelectAll        => IsZH ? "全選" : "All";
        public static string InspServicesSelectAllTip     => IsZH ? "勾選所有反射到的服務並依 priority 排序" : "Check all discovered services, sorted by priority";
        public static string InspServicesSelectNone       => IsZH ? "清空" : "None";
        public static string InspServicesSortByPriority   => IsZH ? "依 Priority 排序" : "Sort by Priority";
        public static string InspServicesSortByPriorityTip => IsZH ? "保留勾選狀態，依 [AutoRegister] priority 重新排序" : "Reorder checked items by [AutoRegister] priority";
        public static string InspServicesCount(int sel, int total) => IsZH ? $"已勾選 {sel} / 掃描到 {total}" : $"{sel} selected / {total} discovered";
        public static string InspServicesNoneScanned      => IsZH ? "（掃描不到任何 [AutoRegister] 服務）" : "(no [AutoRegister] services discovered)";
        public static string InspServicesScan             => IsZH ? "重新掃描" : "Rescan";
        public static string InspServicesScanTip          => IsZH ? "重新反射掃描所有 [AutoRegister] 服務（編譯完成後使用）" : "Re-scan all [AutoRegister] services (use after recompile)";
        public static string InspEndDesc => IsZH ? "流程終點" : "End Point";
        public static string InspSceneName => IsZH ? "場景名稱" : "Scene Name";
        public static string InspSceneAsset => IsZH ? "場景資源" : "Scene Asset";
        public static string InspStatus => IsZH ? "狀態" : "Status";
        public static string InspStartScene => IsZH ? "★ 起始場景" : "★ Start Scene";
        public static string InspNormalScene => IsZH ? "一般場景" : "Normal Scene";
        public static string InspDescription => IsZH ? "描述" : "Description";
        public static string InspOperations => IsZH ? "操作" : "Actions";
        public static string InspCreateScene => IsZH ? "建立場景" : "Create Scene";
        public static string InspOpenScene => IsZH ? "開啟場景" : "Open Scene";
        public static string InspSetStart => IsZH ? "設為起點" : "Set as Start";
        public static string InspSetStartDone => IsZH ? "已設為起始場景" : "Set as start scene";
        public static string InspLinked => IsZH ? "✓ 已連結" : "✓ Linked";
        public static string InspUnlinked => IsZH ? "✗ 未連結場景資源" : "✗ No scene asset";

        // ─────────────────────────────────
        //  連線屬性（轉場）
        // ─────────────────────────────────
        public static string TransEffect => IsZH ? "轉場效果" : "Transition";
        public static string TransDuration => IsZH ? "時長 (秒)" : "Duration (s)";
        public static string TransMaskColor => IsZH ? "遮罩顏色" : "Mask Color";
        public static string TransShaderMat => IsZH ? "Shader 材質" : "Shader Material";
        public static string TransMatHint => IsZH ? "需要含 _Progress (0~1) 屬性的 Material" : "Material needs _Progress (0~1) property";
        public static string TransUILabel => IsZH ? "UI 轉場" : "UI Transition";
        public static string TransCustomLabel => IsZH ? "自訂轉場" : "Custom Transition";
        public static string TransCustomProps(int count) => IsZH ? $"自訂屬性（此連線）— {count} 項" : $"Custom Props (this edge) — {count}";

        // 轉場類型名稱
        public static string TransNone => IsZH ? "無轉場" : "None";
        public static string TransFade => IsZH ? "淡入淡出" : "Fade";
        public static string TransSlideL => IsZH ? "左滑" : "Slide Left";
        public static string TransSlideR => IsZH ? "右滑" : "Slide Right";
        public static string TransSlideU => IsZH ? "上滑" : "Slide Up";
        public static string TransSlideD => IsZH ? "下滑" : "Slide Down";
        public static string TransCustom(string name) => IsZH ? $"自訂：{name}" : $"Custom: {name}";
        public static string TransCustomNoMat => IsZH ? "自訂：未指定材質" : "Custom: No Material";

        // 快捷按鈕
        public static string PresetFade => IsZH ? "淡入淡出" : "Fade";
        public static string PresetWhiteFade => IsZH ? "白色淡入" : "White Fade";
        public static string PresetSlideL => IsZH ? "左滑" : "Left";
        public static string PresetSlideR => IsZH ? "右滑" : "Right";
        public static string PresetNone => IsZH ? "無轉場" : "None";

        // ─────────────────────────────────
        //  右側面板
        // ─────────────────────────────────
        public static string PanelBuildSettings => IsZH ? "建置設定" : "Build Settings";
        public static string PanelMiniMap => IsZH ? "小地圖" : "Mini Map";
        public static string PanelCoverTitle => IsZH ? "◻ 彈出層管理" : "◻ Cover Manager";
        public static string PanelCoverAdd => IsZH ? "＋ 新增" : "＋ Add";
        public static string PanelCoverEmpty => IsZH ? "尚無彈出層。點擊「＋ 新增」建立。" : "No covers yet. Click + Add.";
        public static string PanelBuildEmpty => IsZH ? "建置設定中無場景" : "No scenes in Build Settings";
        public static string LabelEnabled => IsZH ? "啟用" : "ON";
        public static string LabelDisabled => IsZH ? "停用" : "OFF";
        public static string LabelAdditive => IsZH ? "加載" : "Additive";

        // ─────────────────────────────────
        //  Cover 面板
        // ─────────────────────────────────
        public static string CoverSource => IsZH ? "來源" : "Source";
        public static string CoverPrefab => IsZH ? "預置物" : "Prefab";
        public static string CoverSceneAsset => IsZH ? "場景資源" : "Scene Asset";
        public static string CoverOpenTrans => IsZH ? "開啟轉場" : "Open Transition"; // Legacy
        public static string CoverCloseTrans => IsZH ? "關閉轉場" : "Close Transition"; // Legacy
        public static string CoverOpenAnim => IsZH ? "開啟動畫" : "Open Anim";
        public static string CoverCloseAnim => IsZH ? "關閉動畫" : "Close Anim";
        public static string CoverBoundScenes => IsZH ? "可用場景" : "Bound Scenes";
        public static string CoverGlobal => IsZH ? "全域 — 所有場景皆可開啟" : "Global — All scenes can open";
        public static string CoverBindScene => IsZH ? "＋ 綁定場景" : "＋ Bind Scene";
        public static string CoverClearGlobal => IsZH ? "清除（全域）" : "Clear (Global)";
        public static string CoverAllBound => IsZH ? "所有場景已綁定" : "All scenes bound";
        public static string CoverCreateScene => IsZH ? "建立場景" : "Create Scene";
        public static string CoverOpenScene => IsZH ? "開啟場景" : "Open Scene";
        public static string CoverOpenEdit => IsZH ? "開啟編輯" : "Edit";
        public static string CoverCreatePrefab => IsZH ? "建立 UI 預置物" : "Create UI Prefab";
        public static string CoverSortOrder => IsZH ? "排序值" : "Sort Order";
        public static string CoverSortOrderHint => IsZH ? "值大的渲染在上面（前景）" : "Higher values render on top";
        public static string InspAutoShowCovers => IsZH ? "自動開啟 Cover" : "Auto Show Covers";
        public static string InspAutoShowCoversHint => IsZH ? "進入此場景時自動開啟的 Cover" : "Covers to open when entering this scene";
        public static string InspAutoShowAdd => IsZH ? "＋ 新增" : "＋ Add";
        public static string InspAutoShowNone => IsZH ? "（無自動開啟）" : "(none)";
        public static string InspAutoShowAllAdded => IsZH ? "所有 Cover 已加入" : "All covers added";

        // ─────────────────────────────────
        //  對話框
        // ─────────────────────────────────
        public static string DlgOk => IsZH ? "確定" : "OK";
        public static string DlgCancel => IsZH ? "取消" : "Cancel";
        public static string DlgDelete => IsZH ? "刪除" : "Delete";
        public static string DlgContinue => IsZH ? "繼續" : "Continue";
        public static string DlgCreateAll => IsZH ? "建立全部" : "Create All";

        // ─────────────────────────────────
        //  右鍵選單
        // ─────────────────────────────────
        public static string CtxAddScene => IsZH ? "新增場景節點" : "Add Scene Node";
        public static string CtxAddEnd => IsZH ? "新增結束節點" : "Add End Node";
        public static string CtxSetStart => IsZH ? "設為起點" : "Set as Start";
        public static string CtxDeleteEdge => IsZH ? "刪除連線" : "Delete Edge";

        // ─────────────────────────────────
        //  節點狀態
        // ─────────────────────────────────
        public static string NodeStartEntry => IsZH ? "遊戲啟動入口" : "Game Entry";
        public static string NodeEndPoint => IsZH ? "流程終點" : "End Point";
        public static string NodeEdgeCount(int outC, int inC) => IsZH ? $"出: {outC} 條  |  入: {inC} 條" : $"Out: {outC}  |  In: {inC}";

        // ─────────────────────────────────
        //  邊線轉場標籤
        // ─────────────────────────────────
        public static string EdgeFade => IsZH ? "淡入淡出" : "Fade";
        public static string EdgeSlideL => IsZH ? "← 左滑" : "← Left";
        public static string EdgeSlideR => IsZH ? "右滑 →" : "Right →";
        public static string EdgeSlideU => IsZH ? "↑ 上滑" : "↑ Up";
        public static string EdgeSlideD => IsZH ? "↓ 下滑" : "↓ Down";
        public static string EdgeNoMat => IsZH ? "未指定材質" : "No Material";

        // ─────────────────────────────────
        //  端口名稱
        // ─────────────────────────────────
        public static string PortIn => IsZH ? "入" : "In";
        public static string PortOut => IsZH ? "出" : "Out";

        // ─────────────────────────────────
        //  對話框訊息
        // ─────────────────────────────────
        public static string DlgNameDuplicate => IsZH ? "名稱重複" : "Duplicate Name";
        public static string DlgNameDuplicateMsg(string n) => IsZH ? $"流程圖中已有 \"{n}\" 節點！請換一個名稱。" : $"Node \"{n}\" already exists. Please use another name.";
        public static string DlgError => IsZH ? "錯誤" : "Error";
        public static string DlgFmMissing => IsZH ? "FlowManager 場景不存在，請先重建。" : "FlowManager scene not found. Please rebuild.";
        public static string DlgRebuildFail => IsZH ? "重建失敗" : "Rebuild Failed";
        public static string DlgAllReady => IsZH ? "全部就緒" : "All Ready";
        public static string DlgAllReadyMsg => IsZH ? "所有場景檔皆已建立。" : "All scene files are created.";
        public static string DlgGenScenes => IsZH ? "產生場景" : "Generate Scenes";
        public static string DlgGenDone => IsZH ? "完成" : "Done";
        public static string DlgLoadScenes => IsZH ? "載入場景" : "Load Scenes";
        public static string DlgLoadScenesMsg => IsZH ? "這將清除現有場景節點並從 Build Settings 載入，是否繼續？" : "This will clear existing nodes and load from Build Settings. Continue?";
        public static string DlgLoadDone => IsZH ? "載入完成" : "Load Complete";
        public static string DlgDeleteCover => IsZH ? "刪除彈出層" : "Delete Cover";
        public static string DlgDeleteCoverMsg(string n) => IsZH ? $"確定刪除「{n}」？" : $"Delete \"{n}\"?";

        // ─────────────────────────────────
        //  SceneNameInputWindow
        // ─────────────────────────────────
        public static string InputTitle => IsZH ? "新增場景" : "Add Scene";
        public static string InputLabel => IsZH ? "場景名稱：" : "Scene Name:";
        public static string InputConfirm => IsZH ? "確認" : "Confirm";
        public static string InputEmptyErr => IsZH ? "場景名稱不能為空！" : "Scene name cannot be empty!";
        public static string InputInvalidChar(char c) => IsZH ? $"場景名稱包含非法字元：{c}" : $"Invalid character in name: {c}";

        // ─────────────────────────────────
        //  Trigger Inspector 欄位
        // ─────────────────────────────────
        public static string TrigSectionTransition  => IsZH ? "轉場設定"     : "Transition";
        public static string TrigSectionCollision   => IsZH ? "碰撞設定"     : "Collision";
        public static string TrigSectionAutoTrigger => IsZH ? "自動觸發設定" : "Auto Trigger";
        public static string TrigSectionCover       => IsZH ? "Cover 設定"  : "Cover";
        public static string TrigSectionDebug       => IsZH ? "調試"         : "Debug";
        public static string TrigFieldTargetScene   => IsZH ? "目標場景"     : "Target Scene";
        public static string TrigFieldAllowRetrigger => IsZH ? "允許重複觸發" : "Allow Retrigger";
        public static string TrigFieldTriggerTag    => IsZH ? "觸發標籤"     : "Trigger Tag";
        public static string TrigFieldDelay         => IsZH ? "延遲秒數"     : "Delay (s)";
        public static string TrigFieldAutoFire      => IsZH ? "自動觸發"     : "Auto Fire";
        public static string TrigAutoFireHint       => IsZH ? "關閉時，物件啟用後不會自動轉場；請外部呼叫 Transition() 或從 UnityEvent 拉線觸發。" : "When off, this object will NOT auto-fire on enable. Call Transition() externally or wire a UnityEvent to it.";
        // TrigSectionInteract / TrigFieldInteractKey / TrigFieldPromptUI 已於 v0.7.7b 移除（隨 InteractionTransitionTrigger 一起）
        public static string TrigFieldCoverName     => IsZH ? "Cover 名稱"   : "Cover Name";
        public static string TrigFieldAction        => IsZH ? "觸發動作"     : "Action";

        // ─────────────────────────────────
        //  SceneEventEditor
        // ─────────────────────────────────
        public static string SeSceneName => IsZH ? "場景名稱" : "Scene Name";
        public static string SeDebugLog => IsZH ? "顯示 Debug Log" : "Debug Log";
        public static string SeConnectedScenes(int c) => IsZH ? $"可關聯場景（{c}）" : $"Connected Scenes ({c})";
        public static string SeNoConnected => IsZH ? "尚無關聯場景。請在「場景流程圖」中建立連線。" : "No connected scenes. Create edges in the flow editor.";
        public static string SeBtnTest => IsZH ? "▶ 測試" : "▶ Test";
        public static string SeBtnReload => IsZH ? "↻ 重載" : "↻ Reload";
        public static string SeBtnAddTrigger => IsZH ? "＋ 觸發 ▾" : "＋ Trigger ▾";
        public static string SeSelfLabel(string scene) => IsZH ? $"↻ {scene}（自己 / 重載）" : $"↻ {scene} (self / reload)";
        public static string SeHint => IsZH ? "「▶ 測試」需在 Play Mode。「＋ 觸發」可在 Edit Mode 產生觸發物件。第一列為自己（重載）。" : "Test requires Play Mode. Trigger creates objects in Edit Mode. First row is self (reload).";
        public static string SeCovers(int c) => IsZH ? $"可用 Cover（{c}）" : $"Available Cover ({c})";
        public static string SeNoCover => IsZH ? "尚無可用 Cover。請在「場景流程圖」中建立 Cover 節點並連線。" : "No Cover available. Create Cover in the flow editor.";
        public static string SeBtnOpen => IsZH ? "▶ 開啟" : "▶ Open";

        // 觸發器類型
        public static string TrigAuto => IsZH ? "自動轉場（無條件）" : "Auto Transition";
        public static string TrigButton => IsZH ? "UI 按鈕" : "UI Button";
        public static string TrigCollider3D => IsZH ? "碰撞觸發器 (3D)" : "Collider Trigger (3D)";
        public static string TrigCollider2D => IsZH ? "碰撞觸發器 (2D)" : "Collider Trigger (2D)";
        // TrigInteract2D / TrigInteract3D 已於 v0.7.7b 移除（隨 InteractionTransitionTrigger 一起）

        // ─────────────────────────────────
        //  Hybrid 轉場設定（v0.7.8b）
        // ─────────────────────────────────
        public static string TransModeEnter       => IsZH ? "▶ 進場" : "▶ Enter";
        public static string TransModeExit        => IsZH ? "◀ 離場" : "◀ Exit";
        public static string TransSectionDefault  => IsZH ? "── 場景預設轉場 ──" : "── Default Transitions ──";
        public static string TransSceneHelpEnter  => IsZH ? "此場景被進入時的預設效果。" : "Default effect when entering this scene.";
        public static string TransSceneHelpExit   => IsZH ? "從此場景離開時的預設效果。" : "Default effect when leaving this scene.";
        public static string TransSceneHelpHint   => IsZH ? "某條連線需特殊效果時可在該 edge 上勾「覆寫」。" : "For special per-edge effect, enable Override on that edge.";

        public static string EdgeModeDefault      => IsZH ? "◆ 場景預設" : "◆ Scene Default";
        public static string EdgeModeOverride     => IsZH ? "✎ 覆寫" : "✎ Override";
        public static string EdgePreviewTitle     => IsZH ? "實際播放（唯讀）" : "Currently Playing (read-only)";
        public static string EdgePreviewExit(string scene, string effect) => IsZH ? $"◀ {scene} 離場 → {effect}" : $"◀ {scene} Exit → {effect}";
        public static string EdgePreviewEnter(string scene, string effect) => IsZH ? $"▶ {scene} 進場 → {effect}" : $"▶ {scene} Enter → {effect}";
        public static string EdgePreviewHint      => IsZH ? "修改請回對應場景節點調整" : "Edit → adjust respective scene nodes";
        public static string EdgeOverrideHint     => IsZH ? "⚠ 此連線不使用場景預設，自訂進出。" : "⚠ This edge bypasses scene defaults.";
        public static string TrigCoverOpen => IsZH ? "開啟按鈕" : "Open Button";
        public static string TrigCoverClose => IsZH ? "關閉按鈕" : "Close Button";
        public static string TrigCoverToggle => IsZH ? "切換按鈕" : "Toggle Button";

        // ─────────────────────────────────
        //  CoverControllerEditor
        // ─────────────────────────────────
        public static string CcTitle => IsZH ? "彈出層控制器" : "Cover Controller";
        public static string CcOnOpenEvent => IsZH ? "開啟完成事件" : "On Open Event";
        public static string CcOnCloseEvent => IsZH ? "關閉開始事件" : "On Close Event";
        public static string CcOnVisibility => IsZH ? "顯示狀態變更 (bool)" : "Visibility Changed (bool)";
        public static string CcQuickCreate => IsZH ? "快速建立" : "Quick Create";
        public static string CcSectionSelf => IsZH ? "本 Cover" : "This Cover";
        public static string CcSectionOther => IsZH ? "呼叫其他 Cover" : "Other Cover";
        public static string CcSectionUI => IsZH ? "UI 元素" : "UI Elements";
        public static string CcBtnClose => IsZH ? "關閉按鈕" : "Close Btn";
        public static string CcBtnConfirm => IsZH ? "確認按鈕" : "Confirm Btn";
        public static string CcOtherCoverBtn => IsZH ? "＋ 建立 Cover 按鈕 ▾" : "＋ Cover Button ▾";
        public static string CcNoCover => IsZH ? "（無可選 Cover）" : "(no covers)";
        public static string CcActionShow => IsZH ? "開啟" : "Show";
        public static string CcActionHide => IsZH ? "關閉" : "Hide";
        public static string CcActionToggle => IsZH ? "切換" : "Toggle";
        public static string CcBtnCancel => IsZH ? "取消按鈕" : "Cancel Btn";
        public static string CcTitleText => IsZH ? "標題文字 ▾" : "Title Text ▾";
        public static string CcContentText => IsZH ? "內容文字 ▾" : "Content Text ▾";
        public static string CcTestSection => IsZH ? "測試" : "Test";
        public static string CcShowing => IsZH ? "● 顯示中" : "● Visible";
        public static string CcHidden => IsZH ? "○ 隱藏中" : "○ Hidden";

        // ─────────────────────────────────
        //  FlowManager Inspector
        // ─────────────────────────────────
        public static string FmDebugLog => IsZH ? "顯示 Debug Log" : "Debug Log";
        public static string FmStartScene => IsZH ? "起始場景" : "Start Scene";
        public static string FmStartNotSet => IsZH ? "⚠ 未設定（請在場景流程圖中設定）" : "⚠ Not set (set in flow editor)";
        public static string FmNoData => IsZH ? "找不到 SceneBlueprintData。" : "SceneBlueprintData not found.";
        public static string FmOverview(int n, int e, int l) => IsZH ? $"場景總覽（{n} 節點 / {e} 連線 / {l} 已建立）" : $"Overview ({n} nodes / {e} edges / {l} linked)";
        public static string FmRuntime => IsZH ? "運行時狀態" : "Runtime State";
        public static string FmCurrentScene => IsZH ? "當前場景" : "Current Scene";
        public static string FmTransitioning => IsZH ? "轉場中" : "Transitioning";
        public static string FmYes => IsZH ? "是" : "Yes";
        public static string FmNo => IsZH ? "否" : "No";
        public static string FmHistory => IsZH ? "場景歷史" : "Scene History";
        public static string FmSceneEvent => IsZH ? "SceneEvent" : "SceneEvent";
        public static string FmNone => IsZH ? "無" : "None";
        public static string FmOpenEditor => IsZH ? "開啟場景流程圖" : "Open Flow Editor";
        public static string FmRefresh => IsZH ? "刷新" : "Refresh";

        // ─────────────────────────────────
        //  場景節點右鍵（GraphView 額外）
        // ─────────────────────────────────
        public static string CtxOpenScene => IsZH ? "開啟場景" : "Open Scene";
        public static string CtxCreateAndOpen => IsZH ? "建立並開啟場景" : "Create & Open Scene";

        // ─────────────────────────────────
        //  Build Settings 圖例說明
        // ─────────────────────────────────
        // ─────────────────────────────────
        //  視窗
        // ─────────────────────────────────
        public static string WindowTitle => IsZH ? "\u2699 \u5834\u666f\u6d41\u7a0b\u5716" : "\u2699 Scene Flow";

        // ─────────────────────────────────
        //  匯入匯�� / 預設
        // ─────────────────────────────────
        // 以下 JSON 匯入匯出 / 存讀預設相關 Locale 字串於 v0.7.9b 移除（方法同步刪除）
        public static string LoadBuildDone(int c) => IsZH ? $"已從 Build Settings 載入 {c} 個遊戲場景" : $"Loaded {c} scenes from Build Settings";
        public static string GenWillCreate(int c) => IsZH ? $"將為以下 {c} 個節點建立場景檔：\n" : $"Will create scene files for {c} nodes:\n";
        public static string GenCreated(int c) => IsZH ? $"已建立 {c} 個場景檔。" : $"Created {c} scene files.";
        public static string NewCoverDefault => IsZH ? "新彈出層" : "NewCover";
        public static string HelpBoxPreview(string label, float dur) => IsZH ? $"▶ {label}　時長 {dur:F1}s" : $"▶ {label}　{dur:F1}s";

        public static string LegendInGraph => IsZH ? "在流程圖中" : "In Flow Graph";
        public static string LegendNotInGraph => IsZH ? "不在流程圖中" : "Not in Graph";
        public static string LegendFileMissing => IsZH ? "檔案不存在" : "File Missing";
    }
    #endregion 語系切換
}
#endif
