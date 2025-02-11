/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisConnectionDebug
*版本号： v7.0.0.1
*唯一标识：a22caf84-4c61-456e-98cc-cbb6cb2c6d6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/11/5 20:45:02
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/11/5 20:45:02
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System.Text;

using SAEA.Common;

namespace SAEA.RedisSocket.Core
{
    internal class RedisConnectionDebug : RedisConnection
    {
        public RedisConnectionDebug(string ipPort, int actionTimeout = 6 * 1000) : base(ipPort, actionTimeout)
        {

        }

        protected override void _cnn_OnMessage(byte[] msg)
        {
            RedisCoder.Enqueue(msg);
            ConsoleHelper.Write(Encoding.UTF8.GetString(msg));
        }
    }
}
