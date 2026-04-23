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
*文件名： ZScanResponse
*版本号： v26.4.23.1
*唯一标识：cf398c8a-d2f9-4cb9-b22b-363ff53d06e2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/07/24 14:03:20
*描述：ZScanResponse类
*
*=====================================================================
*修改标记
*修改时间：2018/07/24 14:03:20
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ZScanResponse类
*
*****************************************************************************/
using System.Collections.Generic;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// HScan返回值
    /// </summary>
    public class ZScanResponse
    {
        /// <summary>
        /// 游标参数
        /// </summary>
        public int Offset
        {
            get; set;
        }
        /// <summary>
        /// 返回数据
        /// </summary>
        public List<ZItem> Data
        {
            get; set;
        }
    }

    public class ZItem
    {
        public string Value
        {
            get; set;
        }
        public double Score
        {
            get; set;
        }
    }
}