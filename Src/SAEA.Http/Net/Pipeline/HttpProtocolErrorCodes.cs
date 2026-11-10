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
*命名空间：SAEA.Http
*文件名： HttpProtocolErrorCodes
*版本号： v26.11.5.1
*唯一标识：a9b7428f-b25a-445f-a08f-cc7b73fae5d7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 13:12:01
*描述：HTTP 分帧器把协议错误种类编码进 FrameProtocolException 的固定 token
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 13:12:01
*修改人： yswenli
*版本号： v26.11.5.1
*描述：HTTP 分帧器把协议错误种类编码进 FrameProtocolException 的固定 token
*
*****************************************************************************/
namespace SAEA.Http
{
    /// <summary>
    /// HTTP 分帧器的协议错误 token。
    /// <para>
    /// <see cref="SAEA.Sockets.Pipeline.FrameProtocolException"/> 是 sealed 类型，无法用派生类承载错误种类，
    /// 因此分帧器把固定 token 直接写进异常 <c>Message</c>，链上的处理器按 token 转成对应响应。
    /// </para>
    /// </summary>
    public static class HttpProtocolErrorCodes
    {
        /// <summary>请求格式非法，应返回 400。</summary>
        public const string BadRequest = "400";

        /// <summary>请求体超过配置上限，应返回 413。</summary>
        public const string RequestEntityTooLarge = "413";

        /// <summary>请求行与报头超过配置上限，应返回 431。</summary>
        public const string RequestHeaderFieldsTooLarge = "431";

        /// <summary>请求使用 chunked 传输编码，当前不支持，应返回 400。</summary>
        public const string ChunkedNotSupported = "chunked";
    }
}