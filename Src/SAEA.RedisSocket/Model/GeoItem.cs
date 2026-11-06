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
*唯一标识：54df8c7f-3039-42e2-b999-9bf989936d63
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/16 18:02:29
*描述：GeoItem接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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

    /// <summary>
    /// 地理位置成员，包含名称与经纬度
    /// </summary>
    public class GeoItem
    {
        /// <summary>
        /// 成员名称
        /// </summary>
        public string Name
        {
            get; set;
        }

        /// <summary>
        /// 经度
        /// </summary>
        public double Lng
        {
            get; set;
        }

        /// <summary>
        /// 纬度
        /// </summary>
        public double Lat
        {
            get; set;
        }

        /// <summary>
        /// 把多个成员展开为 GEO 命令参数，每个成员按经度、纬度、名称顺序输出
        /// </summary>
        /// <param name="arr">地理位置成员数组</param>
        /// <returns>经度、纬度、名称依次排列的参数列表</returns>
        public static List<string> ToParams(GeoItem[] arr)
        {
            var result = new List<string>();
            foreach (var item in arr)
            {
                result.AddRange(ToParams(item));
            }
            return result;
        }

        /// <summary>
        /// 把单个成员转换为 GEO 命令参数
        /// </summary>
        /// <param name="item">地理位置成员</param>
        /// <returns>依次为经度、纬度、名称的参数列表</returns>
        public static List<string> ToParams(GeoItem item)
        {
            var result = new List<string>();
            result.Add(item.Lng.ToString());
            result.Add(item.Lat.ToString());
            result.Add(item.Name);
            return result;
        }
    }

    /// <summary>
    /// GEO POS 返回的经纬度坐标
    /// </summary>
    public class GeoNum
    {
        /// <summary>
        /// 经度
        /// </summary>
        public double Lng { get; set; }

        /// <summary>
        /// 纬度
        /// </summary>
        public double Lat { get; set; }
    }


    /// <summary>
    /// 带距离的地理位置信息
    /// </summary>
    public class GeoDistInfo : GeoItem
    {
        /// <summary>
        /// 与中心的距离，单位由查询时的 GeoUnit 决定
        /// </summary>
        public double Dist { get; set; }
    }
}
