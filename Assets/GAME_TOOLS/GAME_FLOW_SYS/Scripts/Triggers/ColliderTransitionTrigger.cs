using UnityEngine;

namespace CatzTools
{
    #region 碰撞轉場觸發器
    /// <summary>
    /// 碰撞轉場觸發器 — 物件進入 Trigger 區域即觸發轉場。
    /// 支援 3D (OnTriggerEnter) 和 2D (OnTriggerEnter2D)。
    /// </summary>
    public class ColliderTransitionTrigger : SceneTransitionTrigger
    {
        #region 序列化欄位
        [Header("碰撞設定")]
        /// <summary>觸發對象的 Tag（空字串 = 不過濾）</summary>
        [SerializeField] private string _triggerTag = "Player";
        #endregion 序列化欄位

        #region 3D 碰撞
        private void OnTriggerEnter(Collider other)
        {
            if (IsValidTarget(other.gameObject))
                ExecuteTransition();
        }
        #endregion 3D 碰撞

        #region 2D 碰撞
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsValidTarget(other.gameObject))
                ExecuteTransition();
        }
        #endregion 2D 碰撞

        #region 驗證
        private bool IsValidTarget(GameObject target)
        {
            if (string.IsNullOrEmpty(_triggerTag)) return true;
            return target.CompareTag(_triggerTag);
        }
        #endregion 驗證
    }
    #endregion 碰撞轉場觸發器
}
