using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CatzTools.GameFlow
{
    #region 場景節點類型
    /// <summary>
    /// 場景節點類型
    /// </summary>
    public enum SceneNodeType
    {
        /// <summary>場景節點（一般遊戲場景）</summary>
        Scene,
        /// <summary>結束節點（流程終點，不需場景檔）</summary>
        End,
        /// <summary>起始節點（固定，代表遊戲啟動入口，不可刪除）</summary>
        Start,
        /// <summary>Cover 節點（彈出式 UI 面板，不切換場景）</summary>
        PopCover
    }
    #endregion 場景節點類型

    #region Cover 來源類型
    /// <summary>
    /// Cover 來源類型
    /// </summary>
    public enum CoverSourceType
    {
        /// <summary>預置物 — 實例化在 CoverCanvas 下</summary>
        [UnityEngine.InspectorName("預置物")]
        Prefab,
        /// <summary>場景 — Additive 載入疊加在當前場景上</summary>
        [UnityEngine.InspectorName("場景")]
        Scene
    }
    #endregion Cover 來源類型

    #region 場景藍圖資料結構
    /// <summary>
    /// 場景藍圖資料 - ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "SceneBlueprintData", menuName = "CatzTools/場景藍圖資料")]
    [System.Serializable]
    public class SceneBlueprintData : ScriptableObject
    {
        #region FlowManager（底層管理場景，不參與節點圖）
#if UNITY_EDITOR
        /// <summary>
        /// FlowManager 場景資源（底層常駐場景，Build Settings 第一個，不參與流程節點圖）
        /// </summary>
        [SerializeField]
        public SceneAsset flowManagerScene;
#endif

        /// <summary>
        /// FlowManager 場景路徑（Runtime 用）
        /// </summary>
        [SerializeField]
        public string flowManagerScenePath = "Assets/Scenes/FlowManager.unity";

        /// <summary>
        /// FlowManager 相機的 Tag（在 Rebuild 時套用）。
        /// 預設 "Untagged" — 避免跟遊戲場景的主相機（CameraRig / 自訂主相機）衝突。
        /// 若遊戲中沒有其他相機（純測試場景），可設為 "MainCamera" 讓 `Camera.main` 有東西可回傳。
        /// </summary>
        [SerializeField]
        public string flowManagerCameraTag = "Untagged";
        #endregion FlowManager（底層管理場景，不參與節點圖）

        #region 起始場景
        /// <summary>
        /// 起始場景名稱（FlowManager 啟動後第一個載入的場景）
        /// </summary>
        [SerializeField]
        public string startSceneName = "";
        #endregion 起始場景

        #region ServiceLocator 啟動清單
        /// <summary>
        /// （Legacy v0.x）Start 節點服務啟動清單純字串版本。新版改用 startServices；
        /// 仍保留以便舊資料能無痛載入並自動 migration。
        /// </summary>
        [SerializeField]
        public List<string> startServiceTypeNames = new List<string>();

        /// <summary>
        /// Start 節點要求 ServiceLocator 載入的服務清單（按順序）。
        /// 每筆條目可選擇性覆寫 priority（不填 = 沿用 [AutoRegister] 屬性原值）。
        /// 空清單 = 全自動反射發現（沿用舊行為）。
        /// </summary>
        [SerializeField]
        public List<ServiceManifestEntry> startServices = new List<ServiceManifestEntry>();

        /// <summary>
        /// 取得 startServices；若是空的且 legacy startServiceTypeNames 有資料則自動 migrate。
        /// </summary>
        public List<ServiceManifestEntry> GetOrMigrateStartServices()
        {
            if (startServices == null) startServices = new List<ServiceManifestEntry>();
            if (startServices.Count == 0 && startServiceTypeNames != null && startServiceTypeNames.Count > 0)
            {
                foreach (var name in startServiceTypeNames)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    startServices.Add(new ServiceManifestEntry { typeName = name });
                }
                // 遷移完成，清掉 legacy 欄位防止反覆遷移
                startServiceTypeNames.Clear();
            }
            return startServices;
        }
        #endregion ServiceLocator 啟動清單

        /// <summary>
        /// 場景節點列表（不含 FlowManager）
        /// </summary>
        [SerializeField]
        public List<SceneNode> nodes = new List<SceneNode>();

        /// <summary>
        /// 場景連線列表
        /// </summary>
        [SerializeField]
        public List<SceneEdge> edges = new List<SceneEdge>();

        #region 查詢方法
        /// <summary>
        /// 依 ID 查找節點
        /// </summary>
        public SceneNode FindNodeById(string id) =>
            nodes?.FirstOrDefault(n => n.id == id);

        /// <summary>
        /// 依場景名稱查找節點
        /// </summary>
        public SceneNode FindNodeByName(string sceneName) =>
            nodes?.FirstOrDefault(n => n.sceneName == sceneName);

        /// <summary>
        /// 取得節點的所有出邊
        /// </summary>
        public List<SceneEdge> GetOutgoingEdges(string nodeId) =>
            edges?.Where(e => e.source == nodeId).ToList() ?? new List<SceneEdge>();

        /// <summary>
        /// 取得節點的所有入邊
        /// </summary>
        public List<SceneEdge> GetIncomingEdges(string nodeId) =>
            edges?.Where(e => e.target == nodeId).ToList() ?? new List<SceneEdge>();

        /// <summary>
        /// 依名稱查找 PopCover 節點
        /// </summary>
        public SceneNode FindCoverByName(string coverName) =>
            nodes?.FirstOrDefault(n => n.nodeType == SceneNodeType.PopCover && n.sceneName == coverName);

        /// <summary>
        /// 取得所有 Cover 節點（所有場景皆可呼叫任何 Cover）。
        /// </summary>
        public List<SceneNode> GetAvailableCovers(string sceneName = null) =>
            nodes?.Where(n => n.nodeType == SceneNodeType.PopCover)
                .ToList() ?? new List<SceneNode>();

        /// <summary>
        /// 取得某節點可以轉場到的目標節點 ID（單向 source→target）
        /// </summary>
        public List<string> GetReachableTargets(string nodeId) =>
            edges?.Where(e => e.source == nodeId)
                .Select(e => e.target)
                .ToList() ?? new List<string>();

        /// <summary>
        /// 同步 edges 到 connectedNodeIds（向後相容）
        /// </summary>
        public void SyncEdgesToConnectedIds()
        {
            if (nodes == null || edges == null) return;

            foreach (var node in nodes)
            {
                node.connectedNodeIds = GetReachableTargets(node.id);
            }
        }

        /// <summary>
        /// Hybrid 轉場模型遷移（v0.7.8b）：既有 edge 若有設轉場但 useOverride=false，
        /// 自動轉為 useOverride=true，保留原行為。一次性標記存於 <see cref="_hybridMigrated"/>。
        /// </summary>
        public void MigrateEdgesToHybridModel()
        {
            if (_hybridMigrated) return;
            if (edges != null)
            {
                foreach (var e in edges)
                {
                    if (e == null || e.useOverride) continue;
                    if (e.transition != null && e.transition.type != TransitionType.None)
                        e.useOverride = true;
                }
            }
            _hybridMigrated = true;
        }

        [SerializeField, HideInInspector]
        private bool _hybridMigrated;

        /// <summary>
        /// 從 connectedNodeIds 建立 edges（遷移用）
        /// </summary>
        public void MigrateConnectedIdsToEdges()
        {
            if (nodes == null) return;
            if (edges == null) edges = new List<SceneEdge>();

            foreach (var node in nodes)
            {
                if (node.connectedNodeIds == null) continue;

                foreach (var targetId in node.connectedNodeIds)
                {
                    bool exists = edges.Any(e => e.source == node.id && e.target == targetId);
                    if (!exists)
                    {
                        edges.Add(new SceneEdge(node.id, targetId));
                    }
                }
            }
        }
        #endregion 查詢方法
    }

    /// <summary>
    /// 場景節點
    /// </summary>
    [System.Serializable]
    public class SceneNode
    {
        /// <summary>
        /// 節點唯一識別碼
        /// </summary>
        [SerializeField]
        public string id;

        /// <summary>
        /// 場景名稱
        /// </summary>
        [SerializeField]
        public string sceneName;

        /// <summary>
        /// 節點類型
        /// </summary>
        [SerializeField]
        public SceneNodeType nodeType = SceneNodeType.Scene;

#if UNITY_EDITOR
        /// <summary>
        /// 場景資源參考 (僅在編輯器使用)
        /// </summary>
        [SerializeField]
        public SceneAsset sceneAsset;
#endif

        /// <summary>
        /// 節點在編輯器上的位置
        /// </summary>
        [SerializeField]
        public Vector2 position;

        /// <summary>
        /// 是否為起始場景
        /// </summary>
        [SerializeField]
        public bool isStartNode;

        /// <summary>
        /// 連接的節點ID列表（向後相容，由 edges 同步）
        /// </summary>
        [SerializeField]
        public List<string> connectedNodeIds = new List<string>();

        /// <summary>
        /// 節點描述 (選填)
        /// </summary>
        [SerializeField]
        public string description;

        /// <summary>
        /// 節點標籤 (選填)
        /// </summary>
        [SerializeField]
        public List<string> tags = new List<string>();

        /// <summary>
        /// 進場轉場預設（任何 edge 進入此場景時的預設效果；edge 可勾選覆寫）
        /// </summary>
        [SerializeField]
        public TransitionSettings defaultEnter = new TransitionSettings { type = TransitionType.Fade, duration = 1f, color = Color.black };

        /// <summary>
        /// 離場轉場預設（從此場景離開到任何 edge 的預設效果；edge 可勾選覆寫）
        /// </summary>
        [SerializeField]
        public TransitionSettings defaultExit = new TransitionSettings { type = TransitionType.Fade, duration = 1f, color = Color.black };

        #region PopCover 設定
        /// <summary>Cover 來源類型（僅 PopCover 節點）</summary>
        [SerializeField]
        public CoverSourceType coverSourceType;

        /// <summary>Cover Prefab 參考（CoverSourceType.Prefab 用）</summary>
        [SerializeField]
        public GameObject coverPrefab;

#if UNITY_EDITOR
        /// <summary>Cover 場景資源（CoverSourceType.Scene 用）</summary>
        [SerializeField]
        public SceneAsset coverSceneAsset;
#endif

        /// <summary>Cover 場景名稱（Runtime 用，由編輯器同步）</summary>
        [SerializeField]
        public string coverSceneName;

        /// <summary>Cover 開啟動畫時長（秒），CanvasGroup 淡入</summary>
        [SerializeField]
        public float coverOpenDuration = 0.3f;

        /// <summary>Cover 關閉動畫時長（秒），CanvasGroup 淡出</summary>
        [SerializeField]
        public float coverCloseDuration = 0.2f;

        // Legacy（舊版轉場設定，保留避免序列化報錯，不再使用）
        [SerializeField, HideInInspector]
        public TransitionSettings coverOpenTransition;
        [SerializeField, HideInInspector]
        public TransitionSettings coverCloseTransition;

        /// <summary>
        /// 綁定的場景名稱清單（哪些場景可用此 Cover）。
        /// 空清單 = 全域可用（所有場景都能開啟）。
        /// </summary>
        [SerializeField]
        public List<string> boundSceneNames = new List<string>();

        /// <summary>
        /// Cover 排序值（Sibling Index）。值大的渲染在上面（前景）。
        /// 僅 PopCover 節點使用。
        /// </summary>
        [SerializeField]
        public int sortOrder;
        #endregion PopCover 設定

        #region 場景自動開啟 Cover
        /// <summary>
        /// 進入此場景時自動開啟的 Cover 名稱清單。
        /// 僅 Scene 節點使用。
        /// </summary>
        [SerializeField]
        public List<string> autoShowCovers = new List<string>();
        #endregion 場景自動開啟 Cover

        /// <summary>
        /// 建構函式
        /// </summary>
        public SceneNode()
        {
            id = Guid.NewGuid().ToString();
            connectedNodeIds = new List<string>();
            tags = new List<string>();
            position = new Vector2(400, 300);
        }

        /// <summary>
        /// 建構函式 (帶場景名稱)
        /// </summary>
        public SceneNode(string sceneName) : this()
        {
            this.sceneName = sceneName;
        }
    }
    #endregion 場景藍圖資料結構

    #region 轉場類型
    /// <summary>
    /// 轉場效果類型
    /// </summary>
    public enum TransitionType
    {
        /// <summary>無轉場（直接切換）</summary>
        [UnityEngine.InspectorName("無轉場")]
        None,
        /// <summary>淡入淡出（黑/白/自訂色）</summary>
        [UnityEngine.InspectorName("淡入淡出")]
        Fade,
        /// <summary>從左滑入</summary>
        [UnityEngine.InspectorName("← 左滑")]
        SlideLeft,
        /// <summary>從右滑入</summary>
        [UnityEngine.InspectorName("右滑 →")]
        SlideRight,
        /// <summary>從上滑入</summary>
        [UnityEngine.InspectorName("↑ 上滑")]
        SlideUp,
        /// <summary>從下滑入</summary>
        [UnityEngine.InspectorName("↓ 下滑")]
        SlideDown,
        /// <summary>自訂 Shader 轉場</summary>
        [UnityEngine.InspectorName("自訂 Shader")]
        CustomShader
    }
    #endregion 轉場類型

    #region 轉場設定
    /// <summary>
    /// 轉場設定 — 掛在 SceneEdge 上，決定轉場時的視覺效果
    /// </summary>
    [System.Serializable]
    public class TransitionSettings
    {
        /// <summary>轉場類型</summary>
        [SerializeField]
        public TransitionType type = TransitionType.None;

        /// <summary>轉場時長（秒）</summary>
        [SerializeField]
        public float duration = 0.5f;

        /// <summary>遮罩顏色（Fade 用）</summary>
        [SerializeField]
        public Color color = Color.black;

        /// <summary>自訂 Shader Material（CustomShader 用）</summary>
        [SerializeField]
        public Material customMaterial;

        /// <summary>每條連線獨立的 Shader 屬性覆蓋值</summary>
        [SerializeField]
        public List<ShaderPropertyOverride> shaderOverrides = new();
    }

    /// <summary>
    /// Shader 屬性覆蓋 — 序列化友善的 key-value 結構
    /// </summary>
    [System.Serializable]
    public class ShaderPropertyOverride
    {
        /// <summary>顯示名稱</summary>
        [SerializeField] public string displayName;
        /// <summary>Shader 屬性名稱（如 _Smoothness）</summary>
        [SerializeField] public string propertyName;
        /// <summary>屬性類型</summary>
        [SerializeField] public ShaderPropertyValueType valueType;
        /// <summary>Float / Range 值</summary>
        [SerializeField] public float floatValue;
        /// <summary>Range 最小值</summary>
        [SerializeField] public float rangeMin;
        /// <summary>Range 最大值</summary>
        [SerializeField] public float rangeMax = 1f;
        /// <summary>Color R</summary>
        [SerializeField] public float colorR;
        /// <summary>Color G</summary>
        [SerializeField] public float colorG;
        /// <summary>Color B</summary>
        [SerializeField] public float colorB;
        /// <summary>Color A</summary>
        [SerializeField] public float colorA = 1f;

        /// <summary>Enum 選項名稱（僅 Enum 型用）</summary>
        [SerializeField] public string[] enumNames;
        /// <summary>Enum 選項值（僅 Enum 型用，對應 Shader 的 float 值）</summary>
        [SerializeField] public float[] enumValues;
        /// <summary>Texture 參考（僅 Texture 型用）</summary>
        [SerializeField] public Texture textureValue;

        public Color colorValue
        {
            get => new Color(colorR, colorG, colorB, colorA);
            set { colorR = value.r; colorG = value.g; colorB = value.b; colorA = value.a; }
        }
    }

    /// <summary>Shader 屬性值類型</summary>
    public enum ShaderPropertyValueType { Float, Color, Enum, Texture, Toggle }
    #endregion 轉場設定

    #region 場景連線
    /// <summary>
    /// 場景連線 — 定義兩個場景節點之間的轉場關係
    /// </summary>
    [System.Serializable]
    public class SceneEdge
    {
        /// <summary>
        /// 連線唯一識別碼
        /// </summary>
        [SerializeField]
        public string id;

        /// <summary>
        /// 來源節點 ID
        /// </summary>
        [SerializeField]
        public string source;

        /// <summary>
        /// 目標節點 ID
        /// </summary>
        [SerializeField]
        public string target;

        /// <summary>
        /// 連線標籤（顯示用）
        /// </summary>
        [SerializeField]
        public string label;

        /// <summary>
        /// 轉場條件（選填）
        /// </summary>
        [SerializeField]
        public string condition;

        /// <summary>
        /// 轉場動作（選填）
        /// </summary>
        [SerializeField]
        public string action;

        /// <summary>
        /// 轉場設定（當 <see cref="useOverride"/> = true 時才使用，否則套用兩端場景節點的 defaultExit + defaultEnter）
        /// </summary>
        [SerializeField]
        public TransitionSettings transition = new TransitionSettings();

        /// <summary>
        /// 是否覆寫場景預設轉場。
        /// false（預設）= 走兩端場景節點的 defaultExit + defaultEnter；true = 使用本 edge 的 <see cref="transition"/>。
        /// </summary>
        [SerializeField]
        public bool useOverride;

        /// <summary>
        /// 建構函式
        /// </summary>
        public SceneEdge()
        {
            id = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 建構函式 (帶來源和目標)
        /// </summary>
        public SceneEdge(string source, string target) : this()
        {
            this.source = source;
            this.target = target;
        }
    }
    #endregion 場景連線

    #region ServiceLocator 啟動清單條目
    /// <summary>
    /// SceneBlueprintData.startServices 的單筆條目。
    /// priorityOverride 為空字串 = 沿用 [AutoRegister] 屬性原值；
    /// 有值 = 覆寫該服務的初始化優先序。
    /// 用字串而非 int? 是因為 Unity JsonUtility / SerializeReference 對 nullable struct 支援不一致，
    /// 用 string 最穩。空白 / 非數字皆視為「無覆寫」。
    /// </summary>
    [System.Serializable]
    public class ServiceManifestEntry
    {
        /// <summary>服務類別 FullName（例：CatzTools.DataSys.DataManager）</summary>
        [SerializeField]
        public string typeName;

        /// <summary>覆寫的 priority 值（空字串 = 不覆寫）</summary>
        [SerializeField]
        public string priorityOverride;

        public ServiceManifestEntry() { }
        public ServiceManifestEntry(string typeName) { this.typeName = typeName; }

        /// <summary>取得有效 priority — 有 override 用 override，否則用 fallback（attribute 原值）</summary>
        public int GetEffectivePriority(int fallback)
        {
            if (string.IsNullOrEmpty(priorityOverride)) return fallback;
            return int.TryParse(priorityOverride, out var v) ? v : fallback;
        }

        public bool HasOverride => !string.IsNullOrEmpty(priorityOverride)
                                  && int.TryParse(priorityOverride, out _);
    }
    #endregion ServiceLocator 啟動清單條目
}