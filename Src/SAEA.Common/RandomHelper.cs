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
*命名空间：SAEA.Common
*文件名： RandomHelper
*版本号： v26.4.23.1
*唯一标识：b6836f0e-9445-4cf5-9dc6-3ee36d4c53a5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/06/04 21:20:12
*描述：RandomHelper帮助类
*
*=====================================================================
*修改标记
*修改时间：2020/06/04 21:20:12
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RandomHelper帮助类
*
*****************************************************************************/
using System;

namespace SAEA.Common
{
    /// <summary>
    /// RandomHelper
    /// </summary>
    public static class RandomHelper
    {
        static Random _rnd = null;

        /// <summary>
        /// RandomHelper
        /// </summary>
        static RandomHelper()
        {
            _rnd = new Random(Environment.TickCount);
        }

        /// <summary>
        /// 获取随机整数
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int GetInt(int min=0,int max = 10000)
        {
            return _rnd.Next(min, max);
        }

        /// <summary>
        /// 创建随机base64 string
        /// </summary>
        /// <returns></returns>
        public static string CreateBase64Key()
        {
            var src = new byte[16];
            new Random(Environment.TickCount).NextBytes(src);
            return Convert.ToBase64String(src);
        }
    }
}
