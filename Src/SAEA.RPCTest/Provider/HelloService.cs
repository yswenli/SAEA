/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPCTest.Provider
*文件名： HelloService
*版本号： v7.0.0.1
*唯一标识：a512f6a6-844b-4913-9cc5-ebccb034f78f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/5/17 19:15:01
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 19:15:01
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.RPC;
using SAEA.RPCTest.Provider.Attr;
using SAEA.RPCTest.Provider.Model;
using System.Text;

namespace SAEA.RPCTest.Provider
{
    /// <summary>
    /// Hello 服务示例，验证字符串、数值、对象与字节数组的远程调用
    /// </summary>
    [RPCService]
    public class HelloService
    {
        /// <summary>
        /// 拼接固定内容返回，用于验证大字符串传输
        /// </summary>
        /// <returns>由 1000 个「hello world!」拼接成的字符串</returns>
        [LogAttribute]
        public string Hello()
        {
            var sb = new StringBuilder();

            for (int i = 0; i < 1000; i++)
            {
                sb.Append("hello world!");
            }

            return sb.ToString(); ;
        }

        /// <summary>
        /// 计算两个整数的和
        /// </summary>
        /// <param name="x">被加数</param>
        /// <param name="y">加数</param>
        /// <returns>两数之和</returns>
        public int Plus(int x, int y)
        {
            return x + y;
        }

        /// <summary>
        /// 回传传入的用户信息，用于验证对象序列化
        /// </summary>
        /// <param name="info">待回传的用户信息</param>
        /// <returns>与入参相同的用户信息</returns>
        public UserInfo Update(UserInfo info)
        {
            return info;
        }

        /// <summary>
        /// 标记 NoRpc 的空方法，用于验证未暴露的方法无法远程调用
        /// </summary>
        [NoRpc]
        public void TestNoRPC()
        {

        }

        /// <summary>
        /// 在返回内容前追加固定前缀，用于验证字节数组传输
        /// </summary>
        /// <param name="data">原始字节数据，按 UTF-8 解码</param>
        /// <returns>加上「Provider response:」前缀后按 UTF-8 编码的字节数据</returns>
        public byte[] SendData(byte[] data)
        {
            var str = System.Text.Encoding.UTF8.GetString(data);
            str = "Provider response:" + str;
            return System.Text.Encoding.UTF8.GetBytes(str);
        }
    }
}
