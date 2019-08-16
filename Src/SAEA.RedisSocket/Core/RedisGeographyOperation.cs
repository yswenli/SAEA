/****************************************************************************
*项目名称：SAEA.RedisSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core
*类 名 称：RedisGeographyOperation
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/16 16:26:43
*描述：
*=====================================================================
*修改时间：2019/8/16 16:26:43
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// Geography
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 将给定的空间元素（纬度、经度、名字）添加到指定的键里面
        /// </summary>
        /// <param name="key"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        public int GeoAdd(string key, params GeoItem[] items)
        {
            var result = -1;

            if (items == null || !items.Any())
            {
                throw new Exception("params must not allow null");
            }

            var list = new List<string>();
            list.Add(key);
            list.AddRange(GeoItem.ToParams(items));

            int.TryParse(_cnn.DoWithMutiParams(RequestType.GEOADD, list.ToArray()).Data, out result);

            return result;
        }

    }
}
