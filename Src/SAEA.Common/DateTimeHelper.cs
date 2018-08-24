/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： Class1
*版本号： V1.0.0.0
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Threading;

namespace SAEA.Common
{
    public static class DateTimeHelper
    {

        static DateTime _current;

        static DateTimeHelper()
        {
            new Thread(new ThreadStart(() =>
            {
                try
                {
                    while (true)
                    {
                        _current = DateTime.Now;
                        Thread.Sleep(1);
                    }
                }
                catch { }

            }))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest
            }.Start();
        }

        public static DateTime Now
        {
            get
            {
                if (_current == null || _current.Year == 1)
                {
                    _current = DateTime.Now;
                }
                return _current;
            }
        }

        /// <summary>
        /// 将中国时间转换成UTC
        /// </summary>
        public static DateTime UtcNow
        {
            get
            {
                return DateTimeHelper.Now.AddHours(-8);
            }
        }

        public static string ToString(string format = "yyyy-MM-dd HH:mm:ss.fff")
        {
            return Now.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ToFString(this DateTime dt, string format = "yyyy-MM-dd HH:mm:ss.fff")
        {
            return dt.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 获取unix 时间戳
        /// </summary>
        /// <returns></returns>
        public static int GetUnixTick()
        {
            TimeSpan ts = DateTimeHelper.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt32(ts.TotalSeconds);
        }

        /// <summary>
        /// 将Unix时间戳转换成DateTime
        /// </summary>
        /// <param name="unixTick"></param>
        /// <returns></returns>
        public static DateTime GetDateTimeWithUnixTick(int unixTick)
        {
            var start = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return start.AddSeconds(unixTick);
        }


    }
}
