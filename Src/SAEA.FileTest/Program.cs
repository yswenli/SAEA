/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.SAEA.FileTest
*文件名： Class1
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Common.IO;
using SAEA.FileSocket;
using System;

namespace SAEA.FileTest
{
    class Program
    {
        static FileTransfer _fileTransfer;

        static string filePath = PathHelper.GetCurrentPath();

        static void Main(string[] args)
        {
            _fileTransfer = new FileTransfer(filePath);

            _fileTransfer.OnReceiveEnd += _fileTransfer_OnReceiveEnd;

            _fileTransfer.OnDisplay += _fileTransfer_OnDisplay;

            ConsoleHelper.WriteLine("FileTransfer 已启动，发送文件格式如下： send [ip] [fileName]");

            do
            {
                var input = ConsoleHelper.ReadLine();

                try
                {
                    if (StringHelper.Substring(input, 0, 4) == "send")
                    {
                        var arr = input.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                        _fileTransfer.SendFile(arr[2], arr[1]);
                    }
                    else
                        throw new Exception("未知的命令");
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine("FileTransfer 异常：{0}", ex.Message);
                }
            }
            while (true);
        }

        private static void _fileTransfer_OnReceiveEnd(string obj)
        {
            ConsoleHelper.WriteLine("文件接收完毕 {0}", obj);
        }

        private static void _fileTransfer_OnDisplay(string obj)
        {
            ConsoleHelper.WriteLine(obj);
        }
    }
}
