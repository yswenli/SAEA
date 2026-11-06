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
*命名空间：SAEA.MQTT.Formatter.V5
*文件名： MqttV500PropertiesReader
*版本号： v26.4.23.1
*唯一标识：398621a4-8b81-43b8-8145-4405bc08b4cf
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttV500PropertiesReader接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Formatter.V5
{
    /// <summary>
    /// MQTT 5.0 属性读取器，先读取属性段长度，再按属性标识逐个解析
    /// </summary>
    public class MqttV500PropertiesReader
    {
        private readonly IMqttPacketBodyReader _body;
        private readonly int _length;
        private readonly int _targetOffset;

        /// <summary>
        /// 创建属性读取器并读取属性段长度，报文体已结束时按无属性处理
        /// </summary>
        /// <param name="body">报文体读取器，不能为 null，需定位在属性长度字段</param>
        public MqttV500PropertiesReader(IMqttPacketBodyReader body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));

            if (!body.EndOfStream)
            {
                _length = (int)body.ReadVariableLengthInteger();
            }

            _targetOffset = body.Offset + _length;
        }

        /// <summary>
        /// 最近一次 MoveNext 读出的属性标识
        /// </summary>
        public MqttPropertyId CurrentPropertyId { get; private set; }

        /// <summary>
        /// 读取下一个属性标识，属性段已读完时返回 false
        /// </summary>
        /// <returns>true 表示读到了新的属性标识</returns>
        public bool MoveNext()
        {
            if (_length == 0)
            {
                return false;
            }

            if (_body.Offset >= _targetOffset)
            {
                return false;
            }

            CurrentPropertyId = (MqttPropertyId)_body.ReadByte();
            return true;
        }

        /// <summary>
        /// 读取一对用户属性名与值并追加到集合
        /// </summary>
        /// <param name="userProperties">接收结果的集合，不能为 null</param>
        public void AddUserPropertyTo(List<MqttUserProperty> userProperties)
        {
            if (userProperties == null) throw new ArgumentNullException(nameof(userProperties));

            var name = _body.ReadStringWithLengthPrefix();
            var value = _body.ReadStringWithLengthPrefix();

            userProperties.Add(new MqttUserProperty(name, value));
        }

        /// <summary>
        /// 读取原因字符串
        /// </summary>
        /// <returns>原因字符串</returns>
        public string ReadReasonString()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        /// <summary>
        /// 读取认证方法
        /// </summary>
        /// <returns>认证方法名称</returns>
        public string ReadAuthenticationMethod()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        /// <summary>
        /// 读取认证数据
        /// </summary>
        /// <returns>二进制认证数据</returns>
        public byte[] ReadAuthenticationData()
        {
            return _body.ReadWithLengthPrefix();
        }

        /// <summary>
        /// 读取是否允许保留消息标志
        /// </summary>
        /// <returns>true 表示允许保留消息</returns>
        public bool ReadRetainAvailable()
        {
            return _body.ReadBoolean();
        }

        /// <summary>
        /// 读取会话过期间隔（秒）
        /// </summary>
        /// <returns>会话过期间隔秒数</returns>
        public uint ReadSessionExpiryInterval()
        {
            return _body.ReadFourByteInteger();
        }

        /// <summary>
        /// 读取接收最大报文数
        /// </summary>
        /// <returns>允许并发处理的 QoS 1、QoS 2 报文数上限</returns>
        public ushort ReadReceiveMaximum()
        {
            return _body.ReadTwoByteInteger();
        }

        /// <summary>
        /// 读取服务端支持的最大 QoS，取值只允许 0 与 1，超出时抛出协议违规异常
        /// </summary>
        /// <returns>最大 QoS 等级</returns>
        public MqttQualityOfServiceLevel ReadMaximumQoS()
        {
            byte value = _body.ReadByte();
            if (value > 1)
            {
                throw new MqttProtocolViolationException($"Unexpected Maximum QoS value: {value}");
            }
            
            return (MqttQualityOfServiceLevel)value;
        }

        /// <summary>
        /// 读取服务端分配的客户端标识
        /// </summary>
        /// <returns>分配的客户端标识</returns>
        public string ReadAssignedClientIdentifier()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        /// <summary>
        /// 读取服务端地址引用
        /// </summary>
        /// <returns>可供客户端重连的地址</returns>
        public string ReadServerReference()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        /// <summary>
        /// 读取主题别名最大值
        /// </summary>
        /// <returns>服务端接受的主题别名上限</returns>
        public ushort ReadTopicAliasMaximum()
        {
            return _body.ReadTwoByteInteger();
        }

        /// <summary>
        /// 读取最大报文长度（字节）
        /// </summary>
        /// <returns>允许接收的最大报文总字节数</returns>
        public uint ReadMaximumPacketSize()
        {
            return _body.ReadFourByteInteger();
        }

        /// <summary>
        /// 读取服务端指定的保持连接时间（秒）
        /// </summary>
        /// <returns>服务端要求的保持连接秒数</returns>
        public ushort ReadServerKeepAlive()
        {
            return _body.ReadTwoByteInteger();
        }

        /// <summary>
        /// 读取响应信息
        /// </summary>
        /// <returns>服务端用于构造响应主题的信息</returns>
        public string ReadResponseInformation()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        /// <summary>
        /// 读取是否支持共享订阅
        /// </summary>
        /// <returns>true 表示支持共享订阅</returns>
        public bool ReadSharedSubscriptionAvailable()
        {
            return _body.ReadBoolean();
        }

        /// <summary>
        /// 读取是否支持订阅标识符
        /// </summary>
        /// <returns>true 表示支持订阅标识符</returns>
        public bool ReadSubscriptionIdentifiersAvailable()
        {
            return _body.ReadBoolean();
        }

        /// <summary>
        /// 读取是否支持通配符订阅
        /// </summary>
        /// <returns>true 表示支持通配符订阅</returns>
        public bool ReadWildcardSubscriptionAvailable()
        {
            return _body.ReadBoolean();
        }

        /// <summary>
        /// 读取订阅标识符
        /// </summary>
        /// <returns>订阅标识符数值</returns>
        public uint ReadSubscriptionIdentifier()
        {
            return _body.ReadVariableLengthInteger();
        }

        /// <summary>
        /// 读取载荷格式指示
        /// </summary>
        /// <returns>载荷格式指示，单字节直接转换为枚举</returns>
        public MqttPayloadFormatIndicator? ReadPayloadFormatIndicator()
        {
            return (MqttPayloadFormatIndicator)_body.ReadByte();
        }

        /// <summary>
        /// 读取消息过期间隔（秒）
        /// </summary>
        /// <returns>消息过期间隔秒数</returns>
        public uint ReadMessageExpiryInterval()
        {
            return _body.ReadFourByteInteger();
        }

        /// <summary>
        /// 读取主题别名
        /// </summary>
        /// <returns>主题别名数值</returns>
        public ushort ReadTopicAlias()
        {
            return _body.ReadTwoByteInteger();
        }

        /// <summary>
        /// 读取响应主题
        /// </summary>
        /// <returns>请求响应使用的主题</returns>
        public string ReadResponseTopic()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        /// <summary>
        /// 读取关联数据
        /// </summary>
        /// <returns>用于关联请求与响应的二进制数据</returns>
        public byte[] ReadCorrelationData()
        {
            return _body.ReadWithLengthPrefix();
        }

        /// <summary>
        /// 读取内容类型
        /// </summary>
        /// <returns>载荷的 MIME 内容类型</returns>
        public string ReadContentType()
        {
            return _body.ReadStringWithLengthPrefix();
        }

        /// <summary>
        /// 读取遗嘱延迟间隔（秒）
        /// </summary>
        /// <returns>连接断开后延迟发布遗嘱的秒数</returns>
        public uint ReadWillDelayInterval()
        {
            return _body.ReadFourByteInteger();
        }

        /// <summary>
        /// 读取客户端是否请求响应信息
        /// </summary>
        /// <returns>true 表示客户端请求响应信息</returns>
        public bool RequestResponseInformation()
        {
            return _body.ReadBoolean();
        }

        /// <summary>
        /// 读取客户端是否请求问题信息
        /// </summary>
        /// <returns>true 表示客户端请求问题信息</returns>
        public bool RequestProblemInformation()
        {
            return _body.ReadBoolean();
        }

        /// <summary>
        /// 抛出当前属性标识不适用于该报文类型的协议违规异常
        /// </summary>
        /// <param name="type">当前正在解析的报文类型</param>
        public void ThrowInvalidPropertyIdException(Type type)
        {
            throw new MqttProtocolViolationException($"Property ID '{CurrentPropertyId}' is not supported for package type '{type.Name}'.");
        }
    }
}
