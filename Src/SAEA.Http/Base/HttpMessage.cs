/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base
*文件名： HttpMessage
*版本号： v7.0.0.1
*唯一标识：ab912b9a-c7ed-44d9-8e48-eef0b6ff86a2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
* *修改时间：2018/11/7 17:11:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/11/7 17:11:15
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using SAEA.Http.Model;

namespace SAEA.Http.Base
{
    public class HttpMessage : HttpBase, IDisposable
    {
        public string ID
        {
            get; set;
        }

        public int Position { get; set; }

        /// <summary>
        /// 接收到的文件信息
        /// </summary>
        public List<FilePart> PostFiles
        {
            get; set;
        } = new List<FilePart>();

        /// <summary>
        /// enctype="text/plain"
        /// application/json
        /// </summary>
        public string Json
        {
            get; set;
        }

        public void Dispose()
        {
            if (this.Query != null)
                this.Query.Clear();
            if (this.Forms != null)
                this.Forms.Clear();
            if (this.Parmas != null)
                this.Parmas.Clear();
            if (Body != null)
                Array.Clear(Body, 0, Body.Length);
        }

    }
}
