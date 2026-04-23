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
*文件名： ResponseType
*版本号： v26.4.23.1
*唯一标识：e7466a5e-9236-4b15-bcf5-35fac9ee8cb9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/20 10:04:58
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/03/20 10:04:58
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Model
{
    public enum ResponseType : byte
    {
        OK = 1,
        Error = 2,
        Value = 3,
        Empty = 4,
        String = 5,
        Lines = 6,
        KeyValues = 7,
        Sub = 8,
        UnSub = 9,
        Undefined = 10,
        Redirect = 11,
    }
}