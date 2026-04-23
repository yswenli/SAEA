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
*修改时间：2018/03/20 10:04:58
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ResponseData类
*
*****************************************************************************/
using SAEA.RedisSocket.Interface;

namespace SAEA.RedisSocket.Model
{
    public class ResponseData<T> : IResult
    {
        public ResponseType Type
        {
            get; set;
        }

        string _data = string.Empty;

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

        public T Entity
        {
            get; set;
        }
    }
}