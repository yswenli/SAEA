/****************************************************************************
*项目名称：SAEA.Mongo.Bson
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Bson
*类 名 称：PowerOf2
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/22 11:11:26
*描述：
*=====================================================================
*修改时间：2019/5/22 11:11:26
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Mongo.Bson
{
    internal static class PowerOf2
    {
        public static bool IsPowerOf2(int n)
        {
            return n == RoundUpToPowerOf2(n);
        }

        public static int RoundUpToPowerOf2(int n)
        {
            if (n < 0 || n > 0x40000000)
            {
                throw new ArgumentOutOfRangeException("n");
            }

            // see: Hacker's Delight, by Henry S. Warren
            n = n - 1;
            n = n | (n >> 1);
            n = n | (n >> 2);
            n = n | (n >> 4);
            n = n | (n >> 8);
            n = n | (n >> 16);
            return n + 1;
        }
    }
}
