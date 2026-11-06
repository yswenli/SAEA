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
*命名空间：SAEA.MQTT.Formatter
*文件名： ReadFixedHeaderResult
*版本号： v26.4.23.1
*唯一标识：e6350d96-1bd7-4e83-8301-2537ca301e98
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ReadFixedHeaderResult接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Formatter
{
    /// <summary>
    /// 固定头读取结果，标记连接是否已关闭并携带解析出的固定头
    /// </summary>
    public class ReadFixedHeaderResult
    {
        /// <summary>
        /// 对端是否已关闭连接，为 true 时固定头无效
        /// </summary>
        public bool ConnectionClosed { get; set; }

        /// <summary>
        /// 读取到的固定头，连接关闭时保持默认值
        /// </summary>
        public MqttFixedHeader FixedHeader { get; set; }
    }
}
