using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CatzTools
{
    #region JSON 交換格式
    /// <summary>
    /// 場景流程 JSON 交換格式 — GDS 和 Unity 共用
    /// </summary>
    public static class SceneFlowJson
    {
        #region 匯出
        /// <summary>
        /// 將 SceneBlueprintData 匯出為 JSON 字串
        /// </summary>
        public static string Export(SceneBlueprintData data)
        {
            var export = new SceneFlowExportData
            {
                version = "1.0",
                generator = "Unity",
                nodes = new List<SceneFlowNodeJson>(),
                edges = new List<SceneFlowEdgeJson>(),
                viewport = new SceneFlowViewport { x = 0, y = 0, zoom = 1 }
            };

            if (data.nodes != null)
            {
                foreach (var node in data.nodes)
                {
                    export.nodes.Add(new SceneFlowNodeJson
                    {
                        id = node.id,
                        sceneName = node.sceneName,
                        type = node.nodeType.ToString().ToLower(),
                        position = new SceneFlowPosition { x = node.position.x, y = node.position.y },
                        isStartNode = node.isStartNode,
                        description = node.description ?? "",
                        tags = node.tags ?? new List<string>()
                    });
                }
            }

            if (data.edges != null)
            {
                foreach (var edge in data.edges)
                {
                    export.edges.Add(new SceneFlowEdgeJson
                    {
                        id = edge.id,
                        source = edge.source,
                        target = edge.target,
                        label = edge.label ?? "",
                        data = new SceneFlowEdgeData
                        {
                            condition = edge.condition ?? "",
                            action = edge.action ?? ""
                        }
                    });
                }
            }

            return JsonUtility.ToJson(export, true);
        }
        #endregion 匯出

        #region 匯入
        /// <summary>
        /// 從 JSON 字串匯入至 SceneBlueprintData。
        /// FlowManager 不在 nodes 中，flowManagerScene 欄位不受影響。
        /// </summary>
        public static void Import(string json, SceneBlueprintData data)
        {
            var import = JsonUtility.FromJson<SceneFlowExportData>(json);
            if (import == null)
            {
                Debug.LogError("JSON 解析失敗");
                return;
            }

            data.nodes = new List<SceneNode>();
            data.edges = new List<SceneEdge>();

            // 匯入節點（跳過 FlowManager，它由 EditorWindow 獨立管理）
            foreach (var nodeJson in import.nodes)
            {
                if (nodeJson.sceneName == "FlowManager") continue;

                var node = new SceneNode
                {
                    id = nodeJson.id,
                    sceneName = nodeJson.sceneName,
                    nodeType = ParseNodeType(nodeJson.type),
                    position = new Vector2(nodeJson.position.x, nodeJson.position.y),
                    isStartNode = nodeJson.isStartNode,
                    description = nodeJson.description,
                    tags = nodeJson.tags ?? new List<string>()
                };

#if UNITY_EDITOR
                TryMatchSceneAsset(node);
#endif

                data.nodes.Add(node);
            }

            // 匯入連線
            foreach (var edgeJson in import.edges)
            {
                data.edges.Add(new SceneEdge
                {
                    id = edgeJson.id,
                    source = edgeJson.source,
                    target = edgeJson.target,
                    label = edgeJson.label,
                    condition = edgeJson.data?.condition ?? "",
                    action = edgeJson.data?.action ?? ""
                });
            }

            data.SyncEdgesToConnectedIds();
        }

        /// <summary>
        /// 解析節點類型字串
        /// </summary>
        private static SceneNodeType ParseNodeType(string type)
        {
            return type?.ToLower() switch
            {
                "end" => SceneNodeType.End,
                _ => SceneNodeType.Scene
            };
        }

#if UNITY_EDITOR
        /// <summary>
        /// 嘗試自動配對場景資源
        /// </summary>
        private static void TryMatchSceneAsset(SceneNode node)
        {
            string[] guids = AssetDatabase.FindAssets($"t:SceneAsset {node.sceneName}");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (asset != null && asset.name == node.sceneName)
                {
                    node.sceneAsset = asset;
                    return;
                }
            }
        }
#endif
        #endregion 匯入
    }
    #endregion JSON 交換格式

    #region JSON 序列化結構
    /// <summary>
    /// JSON 匯出根結構
    /// </summary>
    [Serializable]
    public class SceneFlowExportData
    {
        public string version;
        public string generator;
        public List<SceneFlowNodeJson> nodes;
        public List<SceneFlowEdgeJson> edges;
        public SceneFlowViewport viewport;
    }

    /// <summary>
    /// JSON 節點
    /// </summary>
    [Serializable]
    public class SceneFlowNodeJson
    {
        public string id;
        public string sceneName;
        public string type;
        public SceneFlowPosition position;
        public bool isStartNode;
        public string description;
        public List<string> tags;
    }

    /// <summary>
    /// JSON 連線
    /// </summary>
    [Serializable]
    public class SceneFlowEdgeJson
    {
        public string id;
        public string source;
        public string target;
        public string label;
        public SceneFlowEdgeData data;
    }

    /// <summary>
    /// JSON 連線附加資料
    /// </summary>
    [Serializable]
    public class SceneFlowEdgeData
    {
        public string condition;
        public string action;
    }

    /// <summary>
    /// JSON 位置
    /// </summary>
    [Serializable]
    public class SceneFlowPosition
    {
        public float x;
        public float y;
    }

    /// <summary>
    /// JSON 視口
    /// </summary>
    [Serializable]
    public class SceneFlowViewport
    {
        public float x;
        public float y;
        public float zoom;
    }
    #endregion JSON 序列化結構
}
