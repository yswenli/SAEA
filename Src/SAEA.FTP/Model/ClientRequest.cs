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
*修改时间：2026/11/11 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：增加原始命令文本，供服务端按原样记录日志
*
*****************************************************************************/
namespace SAEA.FTP.Model
{
    /// <summary>
    /// FTP 控制命令请求，拆分为命令字与参数
    /// </summary>
    public class ClientRequest
    {
        /// <summary>
        /// 命令字，如 USER、RETR
        /// </summary>
        public string Cmd { get; set; }

        /// <summary>
        /// 命令参数；命令不带参数时为 null
        /// </summary>
        public string Arg { get; set; }

        /// <summary>
        /// 收到的一整行原始命令文本，仅用于日志与审计，不参与协议解析
        /// </summary>
        internal string Raw { get; set; }

        /// <summary>
        /// 解析一行控制命令，空格位于下标 2 之后才按「命令 + 参数」拆分，否则整行作为命令字
        /// </summary>
        /// <param name="msg">一行原始命令文本</param>
        /// <returns>解析结果；输入为空或空白时返回 null</returns>
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
