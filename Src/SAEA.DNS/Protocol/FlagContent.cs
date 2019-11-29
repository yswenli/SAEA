/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Protocol
*类 名 称：DnsQuery
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

namespace SAEA.DNS.Protocol
{
    /// <summary>
    /// 标志
    /// </summary>
    public class FlagContent
    {
        byte[] _data = new byte[2];

        /// <summary>
        /// 查询/响应标志，0为查询，1为响应
        /// </summary>
        public QRType QR { get; set; }

        /// <summary>
        /// 0表示标准查询，1表示反向查询，2表示服务器状态请求
        /// </summary>
        public OPCodeType OPCode { get; set; }

        /// <summary>
        /// 表示授权回答
        /// </summary>
        public bool AA { get; set; }
        /// <summary>
        /// 表示可截断的
        /// </summary>
        public bool TC { get; set; }
        /// <summary>
        /// 表示期望递归
        /// </summary>
        public bool RD { get; set; }
        /// <summary>
        /// 表示可用递归
        /// </summary>
        public bool RA { get; set; }
        /// <summary>
        /// 表示返回码，0表示没有差错，3表示名字差错，2表示服务器错误（Server Failure）
        /// </summary>
        public RcodeType Rcode { get; set; }


        /// <summary>
        /// 标志
        /// </summary>
        /// <param name="qrType"></param>
        /// <param name="opCodeType"></param>
        /// <param name="aa"></param>
        /// <param name="tc"></param>
        /// <param name="rd"></param>
        /// <param name="ra"></param>
        /// <param name="rcodeType"></param>
        public FlagContent(QRType qrType, OPCodeType opCodeType, bool aa, bool tc, bool rd, bool ra, RcodeType rcodeType)
        {
            this.QR = qrType;
            this.OPCode = opCodeType;
            this.AA = aa;
            this.TC = tc;
            this.RD = rd;
            this.RA = ra;
            this.Rcode = rcodeType;

            int data = (byte)qrType;
            data = (data << 4) | (byte)opCodeType;
            data = (data << 1) | (aa ? 1 : 0);
            data = (data << 1) | (tc ? 1 : 0);
            data = (data << 1) | (rd ? 1 : 0);

            data = (data << 1) | (ra ? 1 : 0);
            data = (data << 3);
            data = (data << 4) | (byte)rcodeType;

            var sdata = (ushort)data;

            _data = BitConverter.GetBytes(sdata);
        }

        public FlagContent(ushort flag) : this(BitConverter.GetBytes(flag))
        {

        }

        public FlagContent(byte[] flag)
        {
            this.QR = (QRType)(flag[0] >> 7);
            this.OPCode = (OPCodeType)(flag[0] >> 3 - ((flag[0] >> 7) << 4));
            this.AA = ((flag[0] >> 2) - (flag[0] >> 3)) == 1;
            this.TC = ((flag[0] >> 1) - (flag[0] >> 2)) == 1;
            this.RD = ((flag[0]) - (flag[0] >> 1)) == 1;

            this.RA = (flag[1] >> 7) == 1;
            this.Rcode = (RcodeType)(flag[1] - ((flag[1] >> 7) << 7));
        }

        public byte[] ToBytes()
        {
            return _data;
        }

    }

    public enum QRType : byte
    {
        Request = 0,
        Response = 1
    }

    public enum OPCodeType : byte
    {
        Standard = 0,
        Reverse = 1,
        ServerState = 2
    }

    public enum RcodeType : byte
    {
        Ok = 0,
        ServerFailure = 2,
        NameErr = 3
    }
}
