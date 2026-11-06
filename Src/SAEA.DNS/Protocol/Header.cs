/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.DNS.Protocol
*文件名： Header
*版本号： v26.4.23.1
*唯一标识：0718b49e-6642-4774-b3a5-a56cd9fa2d46
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：Header类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Runtime.InteropServices;
using SAEA.Common;
using SAEA.DNS.Common.Utils;

namespace SAEA.DNS.Protocol
{
    /// <summary>
    /// DNS 报文头，12 字节定长，按大端序读写
    /// </summary>
    [Endian(EndianOrder.Big)]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Header
    {
        /// <summary>
        /// 报文头固定长度（12 字节）
        /// </summary>
        public const int SIZE = 12;

        /// <summary>
        /// 从字节数组解析报文头，长度不足 12 字节时抛异常
        /// </summary>
        /// <param name="header">报文头字节</param>
        /// <returns>解析出的报文头</returns>
        public static Header FromArray(byte[] header)
        {
            if (header.Length < SIZE)
            {
                throw new ArgumentException("Header length too small");
            }

            return StructHelper.GetStruct<Header>(header, 0, SIZE);
        }

        private ushort id;

        private byte flag0;
        private byte flag1;

        // Question count: number of questions in the Question section
        private ushort qdCount;

        // Answer record count: number of records in the Answer section
        private ushort anCount;

        // Authority record count: number of records in the Authority section
        private ushort nsCount;

        // Additional record count: number of records in the Additional section
        private ushort arCount;

        /// <summary>
        /// 消息标识，写入时截断为 2 字节
        /// </summary>
        public int Id
        {
            get { return id; }
            set { id = (ushort)value; }
        }

        /// <summary>
        /// 问题段记录数，写入时截断为 2 字节
        /// </summary>
        public int QuestionCount
        {
            get { return qdCount; }
            set { qdCount = (ushort)value; }
        }

        /// <summary>
        /// 应答段记录数，写入时截断为 2 字节
        /// </summary>
        public int AnswerRecordCount
        {
            get { return anCount; }
            set { anCount = (ushort)value; }
        }

        /// <summary>
        /// 授权段记录数，写入时截断为 2 字节
        /// </summary>
        public int AuthorityRecordCount
        {
            get { return nsCount; }
            set { nsCount = (ushort)value; }
        }

        /// <summary>
        /// 附加段记录数，写入时截断为 2 字节
        /// </summary>
        public int AdditionalRecordCount
        {
            get { return arCount; }
            set { arCount = (ushort)value; }
        }

        /// <summary>
        /// 是否为响应报文（QR 标志）
        /// </summary>
        public bool Response
        {
            get { return Qr == 1; }
            set { Qr = Convert.ToByte(value); }
        }

        /// <summary>
        /// 操作码（4 位）
        /// </summary>
        public OperationCode OperationCode
        {
            get { return (OperationCode)Opcode; }
            set { Opcode = (byte)value; }
        }

        /// <summary>
        /// 是否为授权应答（AA 标志）
        /// </summary>
        public bool AuthorativeServer
        {
            get { return Aa == 1; }
            set { Aa = Convert.ToByte(value); }
        }

        /// <summary>
        /// 是否因超长被截断（TC 标志）
        /// </summary>
        public bool Truncated
        {
            get { return Tc == 1; }
            set { Tc = Convert.ToByte(value); }
        }

        /// <summary>
        /// 是否期望递归查询（RD 标志）
        /// </summary>
        public bool RecursionDesired
        {
            get { return Rd == 1; }
            set { Rd = Convert.ToByte(value); }
        }

        /// <summary>
        /// 服务器是否支持递归查询（RA 标志）
        /// </summary>
        public bool RecursionAvailable
        {
            get { return Ra == 1; }
            set { Ra = Convert.ToByte(value); }
        }

        /// <summary>
        /// 数据是否已通过 DNSSEC 验证（AD 标志）
        /// </summary>
        public bool AuthenticData
        {
            get { return Ad == 1; }
            set { Ad = Convert.ToByte(value); }
        }

        /// <summary>
        /// 是否禁用 DNSSEC 校验（CD 标志）
        /// </summary>
        public bool CheckingDisabled
        {
            get { return Cd == 1; }
            set { Cd = Convert.ToByte(value); }
        }

        /// <summary>
        /// 响应码（4 位）
        /// </summary>
        public ResponseCode ResponseCode
        {
            get { return (ResponseCode)RCode; }
            set { RCode = (byte)value; }
        }

        /// <summary>
        /// 报文头长度，固定为 12 字节
        /// </summary>
        public int Size
        {
            get { return Header.SIZE; }
        }

        /// <summary>
        /// 按大端序序列化为 12 字节报文头
        /// </summary>
        /// <returns>报文头字节</returns>
        public byte[] ToArray()
        {
            return StructHelper.GetBytes(this);
        }

        /// <summary>
        /// 输出除长度外的各字段可读文本
        /// </summary>
        /// <returns>可读的报文头描述</returns>
        public override string ToString()
        {
            return ObjectStringifier.New(this)
                .AddAll()
                .Remove("Size")
                .ToString();
        }

        //查询/响应标志
        private byte Qr
        {
            get { return Flag0.GetBitValueAt(7, 1); }
            set { Flag0 = Flag0.SetBitValueAt(7, 1, value); }
        }

        //操作代码
        private byte Opcode
        {
            get { return Flag0.GetBitValueAt(3, 4); }
            set { Flag0 = Flag0.SetBitValueAt(3, 4, value); }
        }

        //权限回答标志
        private byte Aa
        {
            get { return Flag0.GetBitValueAt(2, 1); }
            set { Flag0 = Flag0.SetBitValueAt(2, 1, value); }
        }

        // 截断标志
        private byte Tc
        {
            get { return Flag0.GetBitValueAt(1, 1); }
            set { Flag0 = Flag0.SetBitValueAt(1, 1, value); }
        }

        // 递归标志
        private byte Rd
        {
            get { return Flag0.GetBitValueAt(0, 1); }
            set { Flag0 = Flag0.SetBitValueAt(0, 1, value); }
        }

        // 递归可用标志
        private byte Ra
        {
            get { return Flag1.GetBitValueAt(7, 1); }
            set { Flag1 = Flag1.SetBitValueAt(7, 1, value); }
        }

        // 0
        private byte Z
        {
            get { return Flag1.GetBitValueAt(6, 1); }
            set { }
        }

        // 权限数据
        private byte Ad
        {
            get { return Flag1.GetBitValueAt(5, 1); }
            set { Flag1 = Flag1.SetBitValueAt(5, 1, value); }
        }

        // 检查已禁用
        private byte Cd
        {
            get { return Flag1.GetBitValueAt(4, 1); }
            set { Flag1 = Flag1.SetBitValueAt(4, 1, value); }
        }

        // 响应代码
        private byte RCode
        {
            get { return Flag1.GetBitValueAt(0, 4); }
            set { Flag1 = Flag1.SetBitValueAt(0, 4, value); }
        }

        private byte Flag0
        {
            get { return flag0; }
            set { flag0 = value; }
        }

        private byte Flag1
        {
            get { return flag1; }
            set { flag1 = value; }
        }
    }
}
