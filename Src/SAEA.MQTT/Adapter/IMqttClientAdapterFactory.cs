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
*命名空间：SAEA.MQTT.Adapter
*文件名： IMqttClientAdapterFactory
*版本号： v26.4.23.1
*唯一标识：53ad7d3f-0c39-4ebf-8ea6-51ac2d93680d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttClientAdapterFactory接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Client.Options;

namespace SAEA.MQTT.Adapter
{
    /// <summary>
    /// 客户端信道适配器工厂，按客户端选项创建对应传输方式的适配器
    /// </summary>
    public interface IMqttClientAdapterFactory
    {
        /// <summary>
        /// 创建客户端信道适配器
        /// </summary>
        /// <param name="options">客户端连接选项，决定传输方式与安全设置</param>
        /// <returns>尚未建立连接的客户端适配器</returns>
        IMqttChannelAdapter CreateClientAdapter(IMqttClientOptions options);
    }
}
