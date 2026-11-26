/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 

*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Http.Common
*文件名： ContentDispositionHelper
*版本号： v26.11.26.1
*唯一标识：a3c85a60-72a8-4c13-9193-e9230f34e412
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 17:46:21
*描述：Content-Disposition 头值生成
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 17:46:21
*修改人： yswenli
*版本号： v26.11.26.1
*描述：新增，集中处理文件名清洗与 RFC 5987 编码
*
*****************************************************************************/
using System;
using System.Text;

namespace SAEA.Http.Common
{
    /// <summary>
    /// Content-Disposition 头值生成
    /// </summary>
    public static class ContentDispositionHelper
    {
        /// <summary>
        /// 生成 Content-Disposition 头值，文件名按 RFC 5987 编码并去除头注入字符
        /// </summary>
        /// <param name="fileName">下载文件名，可含中文；null 或空白返回 null</param>
        /// <param name="inline">true 生成 inline，默认 false 生成 attachment</param>
        /// <returns>可直接写入响应头的值；文件名为空时返回 null</returns>
        public static string Build(string fileName, bool inline = false)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            // 只取最后一段：客户端传入的路径不能把目录带进下载文件名
            var slashIndex = fileName.LastIndexOfAny(new[] { '/', '\\' });

            var name = slashIndex >= 0 ? fileName.Substring(slashIndex + 1) : fileName;

            // 控制字符会截断或伪造响应头，先整体剔除
            var cleaned = new StringBuilder(name.Length);

            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];

                if (!char.IsControl(c))
                    cleaned.Append(c);
            }

            var cleanName = cleaned.ToString();

            if (string.IsNullOrWhiteSpace(cleanName))
                return null;

            // ASCII 回退名：引号与反斜杠会破坏 quoted-string，统一替换为下划线
            var fallback = new StringBuilder(cleanName.Length);

            for (var i = 0; i < cleanName.Length; i++)
            {
                var c = cleanName[i];

                if (c >= 0x20 && c <= 0x7E && c != '"' && c != '\\')
                    fallback.Append(c);
                else
                    fallback.Append('_');
            }

            var fallbackName = fallback.Length > 0 ? fallback.ToString() : "download";

            // filename* 用 UTF-8 百分号编码，兼容中文等非 ASCII 文件名
            return (inline ? "inline" : "attachment")
                + "; filename=\"" + fallbackName + "\"; filename*=UTF-8''" + Uri.EscapeDataString(cleanName);
        }
    }
}