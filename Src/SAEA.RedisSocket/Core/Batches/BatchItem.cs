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
*命名空间：SAEA.RedisSocket.Core.Batches
*文件名： BatchItem
*版本号： v26.4.23.1
*唯一标识：174c618d-76ab-4041-b932-dc0cce3c00e8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/07/24 14:55:42
*描述：BatchItem接口
*
*=====================================================================
*修改标记
*修改时间：2020/07/24 14:55:42
*修改人： yswenli
*版本号： v26.4.23.1
*描述：BatchItem接口
*
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Core.Batches
{
    /// <summary>
    /// 批量数据项
    /// </summary>
    class BatchItem
    {
        /// <summary>
        /// 请求值
        /// </summary>
        public RequestType RequestType
        {
            get; set;
        }
        /// <summary>
        /// 请求参数
        /// </summary>
        public string Cmd
        {
            get;set;
        }

        /// <summary>
        /// 批量数据项
        /// </summary>
        /// <param name="requestType"></param>
        /// <param name="cmd"></param>
        public BatchItem(RequestType requestType, string cmd)
        {
            RequestType = requestType;
            Cmd = cmd;
        }
    }
}
