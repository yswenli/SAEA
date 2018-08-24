/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： Class1
*版本号： V1.0.0.0
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.Common
{
    public static class ConsoleHelper
    {
        public static string Title
        {
            get => Console.Title;
            set => Console.Title = value;
        }

        public static void WriteLine(string str)
        {
            var writeStr = DateTimeHelper.ToString() + "  " + str;
            Console.WriteLine(writeStr);
        }

        public static void WriteLine(string str, params object[] args)
        {
            var writeStr = DateTimeHelper.ToString() + "  " + str;
            Console.WriteLine(writeStr, args);
        }

        public static void WriteLine(string str, ConsoleColor color, params object[] args)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            var writeStr = DateTimeHelper.ToString() + "  " + str;
            Console.WriteLine(writeStr, args);
            Console.ForegroundColor = oldColor;
        }

        public static string ReadLine()
        {
            return Console.ReadLine();
        }
    }
}
