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
*文件名： ResponseData
*版本号： v26.4.23.1
*唯一标识：f21a681b-c9d3-43af-9428-e3e686d01ea1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/20 10:04:58
*描述：ResponseData类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.RedisSocket.Interface;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// 单次命令的应答结果
    /// </summary>
    /// <typeparam name="T">应答实体类型</typeparam>
    public class ResponseData<T> : IResult
    {
        /// <summary>
        /// 应答类型，用于区分成功、错误、空结果等情形
        /// </summary>
        public ResponseType Type
        {
            get; set;
        }

        string _data = string.Empty;

        /// <summary>
        /// 应答的原始文本，读取时已去除末尾空白，赋值 null 后读取会抛空引用异常
        /// </summary>
        public string Data
        {
            get
            {
                return _data.TrimEnd();
            }
            set
            {
                _data = value;
            }
        }

        /// <summary>
        /// 由应答文本转换出的强类型实体
        /// </summary>
        public T Entity
        {
            get; set;
        }
    }
}