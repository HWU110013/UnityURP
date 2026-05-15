#if UNITY_EDITOR
using System.Collections.Generic;
using CatzTools.GameFlow;

namespace CatzTools.GameFlow.Editor
{
    /// <summary>
    /// Shader 屬性名稱中文翻譯表。
    /// Shader 原始碼使用英文 displayName，中文模式時查此表翻譯。
    /// 找不到翻譯的（自訂 Shader）直接顯示原文。
    /// </summary>
    public static class ShaderPropertyLocale
    {
        private static readonly Dictionary<string, string> _zhMap = new()
        {
            // ── 通用 ──
            { "Progress",              "轉場進度" },
            { "Mask Color",            "遮罩顏色" },
            { "Opacity",               "不透明度" },
            { "Smoothness",            "邊緣柔化" },
            { "Direction",             "方向" },
            { "Randomness",            "隨機順序" },
            { "Texture",               "貼圖" },

            // ── 擷取畫面 ──
            { "Screen Capture",        "擷取畫面" },
            { "Screen Capture (auto)", "擷取畫面（自動）" },
            { "Screen Capture (old)",  "擷取畫面（舊場景）" },

            // ── 色彩 ──
            { "Background Color",      "背景色" },
            { "Base Color",            "基底色" },
            { "Ash Color",             "灰燼顏色" },
            { "Fog Color",             "霧色" },
            { "Ink Color",             "墨色" },
            { "Glow Color",            "發光色" },
            { "Rain Color",            "雨色" },
            { "Flash Color",           "閃光色" },
            { "Border Color",          "格線顏色" },
            { "Scroll Back Color",     "捲軸背面色" },
            { "Dissolve Edge Color",   "溶解邊緣色" },
            { "Tint Color",            "著色" },

            // ── 亮度 / 對比 ──
            { "Brightness",            "亮度" },
            { "Exposure Brightness",   "曝光亮度" },
            { "Contrast",              "對比度" },
            { "Grayscale Contrast",    "灰階對比度" },
            { "Grayscale Brightness",  "灰階亮度偏移" },
            { "Desaturate",            "去飽和度" },
            { "Blend Mode",            "混合模式" },
            { "Drain Speed",           "褪色速度" },
            { "Dot Brightness",        "點陣亮度" },
            { "Line Brightness",       "線條亮度" },

            // ── 幾何 / 座標 ──
            { "Density (long side)",   "密度（長邊分割數）" },
            { "Horizontal Offset",     "水平偏移" },
            { "Vertical Offset",       "垂直偏移" },
            { "Center X",              "中心 X" },
            { "Center Y",              "中心 Y" },
            { "Grid X",                "水平格數" },
            { "Grid Y",                "垂直格數" },
            { "Columns",               "欄數" },
            { "Segments",              "分割數" },
            { "Angle",                 "角度" },
            { "Start Angle",           "起始角度" },

            // ── 邊線 / 寬度 ──
            { "Border Width",          "格線寬度" },
            { "Edge Width",            "邊緣寬度" },
            { "Crack Width",           "裂縫寬度" },
            { "Gap Width",             "間隙寬度" },
            { "Gap",                   "中縫間距" },
            { "Shadow Width",          "陰影寬度" },
            { "Shadow Strength",       "陰影強度" },
            { "Char Edge",             "焦化邊寬度" },
            { "Overlap",               "合攏重疊量" },

            // ── 模糊 / 噪點 ──
            { "Max Blur",              "最大模糊強度" },
            { "Blur Amount",           "模糊量" },
            { "Blur Strength",         "模糊強度" },
            { "Noise Scale",           "噪點縮放" },
            { "Noise Density",         "噪點密度" },
            { "Noise Amount",          "噪點量" },
            { "Distortion",            "畫面扭曲" },
            { "Distort Strength",      "扭曲強度" },
            { "Warp Strength",         "路徑扭曲強度" },

            // ── 速度 / 動畫 ──
            { "Flow Speed",            "流動速度" },
            { "Speed Variation",       "速度差異" },
            { "Spin Speed",            "旋轉速度" },
            { "Zoom Speed",            "縮放速度" },
            { "Frequency",             "頻率" },
            { "Flicker Speed",         "閃爍速度" },
            { "Flicker Strength",      "閃爍強度" },
            { "Wave Speed",            "波紋速度" },
            { "Wobble Speed",          "晃動速度" },
            { "Fall Speed",            "落下速度" },

            // ── 燃燒 / 火焰 ──
            { "Fire Outer Color",      "火焰外緣色" },
            { "Fire Inner Color",      "火焰內焰色" },
            { "Fire Width",            "火焰邊寬度" },
            { "Ignition Count",        "起火點數量" },

            // ── 霧氣 ──
            { "Fog Density",           "霧濃度" },
            { "Layer Count",           "霧層數" },

            // ── 拉簾 ──
            { "Curtain A (L/U)",       "拉簾 A（左/上）" },
            { "Curtain B (R/D)",       "拉簾 B（右/下）" },
            { "Compress Mode",         "壓縮模式" },

            // ── 形狀 ──
            { "Shape Type",            "形狀類型" },
            { "Shape Texture (white=shape)", "形狀貼圖（白=形狀）" },

            // ── 波紋 ──
            { "Ripple Strength (0=off)", "波紋強度（0=關閉）" },
            { "Wave Count",            "波紋數量" },

            // ── 百葉窗 ──
            { "Strip Count",           "條紋數量" },
            { "A Strip Count",         "A 條紋數量" },
            { "B Strip Count (0=off)", "B 條紋數量（0=關閉）" },
            { "A Angle",               "A 角度" },
            { "B Angle",               "B 角度" },
            { "Streak Density",        "絲線密度" },
            { "Streak Strength",       "絲線強度" },

            // ── 漩渦 ──
            { "Spiral Arms",           "螺旋臂數" },
            { "Twist Count",           "旋轉圈數" },

            // ── 翻頁 / 捲軸 ──
            { "Curl Radius",           "捲曲半徑" },
            { "Page Angle",            "翻頁角度" },
            { "Back Shade",            "背面暗度" },

            // ── 像素 ──
            { "Pixel Size",            "像素大小" },

            // ── 心跳 ──
            { "Beat Count",            "心跳次數" },
            { "Zoom Strength",         "放大強度" },
            { "Red Shift",             "紅色偏移" },

            // ── 故障 ──
            { "Tear Strength",         "撕裂強度" },
            { "Tear Count",            "撕裂條數" },
            { "RGB Shift",             "色偏強度" },
            { "Static Strength",       "雪花雜訊強度" },
            { "Char Flicker",          "字元閃爍" },

            // ── 閃光 ──
            { "Flash Peak",            "閃光峰值" },
            { "Flash Intensity",       "閃光強度" },

            // ── 碎裂 ──
            { "Piece Density",         "碎片密度" },
            { "Fly Strength",          "爆發力" },
            { "Gravity",               "重力" },
            { "Shrink Amount",         "碎片縮小量" },
            { "Rotate Strength",       "旋轉強度" },

            // ── 風 ──
            { "Wind Angle",            "風向角度" },
            { "Wind Strength",         "風力強度" },
            { "Particle Size",         "粒子大小" },
            { "Turbulence",            "亂流" },

            // ── 甩鏡 ──
            { "Whip Angle",            "甩鏡角度" },
            { "Sample Count (quality)", "取樣數（品質）" },

            // ── 棱鏡 ──
            { "Split Strength",        "分離強度" },
            { "Split Angle",           "分離角度" },
            { "Aberration",            "色差擴散" },

            // ── 萬花筒 ──
            { "Zoom Power",            "放大倍率" },

            // ── 雨滴 ──
            { "Drop Density",          "水滴密度" },

            // ── 漫畫格 ──
            // Grid X / Grid Y 已在上方

            // ── 矩陣雨 ──
            { "Trail Length",          "尾跡長度" },
            { "Reverse (opening)",     "反向（開場）" },
            { "Reverse (power on)",    "反向（開機）" },

            // ── 掃描線 ──
            { "Line Count",            "掃描線數" },
            { "Line Darkness",         "掃描線深度" },
            { "Scanline Strength",     "掃描線強度" },
            { "Vignette Strength",     "暗角強度" },
            { "Line Phase",            "線條相位" },
            { "Dot Size",              "點陣大小" },

            // ── 水墨 ──
            { "Ink Spread",            "暈染擴散" },
            { "Brush Scale",           "筆觸大小" },
            { "Brush Detail",          "筆觸細節" },
            { "Brush Angle",           "筆刷角度" },
            { "Edge Wetness",          "邊緣濕潤" },

            // ── 馬賽克拼圖 ──
            // Grid X / Grid Y 已在上方

            // ── 果凍 ──
            { "Wobble X",              "水平晃動" },
            { "Wobble Y",              "垂直晃動" },
            { "Squash",                "壓扁強度" },
        };

        /// <summary>英文原文 → 中文翻譯（找不到回傳原文）</summary>
        public static string GetZH(string en) =>
            _zhMap.TryGetValue(en, out var zh) ? zh : en;
    }
}
#endif
