#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;

namespace CatzTools
{
    #region 場景連線
    /// <summary>
    /// 場景連線 — 單向 A→B，連線上顯示轉場類型標籤
    /// </summary>
    public class SceneFlowEdge : Edge
    {
        /// <summary>對應的資料 ID</summary>
        public string EdgeDataId { get; set; }

        private Label _transitionLabel;
        private bool _labelAdded;

        /// <summary>
        /// 更新連線上顯示的轉場類型文字
        /// </summary>
        public void UpdateTransitionLabel(TransitionSettings t)
        {
            string text = "";
            if (t != null && t.type != TransitionType.None)
            {
                text = t.type switch
                {
                    TransitionType.Fade => "淡入淡出",
                    TransitionType.SlideLeft => "← 左滑",
                    TransitionType.SlideRight => "右滑 →",
                    TransitionType.SlideUp => "↑ 上滑",
                    TransitionType.SlideDown => "↓ 下滑",
                    TransitionType.CustomShader when t.customMaterial != null =>
                        t.customMaterial.name.Replace("SF_", ""),
                    TransitionType.CustomShader => "Shader?",
                    _ => ""
                };
            }

            EnsureLabel();

            if (string.IsNullOrEmpty(text))
            {
                _transitionLabel.style.display = DisplayStyle.None;
                return;
            }

            _transitionLabel.text = text;
            _transitionLabel.style.display = DisplayStyle.Flex;
        }

        private void EnsureLabel()
        {
            if (_transitionLabel != null) return;

            _transitionLabel = new Label();
            _transitionLabel.style.fontSize = 9;
            _transitionLabel.style.color = new Color(0.85f, 0.85f, 0.85f, 0.95f);
            _transitionLabel.style.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.9f);
            _transitionLabel.style.borderTopLeftRadius = 3;
            _transitionLabel.style.borderTopRightRadius = 3;
            _transitionLabel.style.borderBottomLeftRadius = 3;
            _transitionLabel.style.borderBottomRightRadius = 3;
            _transitionLabel.style.paddingLeft = 5;
            _transitionLabel.style.paddingRight = 5;
            _transitionLabel.style.paddingTop = 2;
            _transitionLabel.style.paddingBottom = 2;
            _transitionLabel.style.position = Position.Absolute;
            _transitionLabel.pickingMode = PickingMode.Ignore;
            _transitionLabel.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Edge layout 完成後把標籤掛上去並定位
        /// </summary>
        protected override void OnCustomStyleResolved(ICustomStyle style)
        {
            base.OnCustomStyleResolved(style);
            PositionLabel();
        }

        /// <summary>
        /// 每次 Edge 幾何變更時重新定位標籤
        /// </summary>
        public SceneFlowEdge()
        {
            RegisterCallback<GeometryChangedEvent>(_ => PositionLabel());
        }

        private void PositionLabel()
        {
            if (_transitionLabel == null) return;
            if (_transitionLabel.style.display == DisplayStyle.None) return;
            if (output == null || input == null) return;

            // 掛到 GraphView 的 contentViewContainer 上（跟隨縮放平移）
            if (!_labelAdded)
            {
                var graphView = GetFirstAncestorOfType<GraphView>();
                if (graphView == null) return;
                graphView.contentViewContainer.Add(_transitionLabel);
                _labelAdded = true;
            }

            // 直接用 port 的世界座標換算到 contentViewContainer 本地座標
            var graphView2 = GetFirstAncestorOfType<GraphView>();
            if (graphView2 == null) return;

            var container = graphView2.contentViewContainer;
            var startWorld = output.GetGlobalCenter();
            var endWorld = input.GetGlobalCenter();
            var startLocal = container.WorldToLocal(startWorld);
            var endLocal = container.WorldToLocal(endWorld);

            // 30% 位置
            var pos = Vector2.Lerp(startLocal, endLocal, 0.3f);

            float labelW = _transitionLabel.resolvedStyle.width;
            float labelH = _transitionLabel.resolvedStyle.height;
            if (labelW <= 0) labelW = 60;
            if (labelH <= 0) labelH = 16;

            // 往線的上方偏移，避免壓在線上
            _transitionLabel.style.left = pos.x - labelW * 0.5f;
            _transitionLabel.style.top = pos.y - labelH - 4;
        }
    }
    #endregion 場景連線
}
#endif
