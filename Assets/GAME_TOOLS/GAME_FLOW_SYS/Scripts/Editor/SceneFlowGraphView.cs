#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace CatzTools
{
    #region 場景流程圖
    /// <summary>
    /// 場景流程圖 — GraphView 核心，管理場景節點和連線的視覺化呈現。
    /// FlowManager 不參與節點圖，由 EditorWindow 獨立管理。
    /// </summary>
    public class SceneFlowGraphView : GraphView
    {
        #region 公開事件
        /// <summary>圖表資料變更事件</summary>
        public event Action OnGraphChanged;

        /// <summary>右鍵選單請求新增場景</summary>
        public event Action<Vector2, SceneNodeType> OnRequestAddScene;

        /// <summary>請求設定某場景為起點</summary>
        public event Action<SceneFlowNode> OnRequestSetAsStart;

        /// <summary>雙擊節點請求開啟場景</summary>
        public event Action<SceneNode> OnRequestOpenScene;

        /// <summary>選取變更事件（選到節點或連線時觸發）</summary>
        public event Action<object> OnSelectionChanged;
        #endregion 公開事件

        #region 私有變數
        private SceneBlueprintData _blueprintData;
        private readonly Dictionary<string, SceneFlowNode> _nodeMap = new();
        private bool _hasLoadedOnce;
        private Vector3 _savedViewPosition;
        private Vector3 _savedViewScale;
        #endregion 私有變數

        #region 屬性
        /// <summary>藍圖資料</summary>
        public SceneBlueprintData BlueprintData => _blueprintData;

        /// <summary>
        /// 取得當前視口在 Graph 空間中的矩形
        /// </summary>
        public Rect GetViewportInGraphSpace()
        {
            var viewRect = contentRect;
            if (viewRect.width <= 0 || viewRect.height <= 0) return Rect.zero;

            var transform = contentViewContainer.transform;
            var pos = transform.position;
            var scl = transform.scale;

            if (scl.x == 0 || scl.y == 0) return Rect.zero;

            return new Rect(
                -pos.x / scl.x,
                -pos.y / scl.y,
                viewRect.width / scl.x,
                viewRect.height / scl.y);
        }

        /// <summary>
        /// 平移視口，使指定 Graph 座標置於畫面中央
        /// </summary>
        public void PanToPosition(Vector2 graphPosition)
        {
            var viewRect = contentRect;
            var scl = contentViewContainer.transform.scale;

            var newPos = new Vector3(
                -graphPosition.x * scl.x + viewRect.width * 0.5f,
                -graphPosition.y * scl.y + viewRect.height * 0.5f,
                0);

            UpdateViewTransform(newPos, scl);
        }
        #endregion 屬性

        #region 建構
        /// <summary>
        /// 建構場景流程圖
        /// </summary>
        public SceneFlowGraphView()
        {
            // 基本操作
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // 背景格線
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // 樣式
            style.flexGrow = 1;

            // 雙擊節點開啟場景
            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount != 2) return;

                // 從點擊目標往上找 SceneFlowNode
                var target = evt.target as VisualElement;
                SceneFlowNode clickedNode = null;
                while (target != null && clickedNode == null)
                {
                    clickedNode = target as SceneFlowNode;
                    target = target.parent;
                }

                if (clickedNode != null && clickedNode.SceneData.nodeType != SceneNodeType.End)
                {
                    OnRequestOpenScene?.Invoke(clickedNode.SceneData);
                    evt.StopPropagation();
                }
            });

            // 圖表變更回調
            graphViewChanged = OnGraphViewChanged;

            // 選取變更 — 用多種方式確保能偵測到
            RegisterCallback<MouseUpEvent>(_ => EditorApplication.delayCall += FireSelectionChanged);
            RegisterCallback<KeyUpEvent>(_ => EditorApplication.delayCall += FireSelectionChanged);
        }

        /// <summary>
        /// 觸發選取變更通知
        /// </summary>
        private void FireSelectionChanged()
        {
            var selected = selection;
            if (selected == null || selected.Count == 0)
            {
                OnSelectionChanged?.Invoke(null);
                return;
            }

            var first = selected[0];
            if (first is SceneFlowNode flowNode)
            {
                OnSelectionChanged?.Invoke(flowNode.SceneData);
            }
            else if (first is SceneFlowEdge flowEdge)
            {
                var edgeData = FindEdgeData(flowEdge);
                OnSelectionChanged?.Invoke(edgeData);
            }
            else if (first is Edge defaultEdge)
            {
                // 可能是還沒替換成 SceneFlowEdge 的預設 Edge
                var srcNode = defaultEdge.output?.node as SceneFlowNode;
                var tgtNode = defaultEdge.input?.node as SceneFlowNode;
                if (srcNode != null && tgtNode != null && _blueprintData?.edges != null)
                {
                    var edgeData = _blueprintData.edges.FirstOrDefault(e =>
                        e.source == srcNode.SceneData.id && e.target == tgtNode.SceneData.id);
                    OnSelectionChanged?.Invoke(edgeData);
                }
                else
                {
                    OnSelectionChanged?.Invoke(null);
                }
            }
            else
            {
                OnSelectionChanged?.Invoke(null);
            }
        }
        #endregion 建構

        #region 資料載入
        /// <summary>
        /// 載入藍圖資料並建立節點和連線
        /// </summary>
        public void LoadBlueprintData(SceneBlueprintData data)
        {
            _blueprintData = data;

            // 保存當前視口位置（重建後還原）
            if (_hasLoadedOnce)
            {
                _savedViewPosition = contentViewContainer.transform.position;
                _savedViewScale = contentViewContainer.transform.scale;
            }

            ClearGraph();

            if (data == null || data.nodes == null) return;

            // 確保有 START 節點
            EnsureStartNode(data);

            // 遷移：若有 connectedNodeIds 但沒有 edges，自動建立
            if ((data.edges == null || data.edges.Count == 0) &&
                data.nodes.Any(n => n.connectedNodeIds?.Count > 0))
            {
                data.MigrateConnectedIdsToEdges();
                EditorUtility.SetDirty(data);
            }

            // 建立節點
            foreach (var nodeData in data.nodes)
            {
                var node = new SceneFlowNode(nodeData);
                AddElement(node);
                _nodeMap[nodeData.id] = node;
            }

            // 建立連線
            if (data.edges != null)
            {
                foreach (var edgeData in data.edges)
                {
                    CreateEdgeFromData(edgeData);
                }
            }

            // 還原或初始化視口位置
            if (_hasLoadedOnce)
            {
                // 重建後還原到原本的視口位置
                contentViewContainer.transform.position = _savedViewPosition;
                contentViewContainer.transform.scale = _savedViewScale;
            }
            else
            {
                _hasLoadedOnce = true;
            }
        }

        /// <summary>
        /// 清除所有節點和連線
        /// </summary>
        private void ClearGraph()
        {
            _nodeMap.Clear();

            // 暫時移除回調避免刪除時觸發資料同步
            var savedCallback = graphViewChanged;
            graphViewChanged = null;

            // 快照後再刪除，避免迭代中修改集合
            var elements = graphElements.ToList();
            foreach (var element in elements)
            {
                RemoveElement(element);
            }

            graphViewChanged = savedCallback;
        }
        #endregion 資料載入

        #region 節點操作
        /// <summary>
        /// 新增場景節點
        /// </summary>
        public SceneFlowNode AddSceneNode(string sceneName, Vector2 position,
            SceneNodeType nodeType = SceneNodeType.Scene)
        {
            var nodeData = new SceneNode(sceneName);
            nodeData.position = position;
            nodeData.nodeType = nodeType;

            _blueprintData.nodes.Add(nodeData);

            var node = new SceneFlowNode(nodeData);
            AddElement(node);
            _nodeMap[nodeData.id] = node;

            NotifyChanged();
            return node;
        }

        /// <summary>
        /// 覆寫右鍵選單：節點操作、連線轉場設定、空白處新增
        /// </summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // === 檢查是否點到 Edge（連線）===
            SceneFlowEdge clickedEdge = null;
            {
                var ve = evt.target as VisualElement;
                while (ve != null && clickedEdge == null)
                {
                    clickedEdge = ve as SceneFlowEdge;
                    ve = ve.parent;
                }
            }

            if (clickedEdge != null)
            {
                BuildEdgeContextMenu(evt, clickedEdge);
                return;
            }

            // === 檢查是否點到 Node ===
            SceneFlowNode clickedNode = null;
            {
                var ve = evt.target as VisualElement;
                while (ve != null && clickedNode == null)
                {
                    clickedNode = ve as SceneFlowNode;
                    ve = ve.parent;
                }
            }

            if (clickedNode != null)
            {
                // 開啟場景（End 不行）
                if (clickedNode.SceneData.nodeType != SceneNodeType.End)
                {
                    string openLabel = clickedNode.SceneData.sceneAsset != null
                        ? "開啟場景" : "建立並開啟場景";
                    evt.menu.AppendAction(openLabel, _ =>
                    {
                        OnRequestOpenScene?.Invoke(clickedNode.SceneData);
                    });
                }

                // 設為起點（End 不行，已是起點也不行）
                if (clickedNode.SceneData.nodeType == SceneNodeType.Scene &&
                    !clickedNode.SceneData.isStartNode)
                {
                    evt.menu.AppendAction("設為起點", _ =>
                    {
                        OnRequestSetAsStart?.Invoke(clickedNode);
                    });
                }

                evt.menu.AppendSeparator();
            }
            else
            {
                // 點在空白處：新增節點
                var localPos = contentViewContainer.WorldToLocal(evt.localMousePosition);

                evt.menu.AppendAction("新增場景節點", _ =>
                    OnRequestAddScene?.Invoke(localPos, SceneNodeType.Scene));
                evt.menu.AppendAction("新增結束節點", _ =>
                    OnRequestAddScene?.Invoke(localPos, SceneNodeType.End));
                evt.menu.AppendSeparator();
            }

            base.BuildContextualMenu(evt);
        }

        /// <summary>
        /// 連線右鍵選單 — 開啟轉場設定面板
        /// </summary>
        private void BuildEdgeContextMenu(ContextualMenuPopulateEvent evt, SceneFlowEdge edge)
        {
            SceneEdge edgeData = FindEdgeData(edge);
            if (edgeData == null) return;

            var srcName = _blueprintData.FindNodeById(edgeData.source)?.sceneName ?? "?";
            var tgtName = _blueprintData.FindNodeById(edgeData.target)?.sceneName ?? "?";

            // 標題（資訊，不可點）
            evt.menu.AppendAction($"{srcName} → {tgtName}", null, DropdownMenuAction.Status.Disabled);
            evt.menu.AppendSeparator();

            // 刪除
            evt.menu.AppendAction("刪除連線", _ =>
            {
                edge.output?.Disconnect(edge);
                edge.input?.Disconnect(edge);
                RemoveElement(edge);

                _blueprintData.edges.RemoveAll(e => e.id == edgeData.id);
                _blueprintData.SyncEdgesToConnectedIds();
                NotifyChanged();
            });
        }

        /// <summary>
        /// 從 SceneFlowEdge 反查 SceneEdge 資料
        /// </summary>
        private SceneEdge FindEdgeData(SceneFlowEdge edge)
        {
            SceneEdge edgeData = null;
            if (_blueprintData?.edges != null && !string.IsNullOrEmpty(edge.EdgeDataId))
            {
                edgeData = _blueprintData.edges.FirstOrDefault(e => e.id == edge.EdgeDataId);
            }

            if (edgeData == null)
            {
                var srcNode = edge.output?.node as SceneFlowNode;
                var tgtNode = edge.input?.node as SceneFlowNode;
                if (srcNode != null && tgtNode != null && _blueprintData?.edges != null)
                {
                    edgeData = _blueprintData.edges.FirstOrDefault(e =>
                        e.source == srcNode.SceneData.id && e.target == tgtNode.SceneData.id);
                }
            }
            return edgeData;
        }
        #endregion 節點操作

        #region 連線操作
        /// <summary>
        /// 從資料建立連線
        /// </summary>
        private void CreateEdgeFromData(SceneEdge edgeData)
        {
            if (!_nodeMap.TryGetValue(edgeData.source, out var sourceNode)) return;
            if (!_nodeMap.TryGetValue(edgeData.target, out var targetNode)) return;
            if (sourceNode.OutputPort == null || targetNode.InputPort == null) return;

            var edge = new SceneFlowEdge
            {
                output = sourceNode.OutputPort,
                input = targetNode.InputPort,
                EdgeDataId = edgeData.id
            };

            edge.output.Connect(edge);
            edge.input.Connect(edge);
            edge.UpdateTransitionLabel(edgeData.transition);
            AddElement(edge);

            // 直接在 edge 上註冊點擊，確保選取時觸發屬性面板
            edge.RegisterCallback<MouseDownEvent>(evt =>
            {
                EditorApplication.delayCall += () =>
                {
                    var data = FindEdgeData(edge);
                    if (data != null)
                        OnSelectionChanged?.Invoke(data);
                };
            });
        }

        /// <summary>
        /// 取得相容的端口（決定哪些端口可以連線）
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();

            ports.ForEach(port =>
            {
                // 不能連自己
                if (startPort.node == port.node) return;
                // 必須不同方向（Output→Input）
                if (startPort.direction == port.direction) return;

                compatiblePorts.Add(port);
            });

            return compatiblePorts;
        }
        #endregion 連線操作

        #region 圖表變更回調
        /// <summary>
        /// 處理圖表變更事件（移動、連線、刪除）
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            bool dataChanged = false;

            // 處理節點移動
            bool startSceneMoved = false;
            if (change.movedElements != null)
            {
                foreach (var element in change.movedElements)
                {
                    if (element is SceneFlowNode flowNode)
                    {
                        flowNode.SyncPositionToData();
                        dataChanged = true;

                        // 如果移動的是起始場景，標記需要同步 START
                        if (flowNode.SceneData.isStartNode)
                            startSceneMoved = true;
                    }
                }

                // 起始場景移動時，START 跟隨
                if (startSceneMoved)
                    SyncStartNodePosition();
            }

            // 處理新連線 — 替換預設 Edge 為帶箭頭的 SceneFlowEdge
            if (change.edgesToCreate != null)
            {
                var edgesToReplace = new List<Edge>();

                foreach (var edge in change.edgesToCreate)
                {
                    var sourceNode = edge.output.node as SceneFlowNode;
                    var targetNode = edge.input.node as SceneFlowNode;

                    if (sourceNode != null && targetNode != null)
                    {
                        bool exists = _blueprintData.edges.Any(e =>
                            e.source == sourceNode.SceneData.id &&
                            e.target == targetNode.SceneData.id);

                        if (!exists)
                        {
                            var newEdgeData = new SceneEdge(
                                sourceNode.SceneData.id,
                                targetNode.SceneData.id);
                            _blueprintData.edges.Add(newEdgeData);
                            dataChanged = true;

                            // 把資料 ID 帶給替換後的 SceneFlowEdge
                            if (edge is SceneFlowEdge sfe)
                                sfe.EdgeDataId = newEdgeData.id;
                        }

                        // 標記需要替換的預設 Edge
                        if (edge is not SceneFlowEdge)
                            edgesToReplace.Add(edge);
                    }
                }

                // 延遲替換：移除預設 Edge，建立 SceneFlowEdge
                if (edgesToReplace.Count > 0)
                {
                    EditorApplication.delayCall += () =>
                    {
                        foreach (var oldEdge in edgesToReplace)
                        {
                            var outPort = oldEdge.output;
                            var inPort = oldEdge.input;

                            oldEdge.output?.Disconnect(oldEdge);
                            oldEdge.input?.Disconnect(oldEdge);
                            RemoveElement(oldEdge);

                            if (outPort != null && inPort != null)
                            {
                                // 找到對應的資料 ID
                                var srcNode = outPort.node as SceneFlowNode;
                                var tgtNode = inPort.node as SceneFlowNode;
                                string edgeId = "";
                                if (srcNode != null && tgtNode != null && _blueprintData != null)
                                {
                                    var match = _blueprintData.edges.FirstOrDefault(e =>
                                        e.source == srcNode.SceneData.id &&
                                        e.target == tgtNode.SceneData.id);
                                    if (match != null) edgeId = match.id;
                                }

                                var newEdge = new SceneFlowEdge
                                {
                                    output = outPort,
                                    input = inPort,
                                    EdgeDataId = edgeId
                                };
                                newEdge.output.Connect(newEdge);
                                newEdge.input.Connect(newEdge);
                                AddElement(newEdge);

                                // 點擊監聽
                                var capturedEdge = newEdge;
                                newEdge.RegisterCallback<MouseDownEvent>(clickEvt =>
                                {
                                    EditorApplication.delayCall += () =>
                                    {
                                        var d = FindEdgeData(capturedEdge);
                                        if (d != null)
                                            OnSelectionChanged?.Invoke(d);
                                    };
                                });
                            }
                        }
                    };
                }
            }

            // 處理刪除
            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is SceneFlowNode flowNode)
                    {
                        // 移除相關連線
                        _blueprintData.edges.RemoveAll(e =>
                            e.source == flowNode.SceneData.id ||
                            e.target == flowNode.SceneData.id);

                        _blueprintData.nodes.Remove(flowNode.SceneData);
                        _nodeMap.Remove(flowNode.SceneData.id);
                        dataChanged = true;
                    }
                    else if (element is Edge edge)
                    {
                        var sourceNode = edge.output?.node as SceneFlowNode;
                        var targetNode = edge.input?.node as SceneFlowNode;

                        if (sourceNode != null && targetNode != null)
                        {
                            _blueprintData.edges.RemoveAll(e =>
                                e.source == sourceNode.SceneData.id &&
                                e.target == targetNode.SceneData.id);
                            dataChanged = true;
                        }
                    }
                }
            }

            if (dataChanged)
            {
                _blueprintData.SyncEdgesToConnectedIds();
                NotifyChanged();
            }

            return change;
        }
        #endregion 圖表變更回調

        #region START 節點管理
        /// <summary>
        /// 確保 START 節點存在（固定入口，不可刪除）
        /// </summary>
        private void EnsureStartNode(SceneBlueprintData data)
        {
            var startNode = data.nodes.FirstOrDefault(n => n.nodeType == SceneNodeType.Start);
            if (startNode == null)
            {
                startNode = new SceneNode("START")
                {
                    nodeType = SceneNodeType.Start,
                    position = new Vector2(50, 200)
                };
                data.nodes.Insert(0, startNode);
                EditorUtility.SetDirty(data);
            }
        }

        /// <summary>
        /// 將 START 節點連接到指定場景（清除舊連線，建立新連線）
        /// </summary>
        /// <summary>START 節點與起始場景的水平間距</summary>
        private const float StartNodeOffsetX = 200f;

        public void ConnectStartToScene(SceneNode targetScene)
        {
            if (_blueprintData == null) return;

            var startNode = _blueprintData.nodes.FirstOrDefault(n => n.nodeType == SceneNodeType.Start);
            if (startNode == null) return;

            // 清除 START 的所有舊出邊
            _blueprintData.edges.RemoveAll(e => e.source == startNode.id);

            // 建立新連線
            var newEdge = new SceneEdge(startNode.id, targetScene.id);
            _blueprintData.edges.Add(newEdge);

            // START 自動定位到起始場景左側
            SnapStartNodeToTarget(startNode, targetScene);

            // 重新載入圖表以顯示新連線
            LoadBlueprintData(_blueprintData);
            NotifyChanged();
        }

        /// <summary>
        /// 將 START 節點吸附到目標場景左側
        /// </summary>
        private void SnapStartNodeToTarget(SceneNode startNode, SceneNode targetScene)
        {
            startNode.position = new Vector2(
                targetScene.position.x - StartNodeOffsetX,
                targetScene.position.y);
        }

        /// <summary>
        /// 當起始場景被移動時，START 跟隨移動
        /// </summary>
        public void SyncStartNodePosition()
        {
            if (_blueprintData == null) return;

            var startNode = _blueprintData.nodes.FirstOrDefault(n => n.nodeType == SceneNodeType.Start);
            if (startNode == null) return;

            // 找 START 連接的目標場景
            var startEdge = _blueprintData.edges.FirstOrDefault(e => e.source == startNode.id);
            if (startEdge == null) return;

            var targetScene = _blueprintData.FindNodeById(startEdge.target);
            if (targetScene == null) return;

            SnapStartNodeToTarget(startNode, targetScene);

            // 更新 GraphView 上的位置
            if (_nodeMap.TryGetValue(startNode.id, out var startFlowNode))
            {
                startFlowNode.SetPosition(new Rect(startNode.position, Vector2.zero));
            }
        }
        #endregion START 節點管理

        #region 工具方法
        /// <summary>
        /// 通知資料已變更
        /// </summary>
        private void NotifyChanged()
        {
            if (_blueprintData != null)
            {
                EditorUtility.SetDirty(_blueprintData);
                AssetDatabase.SaveAssets();
            }
            OnGraphChanged?.Invoke();
        }

        /// <summary>
        /// 刷新所有節點外觀（不重載圖表）
        /// </summary>
        public void RefreshAllNodeAppearance()
        {
            foreach (var kvp in _nodeMap)
            {
                kvp.Value.RefreshAppearance();
            }

            // 同步更新所有連線上的轉場標籤
            if (_blueprintData?.edges != null)
            {
                foreach (var element in graphElements)
                {
                    if (element is SceneFlowEdge flowEdge)
                    {
                        var edgeData = _blueprintData.edges.Find(
                            e => e.id == flowEdge.EdgeDataId);
                        if (edgeData != null)
                            flowEdge.UpdateTransitionLabel(edgeData.transition);
                    }
                }
            }
        }

        /// <summary>
        /// 將所有節點置中顯示（快捷鍵 F 也可觸發）
        /// </summary>
        public void FrameAllNodes()
        {
            FrameAll();
        }

        /// <summary>
        /// 將起始場景節點置中，沒有起始節點則置中全部
        /// </summary>
        public void FrameStartNode()
        {
            var startNode = _nodeMap.Values.FirstOrDefault(n => n.SceneData.isStartNode);
            if (startNode != null)
            {
                ClearSelection();
                AddToSelection(startNode);
                FrameSelection();
                ClearSelection();
            }
            else
            {
                FrameAll();
            }
        }

        /// <summary>
        /// 自動排列節點（BFS 分層）
        /// </summary>
        public void AutoLayout()
        {
            if (_blueprintData?.nodes == null || _blueprintData.nodes.Count == 0) return;

            float startX = 50;
            float startY = 50;
            float spacingX = 280;
            float spacingY = 150;

            // START 節點永遠在最左邊，然後是起始場景
            var startEntryNode = _blueprintData.nodes
                .FirstOrDefault(n => n.nodeType == SceneNodeType.Start);
            var startNodes = _blueprintData.nodes
                .Where(n => n.nodeType == SceneNodeType.Start || n.isStartNode)
                .ToList();
            var otherNodes = _blueprintData.nodes
                .Where(n => n.nodeType != SceneNodeType.Start && !n.isStartNode)
                .ToList();

            int col = 0;
            foreach (var node in startNodes)
            {
                node.position = new Vector2(startX, startY + col * spacingY);
                col++;
            }

            // BFS 排列已連接的節點
            var placed = new HashSet<string>(startNodes.Select(n => n.id));
            var queue = new Queue<string>();
            foreach (var n in startNodes) queue.Enqueue(n.id);

            int layer = 1;
            while (queue.Count > 0)
            {
                int count = queue.Count;
                int row = 0;
                for (int i = 0; i < count; i++)
                {
                    var currentId = queue.Dequeue();
                    var outEdges = _blueprintData.GetOutgoingEdges(currentId);

                    foreach (var edge in outEdges)
                    {
                        if (placed.Contains(edge.target)) continue;
                        placed.Add(edge.target);

                        var targetNode = _blueprintData.FindNodeById(edge.target);
                        if (targetNode != null)
                        {
                            targetNode.position = new Vector2(
                                startX + layer * spacingX,
                                startY + row * spacingY);
                            row++;
                            queue.Enqueue(edge.target);
                        }
                    }
                }
                layer++;
            }

            // 未連接的節點放在下方
            int unconnectedRow = 0;
            foreach (var node in otherNodes)
            {
                if (!placed.Contains(node.id))
                {
                    node.position = new Vector2(
                        startX,
                        startY + (col + unconnectedRow + 1) * spacingY);
                    unconnectedRow++;
                }
            }

            LoadBlueprintData(_blueprintData);
            NotifyChanged();
        }
        #endregion 工具方法
    }
    #endregion 場景流程圖
}
#endif
