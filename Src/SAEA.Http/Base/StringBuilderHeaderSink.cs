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
*命名空间：SAEA.Http.Base
*文件名： StringBuilderHeaderSink
*版本号： v26.11.16.1
*唯一标识：156b039f-f2b1-4c8a-bcb3-c66f2efe327a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 16:48:11
*描述：把响应报头分段累加进 StringBuilder 的写出目标
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 16:48:11
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增 StringBuilder 版报头分段写出目标
*
*****************************************************************************/
using System;
using System.Text;

namespace SAEA.Http.Base
{
    /// <summary>
    /// 报头分段写出目标：按分段描述把文本累加进 <see cref="StringBuilder"/>，
    /// 换行取 <see cref="Environment.NewLine"/>，与报头字节路径的换行语义保持一致。
    /// </summary>
    internal struct StringBuilderHeaderSink : IHttpHeaderChunkSink
    {
        /// <summary>
        /// 目标缓冲
        /// </summary>
        public StringBuilder Builder;

        /// <summary>
        /// 用指定缓冲创建写出目标
        /// </summary>
        /// <param name="builder">目标缓冲，由调用方负责最后的 ToString</param>
        public StringBuilderHeaderSink(StringBuilder builder)
        {
            Builder = builder;
        }

        /// <summary>
        /// 写出一段文本，不追加换行
        /// </summary>
        /// <param name="value">文本段，允许为 null（按空段处理）</param>
        public void Text(string value)
        {
            if (!string.IsNullOrEmpty(value))
                Builder.Append(value);
        }

        /// <summary>
        /// 写出一段文本并追加一个换行
        /// </summary>
        /// <param name="value">文本段，允许为 null（此时只写换行）</param>
        public void Line(string value)
        {
            Text(value);
            Builder.AppendLine();
        }

        /// <summary>
        /// 只写一个换行
        /// </summary>
        public void Break()
        {
            Builder.AppendLine();
        }
    }
}