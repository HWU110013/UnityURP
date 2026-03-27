#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;

namespace CatzTools
{
    #region 場景流程 MiniMap
    /// <summary>
    /// 自訂場景流程 MiniMap — 獨立於 GraphView，可放置在任意 VisualElement 容器中
    /// </summary>
    public class SceneFlowMiniMap : VisualElement
    {
        #region 常數
        private const float NodeWidth = 8f;
        private const float NodeHeight = 5f;
        private const float Padding = 8f;
        #endregion 常數

        #region 私有變數
        private SceneFlowGraphView _graphView;
        private bool _isDragging;
        #endregion 私有變數

        #region 建構
        /// <summary>
        /// 建構 MiniMap
        /// </summary>
        public SceneFlowMiniMap(SceneFlowGraphView graphView)
        {
            _graphView = graphView;

            style.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            style.borderTopWidth = 1;
            style.borderTopColor = new Color(0.3f, 0.3f, 0.4f);

            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);

            // 定時刷新（跟隨視口變化）
            schedule.Execute(MarkDirtyRepaint).Every(100);
        }
        #endregion 建構

        #region 繪製
        /// <summary>
        /// 繪製 MiniMap 內容
        /// </summary>
        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (_graphView?.BlueprintData?.nodes == null) return;

            var nodes = _graphView.BlueprintData.nodes;
            if (nodes.Count == 0) return;

            var painter = ctx.painter2D;
            var mapRect = contentRect;
            if (mapRect.width <= Padding * 2 || mapRect.height <= Padding * 2) return;

            var (bounds, scale, offsetX, offsetY) = CalculateDrawParams();

            // 繪製連線
            DrawEdges(painter, nodes, bounds, scale, offsetX, offsetY);

            // 繪製節點
            DrawNodes(painter, nodes, bounds, scale, offsetX, offsetY);

            // 繪製視口指示框
            DrawViewport(painter, bounds, scale, offsetX, offsetY);
        }

        /// <summary>
        /// 繪製節點矩形
        /// </summary>
        private void DrawNodes(Painter2D painter, List<SceneNode> nodes,
            Rect bounds, float scale, float offsetX, float offsetY)
        {
            foreach (var node in nodes)
            {
                float x = (node.position.x - bounds.x) * scale + offsetX;
                float y = (node.position.y - bounds.y) * scale + offsetY;

                Color color = GetNodeColor(node);
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x - NodeWidth * 0.5f, y - NodeHeight * 0.5f));
                painter.LineTo(new Vector2(x + NodeWidth * 0.5f, y - NodeHeight * 0.5f));
                painter.LineTo(new Vector2(x + NodeWidth * 0.5f, y + NodeHeight * 0.5f));
                painter.LineTo(new Vector2(x - NodeWidth * 0.5f, y + NodeHeight * 0.5f));
                painter.ClosePath();
                painter.Fill();
            }
        }

        /// <summary>
        /// 繪製連線
        /// </summary>
        private void DrawEdges(Painter2D painter, List<SceneNode> nodes,
            Rect bounds, float scale, float offsetX, float offsetY)
        {
            var edges = _graphView.BlueprintData.edges;
            if (edges == null) return;

            // 建立快速查表
            var posMap = new Dictionary<string, Vector2>();
            foreach (var node in nodes)
            {
                float x = (node.position.x - bounds.x) * scale + offsetX;
                float y = (node.position.y - bounds.y) * scale + offsetY;
                posMap[node.id] = new Vector2(x, y);
            }

            painter.strokeColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            painter.lineWidth = 1f;

            foreach (var edge in edges)
            {
                if (!posMap.TryGetValue(edge.source, out var from)) continue;
                if (!posMap.TryGetValue(edge.target, out var to)) continue;

                painter.BeginPath();
                painter.MoveTo(from);
                painter.LineTo(to);
                painter.Stroke();
            }
        }

        /// <summary>
        /// 繪製視口指示框
        /// </summary>
        private void DrawViewport(Painter2D painter,
            Rect bounds, float scale, float offsetX, float offsetY)
        {
            var viewRect = _graphView.GetViewportInGraphSpace();
            if (viewRect.width <= 0 || viewRect.height <= 0) return;

            float x = (viewRect.x - bounds.x) * scale + offsetX;
            float y = (viewRect.y - bounds.y) * scale + offsetY;
            float w = viewRect.width * scale;
            float h = viewRect.height * scale;

            // 半透明填充
            painter.fillColor = new Color(1f, 1f, 1f, 0.06f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y));
            painter.LineTo(new Vector2(x + w, y));
            painter.LineTo(new Vector2(x + w, y + h));
            painter.LineTo(new Vector2(x, y + h));
            painter.ClosePath();
            painter.Fill();

            // 邊框
            painter.strokeColor = new Color(1f, 1f, 1f, 0.35f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y));
            painter.LineTo(new Vector2(x + w, y));
            painter.LineTo(new Vector2(x + w, y + h));
            painter.LineTo(new Vector2(x, y + h));
            painter.ClosePath();
            painter.Stroke();
        }
        #endregion 繪製

        #region 輔助方法
        /// <summary>
        /// 計算完整包圍盒（節點 + 視口聯集），確保視口框永遠在範圍內
        /// </summary>
        private Rect CalculateFullBounds()
        {
            var nodes = _graphView.BlueprintData.nodes;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var node in nodes)
            {
                if (node.position.x < minX) minX = node.position.x;
                if (node.position.y < minY) minY = node.position.y;
                if (node.position.x > maxX) maxX = node.position.x;
                if (node.position.y > maxY) maxY = node.position.y;
            }

            // 與視口取聯集
            var viewRect = _graphView.GetViewportInGraphSpace();
            if (viewRect.width > 0 && viewRect.height > 0)
            {
                minX = Mathf.Min(minX, viewRect.xMin);
                minY = Mathf.Min(minY, viewRect.yMin);
                maxX = Mathf.Max(maxX, viewRect.xMax);
                maxY = Mathf.Max(maxY, viewRect.yMax);
            }

            var bounds = new Rect(minX, minY, maxX - minX, maxY - minY);
            if (bounds.width <= 0) bounds.width = 1;
            if (bounds.height <= 0) bounds.height = 1;

            // 加邊距
            float margin = Mathf.Max(bounds.width, bounds.height) * 0.05f;
            bounds.xMin -= margin;
            bounds.yMin -= margin;
            bounds.width += margin * 2;
            bounds.height += margin * 2;

            return bounds;
        }

        /// <summary>
        /// 計算繪製參數（共用，避免繪製和點擊計算不同步）
        /// </summary>
        private (Rect bounds, float scale, float offsetX, float offsetY) CalculateDrawParams()
        {
            var mapRect = contentRect;
            var drawArea = new Rect(
                Padding, Padding,
                mapRect.width - Padding * 2,
                mapRect.height - Padding * 2);

            var bounds = CalculateFullBounds();

            float scaleX = drawArea.width / bounds.width;
            float scaleY = drawArea.height / bounds.height;
            float scale = Mathf.Min(scaleX, scaleY);

            float offsetX = drawArea.x + (drawArea.width - bounds.width * scale) * 0.5f;
            float offsetY = drawArea.y + (drawArea.height - bounds.height * scale) * 0.5f;

            return (bounds, scale, offsetX, offsetY);
        }

        /// <summary>
        /// 取得節點顏色（與 SceneFlowNode 一致）
        /// </summary>
        private Color GetNodeColor(SceneNode node)
        {
            return node.nodeType switch
            {
                SceneNodeType.Start => new Color(0.2f, 0.6f, 0.3f),
                SceneNodeType.End => new Color(0.6f, 0.2f, 0.2f),
                _ => node.isStartNode
                    ? new Color(0.2f, 0.5f, 0.5f)
                    : node.sceneAsset != null
                        ? new Color(0.2f, 0.4f, 0.7f)
                        : new Color(0.6f, 0.3f, 0.2f)
            };
        }

        /// <summary>
        /// 將 MiniMap 上的滑鼠座標轉換為 Graph 空間座標
        /// </summary>
        private Vector2 MiniMapToGraphPosition(Vector2 localPos)
        {
            var nodes = _graphView.BlueprintData?.nodes;
            if (nodes == null || nodes.Count == 0) return Vector2.zero;

            var (bounds, scale, offsetX, offsetY) = CalculateDrawParams();

            float graphX = (localPos.x - offsetX) / scale + bounds.x;
            float graphY = (localPos.y - offsetY) / scale + bounds.y;

            return new Vector2(graphX, graphY);
        }
        #endregion 輔助方法

        #region 滑鼠互動
        /// <summary>
        /// 滑鼠按下 — 開始拖曳或點擊定位
        /// </summary>
        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0) return;

            _isDragging = true;
            PanToMouse(evt.localMousePosition);
            this.CaptureMouse();
            evt.StopPropagation();
        }

        /// <summary>
        /// 滑鼠移動 — 拖曳中持續平移
        /// </summary>
        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_isDragging) return;

            PanToMouse(evt.localMousePosition);
            evt.StopPropagation();
        }

        /// <summary>
        /// 滑鼠放開
        /// </summary>
        private void OnMouseUp(MouseUpEvent evt)
        {
            if (evt.button != 0 || !_isDragging) return;

            _isDragging = false;
            this.ReleaseMouse();
            evt.StopPropagation();
        }

        /// <summary>
        /// 平移 GraphView 視口到滑鼠對應的 Graph 座標
        /// </summary>
        private void PanToMouse(Vector2 localPos)
        {
            var graphPos = MiniMapToGraphPosition(localPos);
            _graphView.PanToPosition(graphPos);
        }
        #endregion 滑鼠互動
    }
    #endregion 場景流程 MiniMap
}
#endif
