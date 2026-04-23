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
*命名空间：SAEA.MQTT.Protocol
*文件名： MqttConnectReasonCodeConverter
*版本号： v26.4.23.1
*唯一标识：8ef18acd-376f-44da-85f3-5bfc53895590
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttConnectReasonCodeConverter转换器类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttConnectReasonCodeConverter转换器类
*
*****************************************************************************/
using SAEA.MQTT.Exceptions;

namespace SAEA.MQTT.Protocol
{
    public class MqttConnectReasonCodeConverter
    {
        public MqttConnectReturnCode ToConnectReturnCode(MqttConnectReasonCode reasonCode)
        {
            switch (reasonCode)
            {
                case MqttConnectReasonCode.Success:
                    {
                        return MqttConnectReturnCode.ConnectionAccepted;
                    }

                case MqttConnectReasonCode.NotAuthorized:
                    {
                        return MqttConnectReturnCode.ConnectionRefusedNotAuthorized;
                    }

                case MqttConnectReasonCode.BadUserNameOrPassword:
                    {
                        return MqttConnectReturnCode.ConnectionRefusedBadUsernameOrPassword;
                    }

                case MqttConnectReasonCode.ClientIdentifierNotValid:
                    {
                        return MqttConnectReturnCode.ConnectionRefusedIdentifierRejected;
                    }

                case MqttConnectReasonCode.UnsupportedProtocolVersion:
                    {
                        return MqttConnectReturnCode.ConnectionRefusedUnacceptableProtocolVersion;
                    }

                case MqttConnectReasonCode.ServerUnavailable:
                case MqttConnectReasonCode.ServerBusy:
                case MqttConnectReasonCode.ServerMoved:
                    {
                        return MqttConnectReturnCode.ConnectionRefusedServerUnavailable;
                    }

                default:
                    {
                        throw new MqttProtocolViolationException("Unable to convert connect reason code (MQTTv5) to return code (MQTTv3).");
                    }
            }
        }

        public MqttConnectReasonCode ToConnectReasonCode(MqttConnectReturnCode returnCode)
        {
            switch (returnCode)
            {
                case MqttConnectReturnCode.ConnectionAccepted:
                    {
                        return MqttConnectReasonCode.Success;
                    }

                case MqttConnectReturnCode.ConnectionRefusedUnacceptableProtocolVersion:
                    {
                        return MqttConnectReasonCode.UnsupportedProtocolVersion;
                    }

                case MqttConnectReturnCode.ConnectionRefusedBadUsernameOrPassword:
                    {
                        return MqttConnectReasonCode.BadUserNameOrPassword;
                    }

                case MqttConnectReturnCode.ConnectionRefusedIdentifierRejected:
                    {
                        return MqttConnectReasonCode.ClientIdentifierNotValid;
                    }

                case MqttConnectReturnCode.ConnectionRefusedServerUnavailable:
                    {
                        return MqttConnectReasonCode.ServerUnavailable;
                    }

                case MqttConnectReturnCode.ConnectionRefusedNotAuthorized:
                    {
                        return MqttConnectReasonCode.NotAuthorized;
                    }

                default:
                    {
                        throw new MqttProtocolViolationException("Unable to convert connect reason code (MQTTv5) to return code (MQTTv3).");
                    }
            }
        }
    }
}
