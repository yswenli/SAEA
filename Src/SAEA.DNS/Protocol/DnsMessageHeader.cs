/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Protocol
*类 名 称：DnsMessageHeader
*版 本 号： v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
****************************************************************************/
using System;
using System.Collections.Generic;

namespace SAEA.DNS.Protocol
{
    /// <summary>
    /// 头部
    /// </summary>
    public class DnsMessageHeader
    {
        Random _randnom = null;

        #region properties

        /// <summary>
        /// 会话标识
        /// </summary>
        public ushort TransactionID { get; set; }
        /// <summary>
        /// 标志
        /// </summary>
        public FlagContent Flag { get; set; }
        /// <summary>
        /// 问题数
        /// </summary>
        public ushort Questions { get; set; }
        /// <summary>
        /// 回答数
        /// </summary>
        public ushort Answers { get; set; }
        /// <summary>
        /// 授权数
        /// </summary>
        public ushort Authorities { get; set; }
        /// <summary>
        /// 附加数
        /// </summary>
        public ushort Additionals { get; set; }

        #endregion

        public DnsMessageHeader(FlagContent flag, ushort questions, ushort answers, ushort authorities, ushort additionals)
        {
            _randnom = new Random(Environment.TickCount);

            this.TransactionID = (ushort)_randnom.Next(0, 65535);

            this.Flag = flag;

            this.Questions = questions;

            this.Answers = answers;

            this.Authorities = authorities;

            this.Additionals = additionals;
        }

        public byte[] ToBytes()
        {
            List<byte> result = new List<byte>();

            result.AddRange(BitConverter.GetBytes(this.TransactionID));

            result.AddRange(this.Flag.ToBytes());

            result.AddRange(BitConverter.GetBytes(this.Questions));

            result.AddRange(BitConverter.GetBytes(this.Answers));

            result.AddRange(BitConverter.GetBytes(this.Authorities));

            result.AddRange(BitConverter.GetBytes(this.Additionals));

            return result.ToArray();
        }
    }
}
