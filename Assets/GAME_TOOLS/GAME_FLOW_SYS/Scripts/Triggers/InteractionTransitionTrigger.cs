using UnityEngine;

namespace CatzTools
{
    #region 互動鍵轉場觸發器
    /// <summary>
    /// 互動鍵轉場觸發器 — 進入 Trigger 範圍後，按指定鍵才觸發轉場。
    /// 支援 3D 和 2D Collider。
    /// </summary>
    public class InteractionTransitionTrigger : SceneTransitionTrigger
    {
        #region 序列化欄位
        [Header("互動設定")]
        /// <summary>互動按鍵</summary>
        [SerializeField] private KeyCode _interactionKey = KeyCode.E;

        /// <summary>觸發對象的 Tag（空字串 = 不過濾）</summary>
        [SerializeField] private string _triggerTag = "Player";

        [Header("提示 UI")]
        /// <summary>進入範圍時顯示的提示物件（可選）</summary>
        [SerializeField] private GameObject _promptUI;
        #endregion 序列化欄位

        #region 私有變數
        private bool _isInRange;
        #endregion 私有變數

        #region Unity 生命週期
        private void Update()
        {
            if (_isInRange && Input.GetKeyDown(_interactionKey))
                ExecuteTransition();
        }
        #endregion Unity 生命週期

        #region 3D 碰撞
        private void OnTriggerEnter(Collider other)
        {
            if (IsValidTarget(other.gameObject))
                SetInRange(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsValidTarget(other.gameObject))
                SetInRange(false);
        }
        #endregion 3D 碰撞

        #region 2D 碰撞
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsValidTarget(other.gameObject))
                SetInRange(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsValidTarget(other.gameObject))
                SetInRange(false);
        }
        #endregion 2D 碰撞

        #region 內部方法
        private void SetInRange(bool inRange)
        {
            _isInRange = inRange;

            if (_promptUI != null)
                _promptUI.SetActive(inRange);

            Log(inRange ? "進入互動範圍" : "離開互動範圍");
        }

        private bool IsValidTarget(GameObject target)
        {
            if (string.IsNullOrEmpty(_triggerTag)) return true;
            return target.CompareTag(_triggerTag);
        }
        #endregion 內部方法
    }
    #endregion 互動鍵轉場觸發器
}
