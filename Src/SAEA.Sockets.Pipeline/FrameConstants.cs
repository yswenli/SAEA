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
*命名空间：SAEA.Sockets.Pipeline
*文件名： FrameConstants
*版本号： v26.11.5.1
*唯一标识：9bc7a639-0975-487a-bb05-48810391974f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：线格式常量：8B 小端 body 长度 + 1B type + body
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：线格式常量：8B 小端 body 长度 + 1B type + body
*
*****************************************************************************/
namespace SAEA.Sockets.Pipeline
{
    /// <summary>线格式常量：8B 小端 body 长度 + 1B type + body。</summary>
    public static class FrameConstants
    {
        /// <summary>长度字段占用的字节数。</summary>
        public const int P_LEN = 8;

        /// <summary>类型字段占用的字节数。</summary>
        public const int P_Type = 1;

        /// <summary>帧头总字节数：长度字段 + 类型字段。</summary>
        public const int P_Head = P_LEN + P_Type;
    }
}