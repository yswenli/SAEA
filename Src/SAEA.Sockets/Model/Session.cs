/****************************************************************************
*项目名称：SAEA.Sockets.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.Model
*类 名 称：Session
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/10/12 10:16:43
*描述：
*=====================================================================
*修改时间：2020/10/12 10:16:43
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Model
{
    public class Session : ISession
    {
        public string ID { get; set; }

        public Session(string id)
        {
            ID = id;
        }

        public new string ToString()
        {
            return ID;
        }
    }
}
