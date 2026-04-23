/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Model
*文件名： ScanResponse
*版本号： v26.4.23.1
*唯一标识：bc48708f-e1e1-4b9e-be22-0cba54211c76
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/7/23 20:52:12
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/7/23 20:52:12
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.RedisSocket.Interface;
using System.Collections.Generic;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// Scan返回值
    /// </summary>
    public class ScanResponse: IResult
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
        public List<string> Data
        {
            get; set;
        }
    }
}