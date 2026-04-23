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
*命名空间：SAEA.FTP.Model
*文件名： ClientRequest
*版本号： v26.4.23.1
*唯一标识：aa324525-275b-4656-98df-73f888927caf
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/12 10:35:26
*描述：ClientRequest接口
*
*=====================================================================
*修改标记
*修改时间：2019/11/12 10:35:26
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ClientRequest接口
*
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
                        Arg = msg.Substring(index + 1).Trim()
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
