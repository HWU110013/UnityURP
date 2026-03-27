#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace CatzTools
{
    #region ShaderPreset 資料結構
    /// <summary>Shader 預設定義</summary>
    public readonly struct ShaderPreset
    {
        /// <summary>Shader 名稱（不含前綴）</summary>
        public readonly string ShaderName;
        /// <summary>顯示名稱（中文）</summary>
        public readonly string DisplayName;
        /// <summary>預設遮罩顏色</summary>
        public readonly Color DefaultColor;

        public ShaderPreset(string shaderName, string displayName, Color defaultColor)
        {
            ShaderName = shaderName;
            DisplayName = displayName;
            DefaultColor = defaultColor;
        }
    }
    #endregion ShaderPreset 資料結構

    #region Shader 轉場預設 Material 管理
    /// <summary>
    /// 自動建立並快取內建轉場 Shader 的 Material。
    /// Material 存放於 Presets/TransitionMaterials/ 目錄。
    /// </summary>
    public static class SceneFlowShaderPresets
    {
        #region 常數
        private const string MaterialFolder =
            "Assets/GAME_TOOLS/GAME_FLOW_SYS/Presets/TransitionMaterials";
        private const string ShaderPrefix = "CatzTools/SceneFlow/";
        #endregion 常數

        #region 預設定義
        /// <summary>內建轉場 Shader 預設</summary>
        public static readonly ShaderPreset[] Presets =
        {
            new("CircleWipe", "圓形擦除", Color.black),
            new("DiamondWipe", "菱形擦除", Color.black),
            new("SquareWipe", "方格擦除", Color.black),
            new("Dissolve", "噪點溶解", Color.black),
            new("Pixelate", "馬賽克", Color.black),
            new("Blur", "模糊", Color.black),
            new("Blinds", "百葉窗", Color.black),
            new("Streak", "拉絲", Color.black),
            new("Vortex", "漩渦", Color.black),
            new("Zoom", "縮放", Color.black),
            new("PageTurn", "翻頁", Color.black),
            new("DualBlinds", "交叉百葉", Color.black),
            new("ShapeWipe", "形狀擦除", Color.black),
            new("Ripple", "水波紋", Color.black),
            new("Glitch", "故障", Color.black),
            new("CurtainImage", "圖片拉簾", Color.black),
            new("RadialWipe", "時鐘擦除", Color.black),
            new("InkSpread", "墨水暈染", Color.black),
            new("WhipPan", "甩鏡", Color.black),
            new("Scanline", "掃描線", Color.black),
            new("Shatter", "碎裂", Color.black),
            new("ColorDrain", "色彩抽離", Color.black),
            new("Jelly", "果凍", Color.black),
            new("Burn", "燃燒", Color.black),
            new("TVOff", "老電視", Color.black),
            new("Scroll", "捲軸", Color.black),
            new("Fog", "霧氣", Color.black),
            new("Kaleidoscope", "萬花筒", Color.black),
            new("Raindrop", "雨滴", Color.black),
            new("Mosaic", "馬賽克拼圖", Color.black),
            new("DoubleExposure", "雙重曝光", Color.black),
            new("Heartbeat", "心跳", Color.black),
            new("WindBlow", "風吹", Color.black),
            new("Prism", "稜鏡", Color.black),
            new("ComicPanel", "漫畫格", Color.black),
            new("MatrixRain", "矩陣雨", Color.black),
            new("Sumi", "水墨", Color.black),
            new("Flashbang", "閃光彈", Color.black),
        };
        #endregion 預設定義

        #region 取得 Material
        /// <summary>
        /// 取得指定預設的 Material（不存在則自動建立）
        /// </summary>
        public static Material GetOrCreateMaterial(ShaderPreset preset)
        {
            string path = $"{MaterialFolder}/SF_{preset.ShaderName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            // 確保目錄存在
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                string parent = "Assets/GAME_TOOLS/GAME_FLOW_SYS/Presets";
                AssetDatabase.CreateFolder(parent, "TransitionMaterials");
            }

            // 找 Shader
            var shader = Shader.Find(ShaderPrefix + preset.ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[SceneFlow] 找不到 Shader：{ShaderPrefix}{preset.ShaderName}");
                return null;
            }

            // 建立 Material
            mat = new Material(shader);
            mat.SetColor("_Color", preset.DefaultColor);
            mat.SetFloat("_Progress", 0f);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneFlow] 已建立轉場 Material：{path}");

            return mat;
        }

        /// <summary>
        /// 一次建立所有內建 Material
        /// </summary>
        public static void EnsureAllMaterials()
        {
            foreach (var preset in Presets)
                GetOrCreateMaterial(preset);
        }
        #endregion 取得 Material
    }
    #endregion Shader 轉場預設 Material 管理
}
#endif
