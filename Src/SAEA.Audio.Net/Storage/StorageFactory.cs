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
*命名空间：SAEA.Audio.Storage
*文件名： StorageFactory
*版本号： v26.4.23.1
*唯一标识：0817040d-3968-43d8-b5f1-1120c87c35ac
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/21 16:29:07
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/02/21 16:29:07
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Audio.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Audio.Storage
{
    /// <summary>
    /// 存储工厂类
    /// </summary>
    public static class StorageFactory
    {
        /// <summary>
        /// 创建存储实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IStorage Create<T>() where T : IStorage
        {
            var t = (IStorage)Activator.CreateInstance<T>();
            return t;
        }
    }
}
