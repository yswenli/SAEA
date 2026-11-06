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
*命名空间：SAEA.MQTT.Server
*文件名： IMqttServerConnectionValidator
*版本号： v26.4.23.1
*唯一标识：05becab3-2dd3-4e28-be62-d36d47ed2880
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServerConnectionValidator接口
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

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 连接校验器，在 CONNECT 报文解析后决定是否接受该客户端
    /// </summary>
    public interface IMqttServerConnectionValidator
    {
        /// <summary>
        /// 校验连接请求，通过上下文的 ReturnCode 或 ReasonCode 给出接受或拒绝的原因
        /// </summary>
        /// <param name="context">校验上下文，含客户端标识、端点、凭据、遗嘱及 MQTT 5.0 属性</param>
        /// <returns>校验完成的任务，结果写入上下文的 ReasonCode</returns>
        Task ValidateConnectionAsync(MqttConnectionValidatorContext context);
    }
}
