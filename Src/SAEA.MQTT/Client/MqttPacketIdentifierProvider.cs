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
*命名空间：SAEA.MQTT.Client
*文件名： MqttPacketIdentifierProvider
*版本号： v26.4.23.1
*唯一标识：c68420a4-93b5-42ce-80e9-1a2333165aff
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
namespace SAEA.MQTT.Client
{
    public class MqttPacketIdentifierProvider
    {
        private readonly object _syncRoot = new object();
        private ushort _value;

        public void Reset()
        {
            lock (_syncRoot)
            {
                _value = 0;
            }
        }

        public ushort GetNextPacketIdentifier()
        {
            lock (_syncRoot)
            {
                _value++;

                if (_value == 0)
                {
                    // As per official MQTT documentation the package identifier should never be 0.
                    _value = 1;
                }

                return _value;
            }
        }
    }
}
