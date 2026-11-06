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
*命名空间：SAEA.MQTT.Client.ExtendedAuthenticationExchange
*文件名： IMqttExtendedAuthenticationExchangeHandler
*版本号： v26.4.23.1
*唯一标识：09905d56-f0cc-4ad7-b1c1-b8a4e3a53250
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttExtendedAuthenticationExchangeHandler接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.MQTT.Client.ExtendedAuthenticationExchange
{
    /// <summary>
    /// MQTT 5.0 增强认证（AUTH 报文）请求的处理接口
    /// </summary>
    public interface IMqttExtendedAuthenticationExchangeHandler
    {
        /// <summary>
        /// 收到服务端的 AUTH 报文时回调，由实现方决定是否回复认证数据
        /// </summary>
        /// <param name="context">本次认证交换的上下文，含原因码、认证方法与数据及客户端实例</param>
        /// <returns>回调完成的任务</returns>
        Task HandleRequestAsync(MqttExtendedAuthenticationExchangeContext context);
    }
}
