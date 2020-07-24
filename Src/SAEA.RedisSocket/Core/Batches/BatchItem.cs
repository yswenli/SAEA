/****************************************************************************
*项目名称：SAEA.RedisSocket.Core.Batches
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core.Batches
*类 名 称：BatchItem
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/7/24 13:22:45
*描述：
*=====================================================================
*修改时间：2020/7/24 13:22:45
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
