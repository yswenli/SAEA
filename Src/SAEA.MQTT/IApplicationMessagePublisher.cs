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
*命名空间：SAEA.MQTT
*文件名： IApplicationMessagePublisher
*版本号： v26.4.23.1
*唯一标识：5455bad5-a6ce-403c-8b7b-f9e3612db026
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IApplicationMessagePublisher接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Client.Publishing;

namespace SAEA.MQTT
{
    /// <summary>
    /// 应用消息发布者，客户端与服务端通过它向外发布消息
    /// </summary>
    public interface IApplicationMessagePublisher
    {
        /// <summary>
        /// 发布一条应用消息
        /// </summary>
        /// <param name="applicationMessage">待发布的应用消息，主题与载荷由发布方校验</param>
        /// <param name="cancellationToken">取消令牌，取消后不再等待服务端确认</param>
        /// <returns>发布结果，QoS 0 恒为成功且不携带报文标识符</returns>
        Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken);
    }
}
