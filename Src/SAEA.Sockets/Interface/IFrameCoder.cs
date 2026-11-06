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
 *修改时间：2026/11/6 12:52:27
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：补全公开成员的 XML 注释
 *
 *****************************************************************************/
using System;

namespace SAEA.Sockets.Interface
{
    /// <summary>
    /// 帧式（9 字节头 + body）编解码器专用：仅 BaseCoder 及其派生实现。
    /// </summary>
    public interface IFrameCoder : ICoder
    {
        /// <summary>
        /// 增量零拷贝解码：frame.Content / onFile 仅在回调期间有效。
        /// 非线程安全；与 Decode 共享解码状态，不得并发调用。
        /// </summary>
        /// <param name="data">本次喂入的增量字节，仅在本次调用期间有效</param>
        /// <param name="handler">完整数据帧回调，frame.Content 仅在回调期间有效</param>
        /// <param name="onHeart">心跳回调（可选），收到 body 长度为 0 的心跳帧时触发</param>
        /// <param name="onFile">大帧/文件内容回调（可选），content 仅在回调期间有效</param>
        void DecodeStream(ReadOnlySpan<byte> data, IFrameHandler handler, Action<DateTime> onHeart = null, FileSpanHandler onFile = null);
    }

    /// <summary>
    /// span 版文件回调。C# 不允许 Action&lt;ReadOnlySpan&lt;byte&gt;&gt;，故用具名委托。
    /// </summary>
    /// <param name="content">大帧/文件内容，仅在本次回调期间有效，跨回调保存必须复制</param>
    public delegate void FileSpanHandler(ReadOnlySpan<byte> content);
}