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
 *命名空间：SAEA.Sockets.Model
 *文件名： StreamUserToken
 *版本号： v26.4.23.1
 *唯一标识：923ea6dd-9c17-4f6e-b560-efe6efd48d78
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2019/02/11 17:03:06
 *描述：ChannelInfo接口
 *
 *=====================================================================
 *修改标记
 *修改时间：2019/02/11 17:03:06
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：ChannelInfo接口
 *
 *****************************************************************************/
using System.IO;
using System.IO.Pipelines;

namespace SAEA.Sockets.Model
{
    /// <summary>
    /// Stream 通道的用户令牌，承载连接流与其 PipeReader 输入。
    /// </summary>
    public class StreamUserToken : SAEA.Sockets.Base.BaseUserToken
    {
        /// <summary>
        /// 获取或设置通道的网络流
        /// </summary>
        public Stream Stream { get; set; }

        /// <summary>
        /// 获取或设置通道的 PipeReader
        /// </summary>
        public PipeReader Input { get; set; }
    }
}
