#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using CatzTools.GameFlow;

namespace CatzTools.GameFlow.Editor
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

        /// <summary>
        /// 從父容器移除轉場標籤（刪除 Edge 前必須呼叫）
        /// </summary>
        public void RemoveTransitionLabel()
        {
            if (_transitionLabel?.parent != null)
                _transitionLabel.parent.Remove(_transitionLabel);
            _transitionLabel = null;
        }

        /// <summary>
        /// 更新連線上顯示的轉場類型文字。
        /// Hybrid 模型（v0.7.9b）：edge 使用場景預設時**不顯示文字**（避免誤解為 edge 自己設的），
        /// 只有 <paramref name="useOverride"/> = true 時才顯示（此時 edge 自訂轉場，需標記清楚）。
        /// </summary>
        public void UpdateTransitionLabel(TransitionSettings t, bool useOverride)
        {
            EnsureLabel();

            if (!useOverride)
            {
                // 走場景預設 → 線上不掛標籤（由兩端節點的設定決定，看節點 Inspector 即可）
                _transitionLabel.style.display = DisplayStyle.None;
                return;
            }

            string text = "";
            if (t != null && t.type != TransitionType.None)
            {
                text = t.type switch
                {
                    TransitionType.Fade => SceneFlowLocale.EdgeFade,
                    TransitionType.SlideLeft => SceneFlowLocale.EdgeSlideL,
                    TransitionType.SlideRight => SceneFlowLocale.EdgeSlideR,
                    TransitionType.SlideUp => SceneFlowLocale.EdgeSlideU,
                    TransitionType.SlideDown => SceneFlowLocale.EdgeSlideD,
                    TransitionType.CustomShader when t.customMaterial != null =>
                        SceneFlowShaderPresets.GetDisplayName(t.customMaterial),
                    TransitionType.CustomShader => SceneFlowLocale.EdgeNoMat,
                    _ => ""
                };
            }

            if (string.IsNullOrEmpty(text))
            {
                _transitionLabel.style.display = DisplayStyle.None;
                return;
            }

            // ✎ 前綴標示是「覆寫」的轉場
            _transitionLabel.text = "\u270E " + text;
            _transitionLabel.style.display = DisplayStyle.Flex;
        }

        #region 標籤內部實作
        public SceneFlowEdge()
        {
            // Edge 幾何變更時重新定位標籤
            RegisterCallback<GeometryChangedEvent>(_ => PositionLabel());
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

            // 標籤是 Edge 的子元素，隨 Edge 生命週期自動管理
            Add(_transitionLabel);
        }

        /// <summary>
        /// Edge layout 完成後定位標籤
        /// </summary>
        protected override void OnCustomStyleResolved(ICustomStyle style)
        {
            base.OnCustomStyleResolved(style);
            PositionLabel();
        }

        private void PositionLabel()
        {
            if (_transitionLabel == null) return;
            if (_transitionLabel.style.display == DisplayStyle.None) return;
            if (output == null || input == null) return;

            // port 世界座標 → Edge 本地座標
            var startLocal = this.WorldToLocal(output.GetGlobalCenter());
            var endLocal = this.WorldToLocal(input.GetGlobalCenter());

            // 30% 位置（靠近起點端）
            var pos = Vector2.Lerp(startLocal, endLocal, 0.3f);

            float labelW = _transitionLabel.resolvedStyle.width;
            float labelH = _transitionLabel.resolvedStyle.height;
            if (labelW <= 0) labelW = 60;
            if (labelH <= 0) labelH = 16;

            // 往線的上方偏移，避免壓在線上
            _transitionLabel.style.left = pos.x - labelW * 0.5f;
            _transitionLabel.style.top = pos.y - labelH - 4;
        }
        #endregion 標籤內部實作
    }
    #endregion 場景連線
}
#endif
