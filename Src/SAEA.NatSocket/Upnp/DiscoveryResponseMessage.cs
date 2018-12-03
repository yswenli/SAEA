/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V3.3.3.5
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
*版本号： V3.3.3.5
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.NatSocket.Upnp
{
    class DiscoveryResponseMessage
    {
        private readonly IDictionary<string, string> _headers;

        public DiscoveryResponseMessage(string message)
        {
            var lines = message.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var headers = from h in lines.Skip(1)
                          let c = h.Split(':')
                          let key = c[0]
                          let value = c.Length > 1
                              ? string.Join(":", c.Skip(1).ToArray())
                              : string.Empty
                          select new { Key = key, Value = value.Trim() };
            _headers = headers.ToDictionary(x => x.Key.ToUpperInvariant(), x => x.Value);
        }

        public string this[string key]
        {
            get { return _headers[key.ToUpperInvariant()]; }
        }
    }
}
