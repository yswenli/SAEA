/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPCTest.Provider
*文件名： HelloService
*版本号： V3.3.3.4
*唯一标识：a512f6a6-844b-4913-9cc5-ebccb034f78f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 19:15:01
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 19:15:01
*修改人： yswenli
*版本号： V3.3.3.4
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RPC.Common;
using SAEA.RPCTest.Provider.Model;
using System.Text;

namespace SAEA.RPCTest.Provider
{
    [RPCService]
    public class HelloService
    {
        public string Hello()
        {
            var sb = new StringBuilder();

            for (int i = 0; i < 100000; i++)
            {
                sb.Append("hello world!");
            }

            return sb.ToString(); ;
        }

        public int Plus(int x, int y)
        {
            return x + y;
        }

        public UserInfo Update(UserInfo info)
        {
            return info;
        }

        [NoRpc]
        public void TestNoRPC()
        {

        }

        public byte[] SendData(byte[] data)
        {
            var str = System.Text.Encoding.UTF8.GetString(data);
            str = "Provider response:" + str;
            return System.Text.Encoding.UTF8.GetBytes(str);
        }
    }
}
