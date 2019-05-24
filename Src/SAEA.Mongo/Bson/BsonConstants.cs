/****************************************************************************
*项目名称：SAEA.Mongo.Bson
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Bson
*类 名 称：BsonConstants
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/22 11:09:15
*描述：
*=====================================================================
*修改时间：2019/5/22 11:09:15
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Mongo.Bson
{
    public static class BsonConstants
    {
        // private static fields
        private static readonly long __dateTimeMaxValueMillisecondsSinceEpoch;
        private static readonly long __dateTimeMinValueMillisecondsSinceEpoch;
        private static readonly DateTime __unixEpoch;

        // static constructor
        static BsonConstants()
        {
            // unixEpoch has to be initialized first
            __unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            __dateTimeMaxValueMillisecondsSinceEpoch = (DateTime.MaxValue - __unixEpoch).Ticks / 10000;
            __dateTimeMinValueMillisecondsSinceEpoch = (DateTime.MinValue - __unixEpoch).Ticks / 10000;
        }

        // public static properties
        /// <summary>
        /// Gets the number of milliseconds since the Unix epoch for DateTime.MaxValue.
        /// </summary>
        public static long DateTimeMaxValueMillisecondsSinceEpoch
        {
            get { return __dateTimeMaxValueMillisecondsSinceEpoch; }
        }

        /// <summary>
        /// Gets the number of milliseconds since the Unix epoch for DateTime.MinValue.
        /// </summary>
        public static long DateTimeMinValueMillisecondsSinceEpoch
        {
            get { return __dateTimeMinValueMillisecondsSinceEpoch; }
        }

        /// <summary>
        /// Gets the Unix Epoch for BSON DateTimes (1970-01-01).
        /// </summary>
        public static DateTime UnixEpoch { get { return __unixEpoch; } }
    }
}
