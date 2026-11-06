/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.PipelineTest
*文件名： Messages
*版本号： v26.11.5.1
*唯一标识：0ea4a68a-c018-4a76-9250-e43c5035ab8f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：登录请求
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：登录请求
*
*****************************************************************************/
using System;

namespace SAEA.Sockets.PipelineTest
{
    /// <summary>
    /// 登录请求。字段保持简单 POCO，便于 JSON 编解码器直接序列化。
    /// </summary>
    public sealed class LoginRequest
    {
        /// <summary>请求序号，用于把回包和请求配对。</summary>
        public int RequestId { get; set; }

        /// <summary>登录用户名。</summary>
        public string UserName { get; set; }
    }

    /// <summary>
    /// 登录响应。带 <see cref="RequestId"/> 与令牌，便于演示端到端校验。
    /// </summary>
    public sealed class LoginResponse
    {
        /// <summary>与请求相同的序号。</summary>
        public int RequestId { get; set; }

        /// <summary>登录是否成功。</summary>
        public bool Success { get; set; }

        /// <summary>登录成功后下发的业务令牌。</summary>
        public string Token { get; set; }

        /// <summary>登录用户名。</summary>
        public string UserName { get; set; }
    }

    /// <summary>
    /// 心跳。演示回环、粘包/半包、背压与并发用例都复用它。
    /// </summary>
    public sealed class Heartbeat
    {
        /// <summary>帧序号，用于校验顺序与请求回包配对。</summary>
        public int Seq { get; set; }

        /// <summary>业务负载。</summary>
        public string Payload { get; set; }
    }
}