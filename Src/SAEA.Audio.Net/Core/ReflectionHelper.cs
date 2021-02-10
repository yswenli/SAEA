/****************************************************************************
*项目名称：SAEA.Audio.GAudio
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio.GAudio
*类 名 称：ReflectionHelper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/27 15:01:13
*描述：
*=====================================================================
*修改时间：2021/1/27 15:01:13
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.Audio.GAudio
{
    static class ReflectionHelper
    {
        public static IEnumerable<T> CreateAllInstancesOf<T>()
        {
            return typeof(ReflectionHelper).Assembly.GetTypes()
                .Where(t => typeof(T).IsAssignableFrom(t))
                .Where(t => !t.IsAbstract && t.IsClass)
                .Select(t => (T)Activator.CreateInstance(t));
        }
    }
}
