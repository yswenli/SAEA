/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Http.Net
*文件名： HCoder
*版本号： V1.0.0.0
*唯一标识：1c78283d-e311-4d8d-b781-395253c9454c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 17:19:30
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 17:19:30
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.WebAPI.Http.Net
{
    class HCoder : ICoder
    {
        private static string ENDSTR = "\r\n\r\n";

        StringBuilder _result = new StringBuilder();

        object _locker = new object();

        public void Pack(byte[] data, Action<DateTime> onHeart, Action<ISocketProtocal> onUnPackage, Action<byte[]> onFile)
        {

        }

        public void GetRequest(byte[] data, Action<string> onUnpackage)
        {
            lock (_locker)
            {
                var str = Encoding.UTF8.GetString(data);

                var index = str.IndexOf(ENDSTR);

                if (index > -1)
                {
                    var s = str.Substring(0, index);

                    _result.Append(s);

                    onUnpackage.Invoke(_result.ToString());

                    _result.Clear();

                    if (str.Length > index + 4)
                    {
                        _result.Append(str.Substring(index + 4));
                    }
                }
                else
                {
                    _result.Append(str);
                }
            }
        }


        public void Dispose()
        {
            _result.Clear();
            _result = null;
        }
    }
}
