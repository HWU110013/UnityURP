using UnityEngine;

namespace CatzTools.GameFlow
{
    #region 自動轉場觸發器
    /// <summary>
    /// 自動轉場觸發器 — 物件啟用後經延遲自動觸發。
    /// 適用於：開場動畫結束自動跳轉、Loading 場景自動進入主選單等。
    /// 關閉 <c>_autoFire</c> 後可作為純外部觸發器，透過 <see cref="SceneTransitionTrigger.Transition"/>
    /// 或 <see cref="FireWithDelay"/> 從 UnityEvent / 程式碼觸發。
    /// </summary>
    public class AutoTransitionTrigger : SceneTransitionTrigger
    {
        #region 序列化欄位
        [Header("自動觸發設定")]
        /// <summary>是否啟用後自動觸發（預設 true，關閉則只接受外部呼叫）</summary>
        [SerializeField] private bool _autoFire = true;

        /// <summary>延遲秒數（0 = 場景就緒後立即觸發）。手動呼叫 <see cref="Transition"/> 時不套用延遲；需延遲請用 <see cref="FireWithDelay"/>。</summary>
        [SerializeField] private float _delay = 0f;
        #endregion 序列化欄位

        #region 公開屬性
        /// <summary>是否啟用後自動觸發</summary>
        public bool AutoFire
        {
            get => _autoFire;
            set => _autoFire = value;
        }

        /// <summary>延遲秒數</summary>
        public float Delay
        {
            get => _delay;
            set => _delay = Mathf.Max(0f, value);
        }
        #endregion 公開屬性

        #region Unity 生命週期
        private async void Start()
        {
            if (!_autoFire)
            {
                Log("自動觸發已關閉，等待外部呼叫");
                return;
            }

            await FireInternal(_delay);
        }
        #endregion Unity 生命週期

        #region 外部觸發入口
        /// <summary>
        /// 外部觸發（套用 Inspector 設定的 <c>_delay</c>）。
        /// 供 UnityEvent 拉線使用 — 例如 InputActionTrigger.onTriggered → FireWithDelay。
        /// 若不需要延遲可直接呼叫繼承自 <see cref="SceneTransitionTrigger"/> 的 <see cref="SceneTransitionTrigger.Transition"/>。
        /// </summary>
        public async void FireWithDelay()
        {
            await FireInternal(_delay);
        }

        /// <summary>
        /// 覆寫延遲的外部觸發 — 無視 Inspector 設定。
        /// </summary>
        public async void FireWithDelay(float overrideDelay)
        {
            await FireInternal(Mathf.Max(0f, overrideDelay));
        }

        private async System.Threading.Tasks.Task FireInternal(float delaySec)
        {
            if (delaySec > 0f)
            {
                Log($"等待 {delaySec:F1} 秒後轉場...");
                await System.Threading.Tasks.Task.Delay(Mathf.CeilToInt(delaySec * 1000));
            }

            ExecuteTransition();
        }
        #endregion 外部觸發入口
    }
    #endregion 自動轉場觸發器
}
