/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *机器名称：WENLI-PC
 *公司名称：yswenli
 *命名空间：SAEA.Sockets.Interface
 *文件名： IFrameCoder
 *版本号： v26.4.23.1
 *唯一标识：13543eec-5e29-4910-8a4e-d5ca591fc5b7
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2018/03/18 02:16:04
 *描述：IFrameCoder接口
 *
 *=====================================================================
 *修改标记
 *修改时间：2018/03/18 02:16:04
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：IFrameCoder接口
 *
 *****************************************************************************/
using System;
using SAEA.Sockets.Base;

namespace SAEA.Sockets.Interface
{
    /// <summary>
    /// 帧式（9 字节头 + body）编解码器专用：仅 BaseCoder 及其派生实现。
    /// </summary>
    public interface IFrameCoder : ICoder
    {
        /// <summary>
        /// 增量零拷贝解码：frame.Content / onFile 仅在回调期间有效。
        /// </summary>
        void DecodeStream(ReadOnlySpan<byte> data, IFrameHandler handler, Action<DateTime> onHeart = null, FileSpanHandler onFile = null);
    }

    /// <summary>
    /// span 版文件回调。C# 不允许 Action&lt;ReadOnlySpan&lt;byte&gt;&gt;，故用具名委托。
    /// </summary>
    public delegate void FileSpanHandler(ReadOnlySpan<byte> content);
}