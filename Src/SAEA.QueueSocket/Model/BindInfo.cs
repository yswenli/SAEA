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
*命名空间：SAEA.QueueSocket.Model
*文件名： BindInfo
*版本号： v26.4.23.1
*唯一标识：590ede90-8229-4a49-8490-ef6e1b977bca
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：BindInfo接口
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：BindInfo接口
*
*****************************************************************************/
using System;

namespace SAEA.QueueSocket.Model
{
    class BindInfo
    {
        public string Name
        {
            get; set;
        }
        public string SessionID
        {
            get; set;
        }
        public string Topic
        {
            get; set;
        }
        public bool Flag
        {
            get; set;
        }
    }
}