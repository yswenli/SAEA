using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace SAEA.RPC.Common
{
    /// <summary>
    /// 程序集获取方法
    /// 因为.net framework的
    /// </summary>
    public static class TypeHelper
    {

        static StackTrace _stackTrace = new StackTrace(true);

        /// <summary>
        /// 因为.net framework的环境不同
        /// </summary>
        /// <returns></returns>
        public static Type[] GetDefalt()
        {
            var frames = _stackTrace.GetFrames();

            MethodBase[] mbs = new MethodBase[frames.Length];

            for (int i = 0; i < frames.Length; i++)
            {
                mbs[i] = frames[i].GetMethod();
                if (mbs[i].Name.Equals("Generate") || mbs[i].Name.Equals("Start"))
                {
                    return frames[i + 1].GetMethod().DeclaringType.Assembly.GetTypes();
                }
            }

            return null;
        }

        /// <summary>
        /// 获取类型名称
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetTypeName(Type type)
        {
            if (type.IsClass)
            {
                if (type.IsGenericType)
                {
                    var tName = type.Name.Substring(0, type.Name.Length - 2) + "<";

                    var gArgs = type.GetGenericArguments();

                    for (int i = 0; i < gArgs.Length; i++)
                    {
                        if (i == gArgs.Length - 1)
                        {
                            tName += GetTypeName(gArgs[i]);
                        }
                        else
                        {
                            tName += GetTypeName(gArgs[i]) + ",";
                        }
                    }
                    tName += ">";
                    return tName;
                }
            }
            return type.Name;
        }


    }
}
