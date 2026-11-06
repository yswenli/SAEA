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
*命名空间：SAEA.Common
*文件名： AssemblyHelper
*版本号： v26.4.23.1
*唯一标识：ab68d003-44ab-4ca2-ade8-e73fcf6ed44f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/08/24 16:31:11
*描述：AssemblyHelper帮助类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common.IO;
using System;
using System.Reflection;

namespace SAEA.Common
{
    /// <summary>
    /// 程序集加载、类型枚举与实例创建
    /// </summary>
    public static class AssemblyHelper
    {
        static Assembly _assembly = null;

        /// <summary>
        /// 加载dll文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static Assembly LoadAssembly(string fileName)
        {
            try
            {
                _assembly = Assembly.LoadFile(PathHelper.GetFullName(fileName + ".exe"));
            }
            catch { }
            if (_assembly == null)
            {
                _assembly = Assembly.LoadFile(PathHelper.GetFullName(fileName + ".dll"));
            }
            return _assembly;
        }


        /// <summary>
        /// 获取当前正在执行的程序集中的全部类型
        /// </summary>
        /// <returns>当前执行程序集的类型数组</returns>
        public static Type[] GetTypes()
        {
            return Assembly.GetExecutingAssembly().GetTypes();
        }

        /// <summary>
        /// 使用无参构造函数创建类型实例
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <returns>新建的实例</returns>
        public static object CreateInstance(Type type)
        {
            return Activator.CreateInstance(type);
        }

        /// <summary>
        /// 使用给定参数匹配的构造函数创建类型实例
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="args">构造函数参数，可为空数组以调用无参构造</param>
        /// <returns>新建的实例</returns>
        public static object CreateInstance(Type type, params object[] args)
        {
            return Activator.CreateInstance(type, args);
        }


        /// <summary>
        /// 获取调用方的程序集
        /// </summary>
        public static Assembly Current
        {
            get { return Assembly.GetCallingAssembly(); }
        }



    }
}