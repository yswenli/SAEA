/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： AssemblyHelper
*版本号： V1.0.0.0
*唯一标识：bf3043aa-a84d-42ab-a6b6-b3adf2ab8925
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 16:53:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 16:53:26
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

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
            return System.Activator.CreateInstance(type);
        }

        public static object CreateInstance(Type type, params object[] args)
        {
            return System.Activator.CreateInstance(type, args);
        }


        public static Assembly Current
        {
            get { return Assembly.GetCallingAssembly(); }
        }



    }
}
