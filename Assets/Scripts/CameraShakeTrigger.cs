using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Cinemachine;

namespace CodingCatz.CameraShake
{
    #region 列舉
    /// <summary>
    /// 震動情境
    /// 決定使用哪種波形，力度時長另外在 Inspector 調
    /// </summary>
    public enum ShakePreset
    {
        /// <summary>啟動機關：輕度短促 (Bump 波形)</summary>
        MachineActivate,
        /// <summary>撞擊：中度反衝 (Recoil 波形)</summary>
        Impact,
        /// <summary>爆炸：強烈擴散 (Explosion 波形)</summary>
        Explosion,
        /// <summary>地震：持續搖晃 (Rumble 波形)</summary>
        Earthquake,
    }

    /// <summary>震動方向模式</summary>
    public enum ShakeMode
    {
        /// <summary>XY 平面隨機方向 (推薦)</summary>
        Random,
        /// <summary>水平左右隨機</summary>
        Horizontal,
        /// <summary>垂直上下隨機</summary>
        Vertical,
        /// <summary>前後 Z 軸隨機</summary>
        Forward,
        /// <summary>使用自訂方向向量</summary>
        Custom,
    }

    /// <summary>連續震動模式</summary>
    public enum RepeatMode
    {
        /// <summary>次數模式：指定觸發 N 次 + 每次間隔 (N=1 即單次)</summary>
        CountBased,
        /// <summary>時間模式：在指定秒數內以指定頻率持續震動</summary>
        DurationBased,
    }
    #endregion 列舉

    /// <summary>
    /// 鏡頭震動觸發器
    /// 情境決定波形，力度時長獨立可調，連續震動支援次數與時間兩種模式。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineImpulseSource))]
    [AddComponentMenu("CodingCatz/Camera Shake/Camera Shake Trigger")]
    public class CameraShakeTrigger : MonoBehaviour
    {
        #region 情境
        [Header("情境 (決定波形)")]
        [SerializeField, Tooltip("選定情境後，Execute 時自動套用對應波形")]
        private ShakePreset preset = ShakePreset.Impact;
        #endregion 情境

        #region 力度與時長
        [Header("力度與時長")]
        [SerializeField, Range(0f, 10f), Tooltip("單次震動力度")]
        private float force = 1.2f;

        [SerializeField, Range(0.05f, 5f), Tooltip("單次震動波形持續時間 (秒)")]
        private float duration = 0.5f;
        #endregion 力度與時長

        #region 方向設定
        [Header("方向")]
        [SerializeField] private ShakeMode mode = ShakeMode.Random;

        [SerializeField, Tooltip("自訂方向向量 (僅 Custom 模式有效)")]
        private Vector3 customDirection = new Vector3(1f, 1f, 0f);
        #endregion 方向設定

        #region 連續震動
        [Header("連續震動")]
        [SerializeField, Tooltip("選擇用「次數+間隔」或「總時長+頻率」來控制連續震動")]
        private RepeatMode repeatMode = RepeatMode.CountBased;

        [Header("─ 次數模式參數")]
        [SerializeField, Range(1, 30), Tooltip("觸發次數 (1 = 單次)")]
        private int repeatCount = 1;

        [SerializeField, Range(0.01f, 1f), Tooltip("每次觸發間隔 (秒)")]
        private float repeatInterval = 0.08f;

        [Header("─ 時間模式參數")]
        [SerializeField, Range(0.1f, 10f), Tooltip("連續震動總時間 (秒)")]
        private float totalDuration = 1f;

        [SerializeField, Range(1f, 60f), Tooltip("震動頻率 (每秒次數，Hz)")]
        private float frequency = 8f;
        #endregion 連續震動

        #region 頻道
        [Header("頻道")]
        [SerializeField, Tooltip("Impulse 頻道，需與 Receiver 的 ChannelMask 對應 (1 = default)")]
        private int channel = 1;
        #endregion 頻道

        #region 元件引用 (Lazy Loading)
        private CinemachineImpulseSource _source;

        /// <summary>取得 ImpulseSource 元件，懶載入快取</summary>
        private CinemachineImpulseSource Source =>
            _source ??= GetComponent<CinemachineImpulseSource>();
        #endregion 元件引用

        #region 取消權杖
        private CancellationTokenSource _cts;
        #endregion 取消權杖

        #region 生命週期
        private void Reset() => ApplyPresetDefaults();

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        #endregion 生命週期

        #region 公開介面
        /// <summary>
        /// 以 Inspector 上的設定觸發震動
        /// 重複呼叫會取消上一次未完成的連續序列
        /// </summary>
        [ContextMenu("Execute")]
        public void Execute()
        {
            var (count, interval) = ResolveRepeat();
            FireSequence(preset, mode, force, duration, count, interval);
        }

        /// <summary>單次/次數模式：覆寫情境、力度、時長、次數</summary>
        public void Execute(ShakePreset overridePreset, float overrideForce, float overrideDuration, int overrideCount = 1)
        {
            FireSequence(overridePreset, mode, overrideForce, overrideDuration, overrideCount, repeatInterval);
        }

        /// <summary>時間模式：在 totalTime 秒內以 freq Hz 持續震動</summary>
        public void ExecuteContinuous(ShakePreset overridePreset, float overrideForce, float overrideDuration,
                                      float totalTime, float freq)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(totalTime * freq));
            float interval = 1f / Mathf.Max(0.1f, freq);
            FireSequence(overridePreset, mode, overrideForce, overrideDuration, count, interval);
        }

        /// <summary>觸發震動並等待全部 (連續) 震動序列結束</summary>
        public async Task ExecuteAsync(CancellationToken token = default)
        {
            var (count, interval) = ResolveRepeat();
            await ExecuteSequenceAsync(preset, mode, force, duration, count, interval, token);
        }

        /// <summary>動態切換情境</summary>
        public void SetPreset(ShakePreset newPreset) => preset = newPreset;

        /// <summary>立即停止所有正在進行的連續震動序列</summary>
        public void Stop() => _cts?.Cancel();

        /// <summary>把目前情境的建議力度時長填回 Inspector</summary>
        [ContextMenu("Apply Preset Defaults")]
        public void ApplyPresetDefaults()
        {
            var (_, presetDuration, presetForce) = GetPresetData(preset);
            force = presetForce;
            duration = presetDuration;
        }
        #endregion 公開介面

        #region 核心邏輯
        /// <summary>啟動 fire-and-forget 連續震動序列 (取消舊序列)</summary>
        private void FireSequence(ShakePreset p, ShakeMode m, float f, float d, int count, float interval)
        {
            RestartCts();
            _ = ExecuteSequenceAsync(p, m, f, d, count, interval, _cts.Token);
        }

        /// <summary>執行 N 次連續震動</summary>
        private async Task ExecuteSequenceAsync(
            ShakePreset p, ShakeMode m, float f, float d, int count, float interval, CancellationToken token)
        {
            count = Mathf.Max(1, count);

            for (int i = 0; i < count; i++)
            {
                if (token.IsCancellationRequested) return;

                FireOnce(p, m, f, d);

                int waitMs = (i == count - 1)
                    ? Mathf.RoundToInt(d * 1000f)
                    : Mathf.RoundToInt(interval * 1000f);

                try
                {
                    await Task.Delay(waitMs, token);
                }
                catch (System.OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>觸發單次 Impulse</summary>
        private void FireOnce(ShakePreset p, ShakeMode m, float f, float d)
        {
            var s = Source;
            if (s == null) return;

            var (shape, _, _) = GetPresetData(p);

            s.ImpulseDefinition.ImpulseShape    = shape;
            s.ImpulseDefinition.ImpulseDuration = d;
            s.ImpulseDefinition.ImpulseChannel  = channel;
            s.DefaultVelocity = GetVelocity(m);
            s.GenerateImpulseWithForce(f);
        }

        /// <summary>
        /// 解析目前 RepeatMode 對應的 (次數, 間隔)
        /// 兩種模式統一輸出成這個形式，後續邏輯只看次數+間隔即可
        /// </summary>
        private (int count, float interval) ResolveRepeat()
        {
            return repeatMode switch
            {
                RepeatMode.CountBased => (Mathf.Max(1, repeatCount), repeatInterval),
                RepeatMode.DurationBased => (
                    Mathf.Max(1, Mathf.RoundToInt(totalDuration * frequency)),
                    1f / Mathf.Max(0.1f, frequency)
                ),
                _ => (1, 0f),
            };
        }

        /// <summary>重啟 CancellationTokenSource，取消上一次未完成的連續序列</summary>
        private void RestartCts()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }
        #endregion 核心邏輯

        #region 情境表
        /// <summary>
        /// 情境 → (波形, 建議時長, 建議力度) 對應表
        /// 想新增情境只要在 ShakePreset 加一筆，這裡再補一筆 case 即可 (OCP)
        /// </summary>
        private static (CinemachineImpulseDefinition.ImpulseShapes shape, float duration, float force)
            GetPresetData(ShakePreset p)
        {
            return p switch
            {
                ShakePreset.MachineActivate => (CinemachineImpulseDefinition.ImpulseShapes.Bump,      0.35f, 0.8f),
                ShakePreset.Impact          => (CinemachineImpulseDefinition.ImpulseShapes.Recoil,    0.50f, 1.2f),
                ShakePreset.Explosion       => (CinemachineImpulseDefinition.ImpulseShapes.Explosion, 1.00f, 2.5f),
                ShakePreset.Earthquake      => (CinemachineImpulseDefinition.ImpulseShapes.Rumble,    2.00f, 1.5f),
                _                           => (CinemachineImpulseDefinition.ImpulseShapes.Bump,      0.50f, 1.0f),
            };
        }
        #endregion 情境表

        #region 方向計算
        /// <summary>依模式回傳震動方向向量 (已 normalized)</summary>
        private Vector3 GetVelocity(ShakeMode m)
        {
            return m switch
            {
                ShakeMode.Random     => RandomXY(),
                ShakeMode.Horizontal => new Vector3(RandomSign(), 0f, 0f),
                ShakeMode.Vertical   => new Vector3(0f, RandomSign(), 0f),
                ShakeMode.Forward    => new Vector3(0f, 0f, RandomSign()),
                ShakeMode.Custom     => customDirection.sqrMagnitude > 0.0001f
                                         ? customDirection.normalized
                                         : Vector3.right,
                _ => Vector3.up,
            };
        }

        /// <summary>XY 平面隨機方向 (Z=0)</summary>
        private static Vector3 RandomXY()
        {
            var v = Random.insideUnitCircle;
            if (v.sqrMagnitude < 0.0001f) return Vector3.right;
            v.Normalize();
            return new Vector3(v.x, v.y, 0f);
        }

        /// <summary>隨機回傳 -1 或 1</summary>
        private static float RandomSign() => Random.value < 0.5f ? -1f : 1f;
        #endregion 方向計算
    }
}
