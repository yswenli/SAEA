/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
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
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common
{
    /// <summary>
    /// 时间工具类
    /// </summary>
    public static class DateTimeHelper
    {

        static DateTimeHelper()
        {
            try
            {
                Console.WriteLine(SAEAVersion.ConsoleTitle);
            }
            catch { }
        }

        public static DateTime Now
        {
            get
            {
                return DateTime.Now;
            }
        }

        public static DateTime Today
        {
            get
            {
                return DateTime.Today;
            }
        }

        /// <summary>
        /// utc时间
        /// </summary>
        public static DateTime UtcNow
        {
            get
            {
                return TimeZoneInfo.ConvertTimeToUtc(Now, TimeZoneInfo.Local);
            }
        }
        /// <summary>
        /// 根据时区获取时间,
        /// DateTimeZoneID.中国标准时间
        /// </summary>
        /// <param name="zoneId"></param>
        /// <returns></returns>
        public static DateTime GetDateTimeByZone(string zoneId)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
        }

        /// <summary>
        /// 不同时区的日期转换，
        /// DateTimeZoneID.中国标准时间
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="fromZoneId"></param>
        /// <param name="toZoneId"></param>
        /// <returns></returns>
        public static DateTime ConvertToOtherZone(this DateTime dt, string fromZoneId, string toZoneId)
        {
            return TimeZoneInfo.ConvertTime(dt, TimeZoneInfo.FindSystemTimeZoneById(fromZoneId), TimeZoneInfo.FindSystemTimeZoneById(toZoneId));
        }


        public static string ToString(string format = "yyyy-MM-dd HH:mm:ss.fff")
        {
            return Now.ToString(format, CultureInfo.InvariantCulture);
        }

        public static string ToFString(this DateTime dt, string format = "yyyy-MM-dd HH:mm:ss.fff")
        {
            return dt.ToString(format, CultureInfo.InvariantCulture);
        }

        public static string ToGMTString(this DateTime dt)
        {
            return dt.ToString("r", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 获取unix 时间戳
        /// </summary>
        /// <returns></returns>
        public static int GetUnixTick()
        {
            TimeSpan ts = DateTimeHelper.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime();
            return Convert.ToInt32(ts.TotalSeconds);
        }

        /// <summary>
        /// 获取unix 时间戳
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static int ToUnixTick(this DateTime dateTime)
        {
            TimeSpan ts = dateTime - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime();
            return Convert.ToInt32(ts.TotalSeconds);
        }

        /// <summary>
        /// 将Unix时间戳转换成DateTime
        /// </summary>
        /// <param name="unixTick"></param>
        /// <returns></returns>
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
            /// <summary>
            /// GMT-12:00
            /// </summary>
            public const string 日界线西国际日期变更线标准时间 = "Dateline Standard Time";
            /// <summary>
            /// GMT-11:00
            /// </summary>
            public const string 中途岛萨摩亚群岛萨摩亚群岛标准时间 = "Samoa Standard Time";
            /// <summary>
            /// GMT-10:00
            /// </summary>
            public const string 夏威夷标准时间 = "Hawaiian Standard Time";
            /// <summary>
            /// GMT-09:00
            /// </summary>
            public const string 阿拉斯加标准时间 = "Alaskan Standard Time";
            /// <summary>
            /// GMT-08:00,
            /// 美国和加拿大
            /// </summary>
            public const string 蒂华纳太平洋标准时间 = "Pacific Standard Time";
            /// <summary>
            /// GMT-07:00
            /// </summary>
            public const string 亚利桑那美国山地标准时间 = "US Mountain Standard Time";
            /// <summary>
            /// GMT-07:00,
            /// 山地时间(美国和加拿大),山地标准时间
            /// </summary>
            public const string 山地标准时间 = "Mountain Standard Time";
            /// <summary>
            /// GMT-07:00,
            /// 奇瓦瓦，拉巴斯，马扎特兰,墨西哥标准时间 2
            /// </summary>
            public const string 墨西哥标准时间2 = "Mexico Standard Time 2";
            /// <summary>
            /// GMT-06:00
            /// </summary>
            public const string 中美洲标准时间 = "Central America Standard Time";
            /// <summary>
            /// GMT-06:00,
            /// 美国和加拿大中部标准时间
            /// </summary>
            public const string 中部标准时间 = "Central Standard Time";
            /// <summary>
            /// GMT-06:00
            /// </summary>
            public const string 萨斯喀彻温加拿大中部标准时间 = "Canada Central Standard Time";
            /// <summary>
            /// GMT-06:00,
            /// 瓜达拉哈拉，墨西哥城，蒙特雷,墨西哥标准时间
            /// </summary>
            public const string 墨西哥标准时间 = "Mexico Standard Time";
            /// <summary>
            /// GMT-05:00,
            /// 东部时间(美国和加拿大),东部标准时间
            /// </summary>
            public const string 东部标准时间 = "Eastern Standard Time";
            /// <summary>
            /// GMT-05:00,
            /// 印地安那州(东部),美国东部标准时间
            /// </summary>
            public const string 美国东部标准时间 = "US Eastern Standard Time";
            /// <summary>
            /// GMT-05:00,
            /// 波哥大，利马，基多,南美州太平洋标准时间
            /// </summary>
            public const string 南美州太平洋标准时间 = "SA Pacific Standard Time";
            /// <summary>
            /// GMT-04:00,
            /// 加拉加斯，拉巴斯,南美州西部标准时间
            /// </summary>
            public const string 南美州西部标准时间 = "SA Western Standard Time";
            /// <summary>
            /// GMT-04:00,
            /// 圣地亚哥,南美州太平洋标准时间
            /// </summary>
            public const string 圣地亚哥南美州太平洋标准时间 = "Pacific SA Standard Time";
            /// <summary>
            /// GMT-04:00,
            /// 大西洋时间(加拿大),大西洋标准时间
            /// </summary>
            public const string 大西洋标准时间 = "Atlantic Standard Time";
            /// <summary>
            /// GMT-03:30
            /// </summary>
            public const string 纽芬兰标准时间 = "Newfoundland Standard Time";
            /// <summary>
            /// GMT-03:00,
            /// 巴西利亚,南美州东部标准时间
            /// </summary>
            public const string 巴西利亚南美州东部标准时间 = "E. South America Standard Time";
            /// <summary>
            /// GMT-03:00,
            /// 布宜诺斯艾利斯，乔治敦,南美州东部标准时间
            /// </summary>
            public const string 南美州东部标准时间 = "SA Eastern Standard Time";
            /// <summary>
            /// GMT-03:00,
            /// 格陵兰东部标准时间
            /// </summary>
            public const string 格陵兰东部标准时间 = "Greenland Standard Time";
            /// <summary>
            /// GMT-02:00
            /// </summary>
            public const string 中大西洋标准时间 = "Mid-Atlantic Standard Time";
            /// <summary>
            /// GMT-01:00
            /// </summary>
            public const string 亚速尔群岛标准时间 = "Azores Standard Time";
            /// <summary>
            /// GMT-01:00
            /// </summary>
            public const string 佛得角群岛标准时间 = "Cape Verde Standard Time";
            /// <summary>
            /// 卡萨布兰卡，蒙罗维亚,格林威治标准时间
            /// </summary>
            public const string 格林威治标准时间 = "Greenwich Standard Time";
            /// <summary>
            /// 格林威治标准时间: 都柏林, 爱丁堡, 伦敦, 里斯本,格林威治标准时间
            /// </summary>
            public const string 伦敦都柏林里斯本格林威治标准时间 = "GMT Standard Time";
            /// <summary>
            /// GMT+01:00
            /// </summary>
            public const string 中非西部标准时间 = "W. Central Africa Standard Time";
            /// <summary>
            /// GMT+01:00,
            /// 布鲁塞尔，哥本哈根，马德里，巴黎,罗马标准时间
            /// </summary>
            public const string 罗马标准时间 = "Romance Standard Time";
            /// <summary>
            /// GMT+01:00,
            /// 萨拉热窝，斯科普里，华沙，萨格勒布,中欧标准时间
            /// </summary>
            public const string 中欧标准时间1 = "Central European Standard Time";
            /// <summary>
            /// GMT+01:00,
            /// 贝尔格莱德，布拉迪斯拉发，布达佩斯，卢布尔雅那，布拉格,中欧标准时间
            /// </summary>
            public const string 中欧标准时间2 = "Central Europe Standard Time";
            /// <summary>
            /// GMT+01:00,
            /// 阿姆斯特丹，柏林，伯尔尼，罗马，斯德哥尔摩，维也纳,西欧标准时间
            /// </summary>
            public const string 西欧标准时间 = "W. Europe Standard Time";
            /// <summary>
            /// GMT+02:00,
            /// 哈拉雷，比勒陀利亚,南非标准时间
            /// </summary>
            public const string 南非标准时间 = "South Africa Standard Time";
            /// <summary>
            /// GMT+02:00,
            /// 布加勒斯特,东欧标准时间
            /// </summary>
            public const string 东欧标准时间 = "E. Europe Standard Time";
            /// <summary>
            /// GMT+02:00,
            /// 开罗,埃及标准时间
            /// </summary>
            public const string 埃及标准时间 = "Egypt Standard Time";
            /// <summary>
            /// GMT+02:00,
            /// 耶路撒冷标准时间
            /// </summary>
            public const string 耶路撒冷标准时间 = "Israel Standard Time";
            /// <summary>
            /// GMT+02:00,
            /// 赫尔辛基，基辅，里加，索非亚，塔林，维尔纽斯,FLE 标准时间
            /// </summary>
            public const string FLE标准时间 = "FLE Standard Time";
            /// <summary>
            /// GMT+02:00,
            /// 雅典，贝鲁特，伊斯坦布尔，明斯克,GTB 标准时间
            /// </summary>
            public const string GTB标准时间 = "GTB Standard Time";
            /// <summary>
            /// GMT+03:00,
            /// 内罗毕,东非标准时间
            /// </summary>
            public const string 东非标准时间 = "E. Africa Standard Time";
            /// <summary>
            /// GMT+03:00,
            /// 巴格达,阿拉伯标准时间
            /// </summary>
            public const string 阿拉伯标准时间1 = "Arabic Standard Time";
            /// <summary>
            /// GMT+03:00,
            /// 科威特，利雅得,阿拉伯标准时间
            /// </summary>
            public const string 阿拉伯标准时间2 = "Arab Standard Time";
            /// <summary>
            /// GMT+03:00,
            /// 莫斯科，圣彼得堡, 伏尔加格勒,俄罗斯标准时间
            /// </summary>
            public const string 俄罗斯标准时间 = "Russian Standard Time";
            /// <summary>
            /// GMT+03:30,
            /// 德黑兰,伊朗标准时间
            /// </summary>
            public const string 伊朗标准时间 = "Iran Standard Time";
            /// <summary>
            /// GMT+04:00,
            /// 巴库，第比利斯，埃里温,高加索标准时间
            /// </summary>
            public const string 高加索标准时间 = "Caucasus Standard Time";
            /// <summary>
            /// GMT+04:00,
            /// 阿布扎比，马斯喀特,阿拉伯半岛标准时间
            /// </summary>
            public const string 阿拉伯半岛标准时间 = "Arabian Standard Time";
            /// <summary>
            /// GMT+04:30,
            /// 喀布尔,阿富汗标准时间
            /// </summary>
            public const string 阿富汗标准时间 = "Afghanistan Standard Time";
            /// <summary>
            /// GMT+05:00,
            /// 伊斯兰堡，卡拉奇，塔什干,西亚标准时间
            /// </summary>
            public const string 西亚标准时间 = "West Asia Standard Time";
            /// <summary>
            /// GMT+05:00
            /// </summary>
            public const string 叶卡捷琳堡标准时间 = "Ekaterinburg Standard Time";
            /// <summary>
            /// GMT+05:30
            /// 马德拉斯，加尔各答，孟买，新德里,印度标准时间
            /// </summary>
            public const string 印度标准时间 = "India Standard Time";
            /// <summary>
            /// GMT+05:45,
            /// 尼泊尔标准时间
            /// </summary>
            public const string 尼泊尔标准时间 = "Nepal Standard Time";
            /// <summary>
            /// GMT+06:00
            /// </summary>
            public const string 斯里兰卡标准时间 = "Sri Lanka Standard Time";
            /// <summary>
            /// GMT+06:00
            /// 阿拉木图，新西伯利亚,中亚北部标准时间
            /// </summary>
            public const string 中亚北部标准时间 = "N. Central Asia Standard Time";
            /// <summary>
            /// GMT+06:00,
            /// 阿斯塔纳，达卡,中亚标准时间
            /// </summary>
            public const string 中亚标准时间 = "Central Asia Standard Time";
            /// <summary>
            /// GMT+06:30
            /// </summary>
            public const string 缅甸标准时间 = "Myanmar Standard Time";
            /// <summary>
            /// GMT+07:00
            /// </summary>
            public const string 北亚标准时间 = "North Asia Standard Time";
            /// <summary>
            /// GMT+07:00
            /// </summary>
            public const string 东南亚标准时间 = "SE Asia Standard Time";
            /// <summary>
            /// GMT+08:00
            /// </summary>
            public const string 北亚东部标准时间 = "North Asia East Standard Time";
            /// <summary>
            /// GMT+08:00,
            /// 北京，重庆，香港特别行政区，乌鲁木齐,中国标准时间
            /// </summary>
            public const string 中国标准时间 = "China Standard Time";
            /// <summary>
            /// GMT+08:00
            /// </summary>
            public const string 台北标准时间 = "Taipei Standard Time";
            /// <summary>
            /// GMT+08:00,
            /// 吉隆坡，新加坡,马来西亚半岛标准时间
            /// </summary>
            public const string 马来西亚半岛标准时间 = "Singapore Standard Time";
            /// <summary>
            /// GMT+08:00
            /// </summary>
            public const string 澳大利亚西部标准时间 = "W. Australia Standard Time";
            /// <summary>
            /// GMT+09:00,
            /// 大坂，札幌，东京,东京标准时间
            /// </summary>
            public const string 东京标准时间 = "Tokyo Standard Time";
            /// <summary>
            /// GMT+09:00,
            /// 汉城,韩国标准时间
            /// </summary>
            public const string 韩国标准时间 = "Korea Standard Time";
            /// <summary>
            /// GMT+09:00
            /// </summary>
            public const string 雅库茨克标准时间 = "Yakutsk Standard Time";
            /// <summary>
            /// GMT+09:30,
            /// 达尔文,澳大利亚中部标准时间
            /// </summary>
            public const string 澳大利亚中部标准时间1 = "AUS Central Standard Time";
            /// <summary>
            /// GMT+09:30,
            /// 阿德莱德,澳大利亚中部标准时间
            /// </summary>
            public const string 澳大利亚中部标准时间2 = "Cen. Australia Standard Time";
            /// <summary>
            /// GMT+10:00,
            /// 关岛，莫尔兹比港,西太平洋标准时间
            /// </summary>
            public const string 西太平洋标准时间 = "West Pacific Standard Time";
            /// <summary>
            /// GMT+10:00,
            /// 堪培拉，墨尔本，悉尼,澳大利亚东部标准时间
            /// </summary>
            public const string 澳大利亚东部标准时间1 = "AUS Eastern Standard Time";
            /// <summary>
            /// GMT+10:00,
            /// 布里斯班,澳大利亚东部标准时间
            /// </summary>
            public const string 澳大利亚东部标准时间2 = "E. Australia Standard Time";
            /// <summary>
            /// GMT+10:00
            /// </summary>
            public const string 符拉迪沃斯托克标准时间 = "Vladivostok Standard Time";
            /// <summary>
            /// GMT+10:00
            /// </summary>
            public const string 塔斯马尼亚岛标准时间 = "Tasmania Standard Time";
            /// <summary>
            /// GMT+11:00,
            /// 马加丹，索罗门群岛，新喀里多尼亚,太平洋中部标准时间
            /// </summary>
            public const string 太平洋中部标准时间 = "Central Pacific Standard Time";
            /// <summary>
            /// GMT+12:00,
            /// 奥克兰，惠灵顿,新西兰标准时间
            /// </summary>
            public const string 新西兰标准时间 = "New Zealand Standard Time";
            /// <summary>
            /// GMT+12:00
            /// </summary>
            public const string 斐济标准时间 = "Fiji Standard Time";
            /// <summary>
            /// GMT+13:00
            /// </summary>
            public const string 汤加标准时间 = "Tonga Standard Time";
        }
    }
}
