/****************************************************************************
*项目名称：SAEA.FTP.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Model
*类 名 称：ClientRequest
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/12 10:29:21
*描述：
*=====================================================================
*修改时间：2019/11/12 10:29:21
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/

namespace SAEA.FTP.Model
{
    public class ClientRequest
    {
        public string Cmd { get; set; }

        public string Arg { get; set; }

        public static ClientRequest Parse(string msg)
        {
            ClientRequest result = null;

            if (!string.IsNullOrWhiteSpace(msg))
            {
                var index = msg.IndexOf(" ");

                if (index > 2)
                {
                    result = new ClientRequest()
                    {
                        Cmd = msg.Substring(0, index),
                        Arg = msg.Substring(index + 1)
                    };
                }
                else
                {
                    result = new ClientRequest()
                    {
                        Cmd = msg.Trim()
                    };
                }
            }

            return result;
        }
    }
}
