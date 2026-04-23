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
*修改时间：2018/08/24 16:31:11
*修改人： yswenli
*版本号： v26.4.23.1
*描述：AssemblyHelper帮助类
*
*****************************************************************************/
using SAEA.Common.IO;
using System;
using System.Reflection;

namespace SAEA.Common
{
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


        public static Type[] GetTypes()
        {
            return Assembly.GetExecutingAssembly().GetTypes();
        }

        public static object CreateInstance(Type type)
        {
            return Activator.CreateInstance(type);
        }

        public static object CreateInstance(Type type, params object[] args)
        {
            return Activator.CreateInstance(type, args);
        }


        public static Assembly Current
        {
            get { return Assembly.GetCallingAssembly(); }
        }



    }
}