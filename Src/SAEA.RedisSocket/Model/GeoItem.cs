/****************************************************************************
*项目名称：SAEA.RedisSocket.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Model
*类 名 称：GeoItem
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/16 17:30:40
*描述：
*=====================================================================
*修改时间：2019/8/16 17:30:40
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/

using System.Collections.Generic;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// 单位的参数 unit 必须是以下单位的其中一个
    /// </summary>
    public enum GeoUnit
    {
        /// <summary>
        /// 表示单位为米
        /// </summary>
        m,
        /// <summary>
        /// 表示单位为千米
        /// </summary>
        km,
        /// <summary>
        /// 表示单位为英里
        /// </summary>
        mi,
        /// <summary>
        /// 表示单位为英尺
        /// </summary>
        ft
    }

    public class GeoItem
    {
        public string Name
        {
            get; set;
        }

        public double Lng
        {
            get; set;
        }

        public double Lat
        {
            get; set;
        }

        public static List<string> ToParams(GeoItem[] arr)
        {
            var result = new List<string>();
            foreach (var item in arr)
            {
                result.AddRange(ToParams(item));
            }
            return result;
        }

        public static List<string> ToParams(GeoItem item)
        {
            var result = new List<string>();
            result.Add(item.Lng.ToString());
            result.Add(item.Lat.ToString());
            result.Add(item.Name);
            return result;
        }
    }

    public class GeoNum
    {
        public double Lng { get; set; }

        public double Lat { get; set; }
    }


    public class GeoDistInfo : GeoItem
    {
        public double Dist { get; set; }
    }
}
