using System;
/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Common
*类 名 称：RandomHelper
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/16 9:43:28
*描述：
*=====================================================================
*修改时间：2019/1/16 9:43:28
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/

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
    }
}
