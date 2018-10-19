/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVC.Model
*文件名： IRequestDataReader
*版本号： V2.2.1.1
*唯一标识：a303db7d-f83c-4c49-9804-032ec2236232
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 13:58:08
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 13:58:08
*修改人： yswenli
*版本号： V2.2.1.1
*描述：
*
*****************************************************************************/
using System.Collections.Generic;

namespace SAEA.MVC.Model
{
    internal interface IRequestDataReader
    {
        string Json { get; set; }
        int Position { get; }
        List<FilePart> PostFiles { get; set; }

        bool Analysis(byte[] buffer);
        void AnalysisBody(byte[] requestData);
        void Dispose();
    }
}