/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Common.Marshalling
*类 名 称：EndianAttribute
*版 本 号：v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using System;

namespace SAEA.DNS.Common.Marshalling 
{
    /// <summary>
    /// 编码排序
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Struct)]
    public class EndianAttribute : Attribute {
        public EndianAttribute(Endianness endianness) {
            this.Endianness = endianness;
        }

        public Endianness Endianness {
            get;
            private set;
        }
    }
}
