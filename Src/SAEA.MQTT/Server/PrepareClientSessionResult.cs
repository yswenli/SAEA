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
*文件名： PrepareClientSessionResult
*版本号： v26.4.23.1
*唯一标识：9087e352-ad00-4fca-a6a3-69d2742f92e6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：PrepareClientSessionResult接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 准备客户端会话的结果，标识是否复用了已存在的会话
    /// </summary>
    public class PrepareClientSessionResult
    {
        /// <summary>
        /// 是否命中已存在的会话
        /// </summary>
        public bool IsExistingSession { get; set; }

        /// <summary>
        /// 命中的会话连接
        /// </summary>
        public MqttClientConnection Session { get; set; }
    }
}
