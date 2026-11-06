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
*命名空间：SAEA.WebSocket
*文件名： SubProtocolType
*版本号： v26.4.23.1
*唯一标识：6e4a335c-4222-498a-b352-1e913f980850
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/06/02 15:29:28
*描述：SubProtocolType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:30
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.WebSocket
{
    /// <summary>
    /// WebSocket 握手时协商的子协议名称常量
    /// </summary>
    public class SubProtocolType
    {
        /// <summary>
        /// 空子协议，表示不协商子协议
        /// </summary>
        public const string Empty = "";

        /// <summary>
        /// 默认子协议名
        /// </summary>
        public const string Default = "SAEA.WebSocket";

        /// <summary>
        /// JSON-RPC 子协议名
        /// </summary>
        public const string Json = "jsonrpc";

        /// <summary>
        /// 第二种子协议名
        /// </summary>
        public const string Protocol2 = "protocol2";
    }
}
