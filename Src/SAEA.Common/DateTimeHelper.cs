/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： Class1
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Globalization;

namespace SAEA.Common
{
    /// <summary>
    /// 时间工具类
    /// </summary>
    public static class DateTimeHelper
    {
        /// <summary>
        /// 时间工具类
        /// </summary>
        static DateTimeHelper()
        {
            try
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine(SAEAVersion.MarkText);
                Console.ForegroundColor = oldColor;
            }
            catch { }
        }

        /// <summary>
        /// 获取当前时间
        /// </summary>
        public static DateTime Now
        {
            get
            {
                return DateTime.Now;
            }
        }

        /// <summary>
        /// 获取当前日期
        /// </summary>
        public static DateTime Today
        {
            get
            {
                return DateTime.Today;
            }
        }

        /// <summary>
        /// 获取当前UTC时间
        /// </summary>
        public static DateTime UtcNow
        {
            get
            {
                return TimeZoneInfo.ConvertTimeToUtc(Now, TimeZoneInfo.Local);
            }
        }

        /// <summary>
        /// 根据时区ID获取时间
        /// </summary>
        /// <param name="zoneId">时区ID</param>
        /// <returns>指定时区的时间</returns>
        public static DateTime GetDateTimeByZone(string zoneId)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
        }

        /// <summary>
        /// 将日期时间从一个时区转换到另一个时区
        /// </summary>
        /// <param name="dt">要转换的日期时间</param>
        /// <param name="fromZoneId">源时区ID</param>
        /// <param name="toZoneId">目标时区ID</param>
        /// <returns>转换后的日期时间</returns>
        public static DateTime ConvertToOtherZone(this DateTime dt, string fromZoneId, string toZoneId)
        {
            return TimeZoneInfo.ConvertTime(dt, TimeZoneInfo.FindSystemTimeZoneById(fromZoneId), TimeZoneInfo.FindSystemTimeZoneById(toZoneId));
        }

        /// <summary>
        /// 将当前时间转换为指定格式的字符串
        /// </summary>
        /// <param name="format">日期时间格式</param>
        /// <returns>格式化后的日期时间字符串</returns>
        public static string ToString(string format = "yyyy-MM-dd HH:mm:ss.fff")
        {
            return Now.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 将日期时间转换为指定格式的字符串
        /// </summary>
        /// <param name="dt">要转换的日期时间</param>
        /// <param name="format">日期时间格式</param>
        /// <returns>格式化后的日期时间字符串</returns>
        public static string ToFString(this DateTime dt, string format = "yyyy-MM-dd HH:mm:ss.fff")
        {
            return dt.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 将日期时间转换为GMT格式的字符串
        /// </summary>
        /// <param name="dt">要转换的日期时间</param>
        /// <returns>GMT格式的日期时间字符串</returns>
        public static string ToGMTString(this DateTime dt)
        {
            return dt.ToString("r", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 获取当前时间的Unix时间戳
        /// </summary>
        /// <returns>Unix时间戳</returns>
        public static int GetUnixTick()
        {
            TimeSpan ts = DateTimeHelper.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime();
            return Convert.ToInt32(ts.TotalSeconds);
        }

        /// <summary>
        /// 将日期时间转换为Unix时间戳
        /// </summary>
        /// <param name="dateTime">要转换的日期时间</param>
        /// <returns>Unix时间戳</returns>
        public static int ToUnixTick(this DateTime dateTime)
        {
            TimeSpan ts = dateTime - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime();
            return Convert.ToInt32(ts.TotalSeconds);
        }

        /// <summary>
        /// 将Unix时间戳转换为日期时间
        /// </summary>
        /// <param name="unixTick">Unix时间戳</param>
        /// <returns>转换后的日期时间</returns>
        public static DateTime ToDateTimeByUnixTick(this int unixTick)
        {
            var start = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
            return start.AddSeconds(unixTick);
        }

        /// <summary>
        /// 世界时区名称
        /// </summary>
        public class DateTimeZoneID
        {
            public const string 日界线西国际日期变更线标准时间 = "Dateline Standard Time";
            public const string 中途岛萨摩亚群岛萨摩亚群岛标准时间 = "Samoa Standard Time";
            public const string 夏威夷标准时间 = "Hawaiian Standard Time";
            public const string 阿拉斯加标准时间 = "Alaskan Standard Time";
            public const string 蒂华纳太平洋标准时间 = "Pacific Standard Time";
            public const string 亚利桑那美国山地标准时间 = "US Mountain Standard Time";
            public const string 山地标准时间 = "Mountain Standard Time";
            public const string 墨西哥标准时间2 = "Mexico Standard Time 2";
            public const string 中美洲标准时间 = "Central America Standard Time";
            public const string 中部标准时间 = "Central Standard Time";
            public const string 萨斯喀彻温加拿大中部标准时间 = "Canada Central Standard Time";
            public const string 墨西哥标准时间 = "Mexico Standard Time";
            public const string 东部标准时间 = "Eastern Standard Time";
            public const string 美国东部标准时间 = "US Eastern Standard Time";
            public const string 南美州太平洋标准时间 = "SA Pacific Standard Time";
            public const string 南美州西部标准时间 = "SA Western Standard Time";
            public const string 圣地亚哥南美州太平洋标准时间 = "Pacific SA Standard Time";
            public const string 大西洋标准时间 = "Atlantic Standard Time";
            public const string 纽芬兰标准时间 = "Newfoundland Standard Time";
            public const string 巴西利亚南美州东部标准时间 = "E. South America Standard Time";
            public const string 南美州东部标准时间 = "SA Eastern Standard Time";
            public const string 格陵兰东部标准时间 = "Greenland Standard Time";
            public const string 中大西洋标准时间 = "Mid-Atlantic Standard Time";
            public const string 亚速尔群岛标准时间 = "Azores Standard Time";
            public const string 佛得角群岛标准时间 = "Cape Verde Standard Time";
            public const string 格林威治标准时间 = "Greenwich Standard Time";
            public const string 伦敦都柏林里斯本格林威治标准时间 = "GMT Standard Time";
            public const string 中非西部标准时间 = "W. Central Africa Standard Time";
            public const string 罗马标准时间 = "Romance Standard Time";
            public const string 中欧标准时间1 = "Central European Standard Time";
            public const string 中欧标准时间2 = "Central Europe Standard Time";
            public const string 西欧标准时间 = "W. Europe Standard Time";
            public const string 南非标准时间 = "South Africa Standard Time";
            public const string 东欧标准时间 = "E. Europe Standard Time";
            public const string 埃及标准时间 = "Egypt Standard Time";
            public const string 耶路撒冷标准时间 = "Israel Standard Time";
            public const string FLE标准时间 = "FLE Standard Time";
            public const string GTB标准时间 = "GTB Standard Time";
            public const string 东非标准时间 = "E. Africa Standard Time";
            public const string 阿拉伯标准时间1 = "Arabic Standard Time";
            public const string 阿拉伯标准时间2 = "Arab Standard Time";
            public const string 俄罗斯标准时间 = "Russian Standard Time";
            public const string 伊朗标准时间 = "Iran Standard Time";
            public const string 高加索标准时间 = "Caucasus Standard Time";
            public const string 阿拉伯半岛标准时间 = "Arabian Standard Time";
            public const string 阿富汗标准时间 = "Afghanistan Standard Time";
            public const string 西亚标准时间 = "West Asia Standard Time";
            public const string 叶卡捷琳堡标准时间 = "Ekaterinburg Standard Time";
            public const string 印度标准时间 = "India Standard Time";
            public const string 尼泊尔标准时间 = "Nepal Standard Time";
            public const string 斯里兰卡标准时间 = "Sri Lanka Standard Time";
            public const string 中亚北部标准时间 = "N. Central Asia Standard Time";
            public const string 中亚标准时间 = "Central Asia Standard Time";
            public const string 缅甸标准时间 = "Myanmar Standard Time";
            public const string 北亚标准时间 = "North Asia Standard Time";
            public const string 东南亚标准时间 = "SE Asia Standard Time";
            public const string 北亚东部标准时间 = "North Asia East Standard Time";
            public const string 中国标准时间 = "China Standard Time";
            public const string 台北标准时间 = "Taipei Standard Time";
            public const string 马来西亚半岛标准时间 = "Singapore Standard Time";
            public const string 澳大利亚西部标准时间 = "W. Australia Standard Time";
            public const string 东京标准时间 = "Tokyo Standard Time";
            public const string 韩国标准时间 = "Korea Standard Time";
            public const string 雅库茨克标准时间 = "Yakutsk Standard Time";
            public const string 澳大利亚中部标准时间1 = "AUS Central Standard Time";
            public const string 澳大利亚中部标准时间2 = "Cen. Australia Standard Time";
            public const string 西太平洋标准时间 = "West Pacific Standard Time";
            public const string 澳大利亚东部标准时间1 = "AUS Eastern Standard Time";
            public const string 澳大利亚东部标准时间2 = "E. Australia Standard Time";
            public const string 符拉迪沃斯托克标准时间 = "Vladivostok Standard Time";
            public const string 塔斯马尼亚岛标准时间 = "Tasmania Standard Time";
            public const string 太平洋中部标准时间 = "Central Pacific Standard Time";
            public const string 新西兰标准时间 = "New Zealand Standard Time";
            public const string 斐济标准时间 = "Fiji Standard Time";
            public const string 汤加标准时间 = "Tonga Standard Time";
        }
    }
}
