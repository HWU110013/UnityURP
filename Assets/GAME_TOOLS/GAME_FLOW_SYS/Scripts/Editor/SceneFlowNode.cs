#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.Experimental.GraphView;

namespace CatzTools
{
    #region 場景流程節點
    /// <summary>
    /// 場景流程節點 — GraphView 視覺化節點（不含 FlowManager，FlowManager 由 EditorWindow 獨立管理）
    /// </summary>
    public class SceneFlowNode : Node
    {
        #region 公開屬性
        /// <summary>對應的 SceneNode 資料</summary>
        public SceneNode SceneData { get; private set; }

        /// <summary>輸入端口</summary>
        public Port InputPort { get; private set; }

        /// <summary>輸出端口</summary>
        public Port OutputPort { get; private set; }

        /// <summary>節點資料變更事件</summary>
        public event Action<SceneFlowNode> OnDataChanged;
        #endregion 公開屬性

        #region 私有變數
        private Label _descriptionLabel;
        private Label _statusLabel;
        #endregion 私有變數

        #region 建構
        /// <summary>
        /// 建構場景流程節點
        /// </summary>
        public SceneFlowNode(SceneNode sceneData)
        {
            SceneData = sceneData;

            title = GetDisplayTitle();
            tooltip = sceneData.description ?? "";

            SetPosition(new Rect(sceneData.position, Vector2.zero));

            SetupPorts();
            SetupContent();
            ApplyNodeStyle();

            RefreshExpandedState();
            RefreshPorts();
        }
        #endregion 建構

        #region 端口設定
        /// <summary>
        /// 設定端口 — 依節點類型決定有哪些端口
        /// </summary>
        private void SetupPorts()
        {
            // START 節點：只有出口
            // End 節點：只有入口
            // Scene 節點：入出都有

            if (SceneData.nodeType != SceneNodeType.Start)
            {
                InputPort = InstantiatePort(
                    Orientation.Horizontal,
                    Direction.Input,
                    Port.Capacity.Multi,
                    typeof(bool));
                InputPort.portName = "入";
                InputPort.portColor = new Color(0.4f, 0.8f, 0.4f);
                inputContainer.Add(InputPort);
            }

            if (SceneData.nodeType != SceneNodeType.End)
            {
                OutputPort = InstantiatePort(
                    Orientation.Horizontal,
                    Direction.Output,
                    Port.Capacity.Multi,
                    typeof(bool));
                OutputPort.portName = "出";
                OutputPort.portColor = new Color(0.8f, 0.5f, 0.3f);
                outputContainer.Add(OutputPort);
            }

            // START 節點不可刪除、不可單獨移動（跟隨起始場景）
            if (SceneData.nodeType == SceneNodeType.Start)
            {
                capabilities &= ~Capabilities.Deletable;
                capabilities &= ~Capabilities.Movable;
            }
        }
        #endregion 端口設定

        #region 內容設定
        /// <summary>
        /// 設定節點內容區域
        /// </summary>
        private void SetupContent()
        {
            var container = new VisualElement();
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;

            // 狀態標籤
            _statusLabel = new Label();
            _statusLabel.style.fontSize = 10;
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            container.Add(_statusLabel);
            UpdateStatusLabel();

            // 描述標籤
            if (!string.IsNullOrEmpty(SceneData.description))
            {
                _descriptionLabel = new Label(SceneData.description);
                _descriptionLabel.style.fontSize = 10;
                _descriptionLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                _descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
                _descriptionLabel.style.maxWidth = 180;
                container.Add(_descriptionLabel);
            }

            // 標籤
            if (SceneData.tags != null && SceneData.tags.Count > 0)
            {
                var tagContainer = new VisualElement();
                tagContainer.style.flexDirection = FlexDirection.Row;
                tagContainer.style.flexWrap = Wrap.Wrap;
                tagContainer.style.marginTop = 2;

                foreach (var tag in SceneData.tags)
                {
                    if (string.IsNullOrEmpty(tag)) continue;
                    var tagLabel = new Label(tag);
                    tagLabel.style.fontSize = 9;
                    tagLabel.style.backgroundColor = new Color(0.3f, 0.3f, 0.5f, 0.6f);
                    tagLabel.style.color = new Color(0.8f, 0.8f, 1f);
                    tagLabel.style.borderTopLeftRadius = 3;
                    tagLabel.style.borderTopRightRadius = 3;
                    tagLabel.style.borderBottomLeftRadius = 3;
                    tagLabel.style.borderBottomRightRadius = 3;
                    tagLabel.style.paddingLeft = 4;
                    tagLabel.style.paddingRight = 4;
                    tagLabel.style.paddingTop = 1;
                    tagLabel.style.paddingBottom = 1;
                    tagLabel.style.marginRight = 2;
                    tagContainer.Add(tagLabel);
                }

                container.Add(tagContainer);
            }

            extensionContainer.Add(container);
        }
        #endregion 內容設定

        #region 樣式
        /// <summary>
        /// 套用節點樣式（依類型和連結狀態著色）
        /// </summary>
        private void ApplyNodeStyle()
        {
            bool hasAsset = SceneData.sceneAsset != null;
            Color headerColor;

            switch (SceneData.nodeType)
            {
                case SceneNodeType.Start:
                    headerColor = new Color(0.2f, 0.6f, 0.3f); // 綠色（START）
                    break;
                case SceneNodeType.End:
                    headerColor = new Color(0.6f, 0.2f, 0.2f); // 紅色
                    break;
                default: // Scene
                    if (SceneData.isStartNode)
                        headerColor = new Color(0.2f, 0.5f, 0.5f); // 青色（起始場景）
                    else
                        headerColor = hasAsset
                            ? new Color(0.2f, 0.4f, 0.7f) // 藍色（已連結）
                            : new Color(0.6f, 0.3f, 0.2f); // 橘色（未連結）
                    break;
            }

            titleContainer.style.backgroundColor = headerColor;

            // 起始場景加粗綠色邊框
            if (SceneData.isStartNode)
            {
                style.borderTopWidth = 2;
                style.borderBottomWidth = 2;
                style.borderLeftWidth = 2;
                style.borderRightWidth = 2;
                style.borderTopColor = new Color(0.3f, 0.8f, 0.3f);
                style.borderBottomColor = new Color(0.3f, 0.8f, 0.3f);
                style.borderLeftColor = new Color(0.3f, 0.8f, 0.3f);
                style.borderRightColor = new Color(0.3f, 0.8f, 0.3f);
            }
        }

        /// <summary>
        /// 取得顯示標題
        /// </summary>
        private string GetDisplayTitle()
        {
            string prefix = SceneData.nodeType switch
            {
                SceneNodeType.Start => "▶ ",
                SceneNodeType.End => "■ ",
                _ => SceneData.isStartNode ? "★ " : ""
            };

            return prefix + SceneData.sceneName;
        }
        #endregion 樣式

        #region 狀態更新
        /// <summary>
        /// 更新狀態標籤
        /// </summary>
        public void UpdateStatusLabel()
        {
            if (_statusLabel == null) return;

            if (SceneData.nodeType == SceneNodeType.Start)
            {
                _statusLabel.text = "遊戲啟動入口";
                _statusLabel.style.color = new Color(0.5f, 0.8f, 0.5f);
                return;
            }

            if (SceneData.nodeType == SceneNodeType.End)
            {
                _statusLabel.text = "流程終點";
                _statusLabel.style.color = new Color(0.7f, 0.5f, 0.5f);
                return;
            }

            bool hasAsset = SceneData.sceneAsset != null;
            _statusLabel.text = hasAsset ? "✓ 已連結" : "✗ 未連結場景資源";
            _statusLabel.style.color = hasAsset
                ? new Color(0.4f, 0.8f, 0.4f)
                : new Color(0.8f, 0.3f, 0.3f);
        }

        /// <summary>
        /// 同步位置到資料
        /// </summary>
        public void SyncPositionToData()
        {
            var rect = GetPosition();
            SceneData.position = new Vector2(rect.x, rect.y);
        }

        /// <summary>
        /// 刷新節點外觀
        /// </summary>
        public void RefreshAppearance()
        {
            title = GetDisplayTitle();
            ApplyNodeStyle();
            UpdateStatusLabel();
        }
        #endregion 狀態更新
    }
    #endregion 場景流程節點
}
#endif
