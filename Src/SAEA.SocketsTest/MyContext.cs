/****************************************************************************
*项目名称：SAEA.SocketsTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.SocketsTest
*类 名 称：MyContext
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/19 15:05:35
*描述：
*=====================================================================
*修改时间：2019/8/19 15:05:35
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.SocketsTest
{
    class MyContext : IContext
    {
        public IUserToken UserToken { get; set; }
        public IUnpacker Unpacker { get; set; }

        public MyContext()
        {
            var unpacker = new MyUnpacker();

            this.UserToken = new UserToken();
            this.UserToken.Unpacker = unpacker;
            this.Unpacker = unpacker;
        }
    }
}
