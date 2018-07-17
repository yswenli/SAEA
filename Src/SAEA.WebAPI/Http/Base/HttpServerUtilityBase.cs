/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Http.Base
*文件名： HttpServerUtilityBase
*版本号： V1.0.0.0
*唯一标识：01f783cd-c751-47c5-a5b9-96d3aa840c70
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/16 11:03:29
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/16 11:03:29
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SAEA.WebAPI.Http.Base
{
    public class HttpServerUtilityBase
    {
        const string Controller = "Controller";

        public virtual string MapPath(string path)
        {
            var prex = new Uri("http://127.0.0.1:39654");

            var uri = new Uri(prex, path);

            //return Path.GetFullPath(Directory.GetCurrentDirectory() + uri.LocalPath);

            StackTrace ss = new StackTrace(true);

            var frames = ss.GetFrames();

            List<MethodBase> mbs = new List<MethodBase>();

            if (frames != null)
            {
                foreach (var item in frames)
                {
                    mbs.Add(item.GetMethod());
                }
            }

            MethodBase mb = null;

            foreach (var item in mbs)
            {
                var len = item.DeclaringType.FullName.Length;
                if (item.DeclaringType.FullName.LastIndexOf(Controller) == len - 10)
                {
                    mb = item;
                    break;
                }
            }

            return Path.Combine(Path.GetDirectoryName(mb.DeclaringType.Assembly.Location) + uri.LocalPath);
        }

        public virtual string UrlEncode(string s, bool isData = false)
        {
            if (isData)
            {
                return Uri.EscapeDataString(s);
            }
            return Uri.EscapeUriString(s);

        }

        public virtual string UrlDecode(string s)
        {
            return Uri.UnescapeDataString(s);
        }
    }
}
