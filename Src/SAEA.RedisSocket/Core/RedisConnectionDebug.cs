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
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisConnectionDebug
*版本号： v26.4.23.1
*唯一标识：f95b45d6-7242-4d1d-8164-1d8fd11f6396
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/10 10:55:00
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/01/10 10:55:00
*修改人： yswenli
*版本号： v26.4.23.1
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
            ConsoleHelper.WriteLine(Encoding.UTF8.GetString(msg));
        }
    }
}