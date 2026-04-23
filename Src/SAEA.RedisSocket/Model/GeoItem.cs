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
*命名空间：SAEA.RedisSocket.Model
*文件名： GeoItem
*版本号： v26.4.23.1
*唯一标识：726185b0-37bf-4ef7-abed-15d16cd118e5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/16 18:02:29
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/08/16 18:02:29
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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
