/****************************************************************************
*项目名称：SAEA.MQTT.Interface
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Interface
*类 名 称：IMessagePublisher
*版 本 号： V4.1.2.2
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:52:44
*描述：
*=====================================================================
*修改时间：2019/1/14 19:52:44
*修 改 人： yswenli
*版 本 号： V4.1.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Model;
using System.Threading.Tasks;

namespace SAEA.MQTT.Interface
{
    public interface IMessagePublisher
    {
        Task PublishAsync(MqttMessage applicationMessage);
    }
}
