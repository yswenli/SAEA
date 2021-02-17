/****************************************************************************
*项目名称：SAEA.Audio
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio.Storage
*类 名 称：StorageFactory
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/5 15:43:29
*描述：
*=====================================================================
*修改时间：2019/11/5 15:43:29
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
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
