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
*文件名： RedisParamsNullException
*版本号： v26.4.23.1
*唯一标识：4192dedb-11a9-460a-829d-920d1818ed00
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/12/27 20:43:44
*描述：RedisParamsNullException接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// redis 连接已断开时抛出的异常
    /// </summary>
    public class RedisIOException : Exception
    {
        /// <summary>
        /// 使用默认提示信息 redis disconnected! 初始化
        /// </summary>
        public RedisIOException() : this("redis disconnected!")
        {

        }

        /// <summary>
        /// 使用指定提示信息初始化
        /// </summary>
        /// <param name="message">异常提示信息</param>
        public RedisIOException(string message) : base(message)
        {

        }
    }

    /// <summary>
    /// 命令入参为 null 时抛出的异常
    /// </summary>
    public class RedisParamsNullException : Exception
    {
        /// <summary>
        /// 使用默认提示信息 redis 输入参数不能为null! 初始化
        /// </summary>
        public RedisParamsNullException() : this("redis 输入参数不能为null!")
        {

        }

        /// <summary>
        /// 使用指定提示信息初始化
        /// </summary>
        /// <param name="message">异常提示信息</param>
        public RedisParamsNullException(string message) : base(message)
        {

        }
    }
}