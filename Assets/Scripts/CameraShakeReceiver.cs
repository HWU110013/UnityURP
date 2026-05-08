using UnityEngine;
using Unity.Cinemachine;

namespace CodingCatz.CameraShake
{
    /// <summary>
    /// 鏡頭震動接收器
    /// 掛在 Cinemachine 鏡頭物件上即可自動配置 CinemachineImpulseListener，
    /// 會接收所有 Channel 對應的 CameraShakeTrigger 廣播。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineImpulseListener))]
    [AddComponentMenu("CodingCatz/Camera Shake/Camera Shake Receiver")]
    public class CameraShakeReceiver : MonoBehaviour
    {
        #region 接收參數
        [SerializeField, Tooltip("頻道遮罩 (Bit Mask)，需包含 Trigger 端的 Channel")]
        private int channelMask = 1;

        [SerializeField, Range(0f, 5f), Tooltip("整體震動強度倍率，越高鏡頭震得越誇張")]
        private float gain = 1f;

        [SerializeField, Tooltip("以 2D 距離計算衰減 (適用 2D 遊戲，忽略 Z 軸)")]
        private bool use2DDistance = false;

        [SerializeField, Tooltip("以鏡頭本地座標處理震動訊號，常用於第一人稱讓震動跟著視角走")]
        private bool useCameraSpace = false;
        #endregion 接收參數

        #region 元件引用 (Lazy Loading)
        private CinemachineImpulseListener _listener;

        /// <summary>取得 ImpulseListener 元件，懶載入快取</summary>
        private CinemachineImpulseListener Listener =>
            _listener ??= GetComponent<CinemachineImpulseListener>();
        #endregion 元件引用

        #region 生命週期
        private void Reset() => ApplySettings();
        private void OnValidate() => ApplySettings();
        #endregion 生命週期

        #region 設定套用
        /// <summary>
        /// 將 Inspector 上的參數寫回 CinemachineImpulseListener
        /// </summary>
        [ContextMenu("Apply Settings")]
        private void ApplySettings()
        {
            var l = Listener;
            if (l == null) return;

            l.ChannelMask     = channelMask;
            l.Gain            = gain;
            l.Use2DDistance   = use2DDistance;
            l.UseCameraSpace  = useCameraSpace;
        }
        #endregion 設定套用
    }
}
