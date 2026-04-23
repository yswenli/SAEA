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
*修改时间：2020/06/02 15:29:28
*修改人： yswenli
*版本号： v26.4.23.1
*描述：SubProtocolType类型枚举
*
*****************************************************************************/
namespace SAEA.WebSocket
{
    public class SubProtocolType
    {
        public const string Empty = "";

        public const string Default = "SAEA.WebSocket";

        public const string Json = "jsonrpc";

        public const string Protocol2 = "protocol2";
    }
}
