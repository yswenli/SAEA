/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：StaticTable
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 14:16:44
*描述：
*=====================================================================
*修改时间：2019/6/27 14:16:44
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Http2.Model
{
    /// <summary>
    /// RFC 7541附录A中定义的hpack静态标题表
    /// </summary>
    public static class StaticTable
    {

        public static int Length
        {
            get { return Entries.Length; }
        }


        public static readonly TableEntry[] Entries =
        {
            new TableEntry { Name = ":authority", NameLen = 10, Value = "", ValueLen = 0},
            new TableEntry { Name = ":method", NameLen = 7, Value = "GET", ValueLen = 3},
            new TableEntry { Name = ":method", NameLen = 7, Value = "POST", ValueLen = 4},
            new TableEntry { Name = ":path", NameLen = 5, Value = "/", ValueLen = 1},
            new TableEntry { Name = ":path", NameLen = 5, Value = "/index.html", ValueLen = 11},
            new TableEntry { Name = ":scheme", NameLen = 7, Value = "http", ValueLen = 4},
            new TableEntry { Name = ":scheme", NameLen = 7, Value = "https", ValueLen = 5},
            new TableEntry { Name = ":status", NameLen = 7, Value = "200", ValueLen = 3},
            new TableEntry { Name = ":status", NameLen = 7, Value = "204", ValueLen = 3},
            new TableEntry { Name = ":status", NameLen = 7, Value = "206", ValueLen = 3},
            new TableEntry { Name = ":status", NameLen = 7, Value = "304", ValueLen = 3},
            new TableEntry { Name = ":status", NameLen = 7, Value = "400", ValueLen = 3},
            new TableEntry { Name = ":status", NameLen = 7, Value = "404", ValueLen = 3},
            new TableEntry { Name = ":status", NameLen = 7, Value = "500", ValueLen = 3},
            new TableEntry { Name = "accept-charset", NameLen = 14, Value = "", ValueLen = 0},
            new TableEntry { Name = "accept-encoding", NameLen = 15, Value = "gzip, deflate", ValueLen = 13},
            new TableEntry { Name = "accept-language", NameLen = 15, Value = "", ValueLen = 0},
            new TableEntry { Name = "accept-ranges", NameLen = 13, Value = "", ValueLen = 0},
            new TableEntry { Name = "accept", NameLen = 6, Value = "", ValueLen = 0},
            new TableEntry { Name = "access-control-allow-origin", NameLen = 27, Value = "", ValueLen = 0},
            new TableEntry { Name = "age", NameLen = 3, Value = "", ValueLen = 0},
            new TableEntry { Name = "allow", NameLen = 5, Value = "", ValueLen = 0},
            new TableEntry { Name = "authorization", NameLen = 13, Value = "", ValueLen = 0},
            new TableEntry { Name = "cache-control", NameLen = 13, Value = "", ValueLen = 0},
            new TableEntry { Name = "content-disposition", NameLen = 19, Value = "", ValueLen = 0},
            new TableEntry { Name = "content-encoding", NameLen = 16, Value = "", ValueLen = 0},
            new TableEntry { Name = "content-language", NameLen = 16, Value = "", ValueLen = 0},
            new TableEntry { Name = "content-length", NameLen = 14, Value = "", ValueLen = 0},
            new TableEntry { Name = "content-location", NameLen = 16, Value = "", ValueLen = 0},
            new TableEntry { Name = "content-range", NameLen = 13, Value = "", ValueLen = 0},
            new TableEntry { Name = "content-type", NameLen = 12, Value = "", ValueLen = 0},
            new TableEntry { Name = "cookie", NameLen = 6, Value = "", ValueLen = 0},
            new TableEntry { Name = "date", NameLen = 4, Value = "", ValueLen = 0},
            new TableEntry { Name = "etag", NameLen = 4, Value = "", ValueLen = 0},
            new TableEntry { Name = "expect", NameLen = 6, Value = "", ValueLen = 0},
            new TableEntry { Name = "expires", NameLen = 7, Value = "", ValueLen = 0},
            new TableEntry { Name = "from", NameLen = 4, Value = "", ValueLen = 0},
            new TableEntry { Name = "host", NameLen = 4, Value = "", ValueLen = 0},
            new TableEntry { Name = "if-match", NameLen = 8, Value = "", ValueLen = 0},
            new TableEntry { Name = "if-modified-since", NameLen = 17, Value = "", ValueLen = 0},
            new TableEntry { Name = "if-none-match", NameLen = 13, Value = "", ValueLen = 0},
            new TableEntry { Name = "if-range", NameLen = 8, Value = "", ValueLen = 0},
            new TableEntry { Name = "if-unmodified-since", NameLen = 19, Value = "", ValueLen = 0},
            new TableEntry { Name = "last-modified", NameLen = 13, Value = "", ValueLen = 0},
            new TableEntry { Name = "link", NameLen = 4, Value = "", ValueLen = 0},
            new TableEntry { Name = "location", NameLen = 8, Value = "", ValueLen = 0},
            new TableEntry { Name = "max-forwards", NameLen = 12, Value = "", ValueLen = 0},
            new TableEntry { Name = "proxy-authenticate", NameLen = 18, Value = "", ValueLen = 0},
            new TableEntry { Name = "proxy-authorization", NameLen = 19, Value = "", ValueLen = 0},
            new TableEntry { Name = "range", NameLen = 5, Value = "", ValueLen = 0},
            new TableEntry { Name = "referer", NameLen = 7, Value = "", ValueLen = 0},
            new TableEntry { Name = "refresh", NameLen = 7, Value = "", ValueLen = 0},
            new TableEntry { Name = "retry-after", NameLen = 11, Value = "", ValueLen = 0},
            new TableEntry { Name = "server", NameLen = 6, Value = "", ValueLen = 0},
            new TableEntry { Name = "set-cookie", NameLen = 10, Value = "", ValueLen = 0},
            new TableEntry { Name = "strict-transport-security", NameLen = 25, Value = "", ValueLen = 0},
            new TableEntry { Name = "transfer-encoding", NameLen = 17, Value = "", ValueLen = 0},
            new TableEntry { Name = "user-agent", NameLen = 10, Value = "", ValueLen = 0},
            new TableEntry { Name = "vary", NameLen = 4, Value = "", ValueLen = 0},
            new TableEntry { Name = "via", NameLen = 3, Value = "", ValueLen = 0},
            new TableEntry { Name = "www-authenticate", NameLen = 16, Value = "", ValueLen = 0},
        };
    }
}
