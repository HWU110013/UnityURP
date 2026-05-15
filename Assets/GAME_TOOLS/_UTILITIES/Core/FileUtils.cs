using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace CatzTools
{

    #region 檔案工具類別

    /// <summary>
    /// 檔案工具類別 - 提供檔案操作相關功能
    /// </summary>
    public static class FileUtils
    {
        #region 路徑工具方法

        /// <summary>
        /// 確保路徑存在，如果不存在則建立
        /// </summary>
        /// <param name="path">路徑</param>
        public static void EnsureDirectoryExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && !System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
                CatzLogger.LogDebug($"已建立路徑: {path}");
            }
        }

        /// <summary>
        /// 安全地讀取文字檔案
        /// </summary>
        /// <param name="filePath">檔案路徑</param>
        /// <returns>檔案內容，失敗時返回空字串</returns>
        public static string SafeReadAllText(string filePath)
        {
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    return System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                CatzLogger.LogError($"讀取檔案失敗: {filePath}, {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// 安全地寫入文字檔案
        /// </summary>
        /// <param name="filePath">檔案路徑</param>
        /// <param name="content">檔案內容</param>
        /// <returns>是否成功</returns>
        public static bool SafeWriteAllText(string filePath, string content)
        {
            try
            {
                string directory = System.IO.Path.GetDirectoryName(filePath);
                EnsureDirectoryExists(directory);

                System.IO.File.WriteAllText(filePath, content, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                CatzLogger.LogError($"寫入檔案失敗: {filePath}, {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 取得檔案大小（位元組）
        /// </summary>
        /// <param name="filePath">檔案路徑</param>
        /// <returns>檔案大小，失敗時返回0</returns>
        public static long GetFileSize(string filePath)
        {
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    var fileInfo = new System.IO.FileInfo(filePath);
                    return fileInfo.Length;
                }
            }
            catch (Exception ex)
            {
                CatzLogger.LogError($"取得檔案大小失敗: {filePath}, {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// 格式化檔案大小為人類可讀格式
        /// </summary>
        /// <param name="bytes">位元組數</param>
        /// <returns>格式化的大小字串</returns>
        public static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:N1} {suffixes[counter]}";
        }
        /// <summary>
        /// 異步寫入所有文字
        /// </summary>
        /// <param name="filePath">檔案路徑</param>
        /// <param name="content">文字內容</param>
        /// <returns>寫入任務</returns>
        public static async Task WriteAllTextAsync(string filePath, string content)
        {
            await Task.Run(() => File.WriteAllText(filePath, content));
        }

        /// <summary>
        /// 異步讀取所有文字
        /// </summary>
        /// <param name="filePath">檔案路徑</param>
        /// <returns>文字內容</returns>
        public static async Task<string> ReadAllTextAsync(string filePath)
        {
            return await Task.Run(() => File.ReadAllText(filePath));
        }
        #endregion
    }

    #endregion
}