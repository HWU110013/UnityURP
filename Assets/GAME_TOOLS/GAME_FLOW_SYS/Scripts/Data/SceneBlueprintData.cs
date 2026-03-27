using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CatzTools
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
        Start
    }
    #endregion 場景節點類型

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
        #endregion FlowManager（底層管理場景，不參與節點圖）

        #region 起始場景
        /// <summary>
        /// 起始場景名稱（FlowManager 啟動後第一個載入的場景）
        /// </summary>
        [SerializeField]
        public string startSceneName = "";
        #endregion 起始場景

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
        None,
        /// <summary>淡入淡出（黑/白/自訂色）</summary>
        Fade,
        /// <summary>從左滑入</summary>
        SlideLeft,
        /// <summary>從右滑入</summary>
        SlideRight,
        /// <summary>從上滑入</summary>
        SlideUp,
        /// <summary>從下滑入</summary>
        SlideDown,
        /// <summary>自訂 Shader 轉場</summary>
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
        /// 轉場設定
        /// </summary>
        [SerializeField]
        public TransitionSettings transition = new TransitionSettings();

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
}