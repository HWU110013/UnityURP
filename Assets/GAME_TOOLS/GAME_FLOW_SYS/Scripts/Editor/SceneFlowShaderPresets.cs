#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using CatzTools.GameFlow;

namespace CatzTools.GameFlow.Editor
{
    #region ShaderPreset 資料結構
    /// <summary>Shader 預設定義（含中英雙語名稱）</summary>
    public readonly struct ShaderPreset
    {
        /// <summary>Shader 名稱（不含前綴）</summary>
        public readonly string ShaderName;
        /// <summary>中文顯示名稱</summary>
        public readonly string NameZH;
        /// <summary>英文顯示名稱</summary>
        public readonly string NameEN;
        /// <summary>預設遮罩顏色</summary>
        public readonly Color DefaultColor;

        /// <summary>依目前語系回傳顯示名稱</summary>
        public string DisplayName => SceneFlowLocale.IsZH ? NameZH : NameEN;

        public ShaderPreset(string shaderName, string nameZH, string nameEN, Color defaultColor)
        {
            ShaderName = shaderName;
            NameZH = nameZH;
            NameEN = nameEN;
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
            new("CircleWipe",      "圓形擦除",   "Circle Wipe",      Color.black),
            new("DiamondWipe",     "菱形擦除",   "Diamond Wipe",     Color.black),
            new("SquareWipe",      "方格擦除",   "Square Wipe",      Color.black),
            new("Dissolve",        "噪點溶解",   "Dissolve",         Color.black),
            new("Pixelate",        "馬賽克",     "Pixelate",         Color.black),
            new("Blur",            "模糊",       "Blur",             Color.black),
            new("Blinds",          "百葉窗",     "Blinds",           Color.black),
            new("Streak",          "拉絲",       "Streak",           Color.black),
            new("Vortex",          "漩渦",       "Vortex",           Color.black),
            new("Zoom",            "縮放",       "Zoom",             Color.black),
            new("PageTurn",        "翻頁",       "Page Turn",        Color.black),
            new("DualBlinds",      "交叉百葉",   "Dual Blinds",      Color.black),
            new("ShapeWipe",       "形狀擦除",   "Shape Wipe",       Color.black),
            new("Ripple",          "水波紋",     "Ripple",           Color.black),
            new("Glitch",          "故障",       "Glitch",           Color.black),
            new("CurtainImage",    "圖片拉簾",   "Curtain Image",    Color.black),
            new("RadialWipe",      "時鐘擦除",   "Radial Wipe",      Color.black),
            new("InkSpread",       "墨水暈染",   "Ink Spread",       Color.black),
            new("WhipPan",         "甩鏡",       "Whip Pan",         Color.black),
            new("Scanline",        "掃描線",     "Scanline",         Color.black),
            new("Shatter",         "碎裂",       "Shatter",          Color.black),
            new("ColorDrain",      "色彩抽離",   "Color Drain",      Color.black),
            new("Jelly",           "果凍",       "Jelly",            Color.black),
            new("Burn",            "燃燒",       "Burn",             Color.black),
            new("TVOff",           "老電視",     "TV Off",           Color.black),
            new("Scroll",          "捲軸",       "Scroll",           Color.black),
            new("Fog",             "霧氣",       "Fog",              Color.black),
            new("Kaleidoscope",    "萬花筒",     "Kaleidoscope",     Color.black),
            new("Raindrop",        "雨滴",       "Raindrop",         Color.black),
            new("Mosaic",          "馬賽克拼圖", "Mosaic",           Color.black),
            new("DoubleExposure",  "雙重曝光",   "Double Exposure",  Color.black),
            new("Heartbeat",       "心跳",       "Heartbeat",        Color.black),
            new("WindBlow",        "風吹",       "Wind Blow",        Color.black),
            new("Prism",           "稜鏡",       "Prism",            Color.black),
            new("ComicPanel",      "漫畫格",     "Comic Panel",      Color.black),
            new("MatrixRain",      "矩陣雨",     "Matrix Rain",      Color.black),
            new("Sumi",            "水墨",       "Sumi",             Color.black),
            new("Flashbang",       "閃光彈",     "Flashbang",        Color.black),
        };
        #endregion 預設定義

        #region 反查顯示名稱
        /// <summary>
        /// 從 Material 名稱反查顯示名（依目前語系）。
        /// 系統預設 → i18n 翻譯；自訂材質 → 直接用檔名。
        /// </summary>
        public static string GetDisplayName(Material mat)
        {
            if (mat == null) return SceneFlowLocale.EdgeNoMat;
            string matName = mat.name.Replace("SF_", "");
            foreach (var p in Presets)
            {
                if (p.ShaderName == matName) return p.DisplayName;
            }
            return matName; // 使用者自訂 Material，直接顯示原名
        }
        #endregion 反查顯示名稱

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
                CatzLogger.LogError("FlowManager", $"[SceneFlow] Shader not found: {ShaderPrefix}{preset.ShaderName}");
                return null;
            }

            // 建立 Material
            mat = new Material(shader);
            mat.SetColor("_Color", preset.DefaultColor);
            mat.SetFloat("_Progress", 0f);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            CatzLogger.Log("FlowManager", $"[SceneFlow] Created transition Material: {path}");

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
