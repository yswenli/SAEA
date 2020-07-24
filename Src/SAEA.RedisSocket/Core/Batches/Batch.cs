/****************************************************************************
*项目名称：SAEA.RedisSocket.Core.Batches
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core.Batches
*类 名 称：Batch
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/7/23 17:27:12
*描述：
*=====================================================================
*修改时间：2020/7/23 17:27:12
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Core.Batches
{
    /// <summary>
    /// Redis批量操作类
    /// </summary>
    public class Batch
    {
        RedisDataBase _redisDataBase;

        Dictionary<int, RequestType> _dic;

        internal Batch(RedisDataBase redisDataBase)
        {
            _redisDataBase = redisDataBase;

            _dic = new Dictionary<int, RequestType>();
        }




    }
}
