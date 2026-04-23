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
*命名空间：SAEA.Sockets.Model
*文件名： Session
*版本号： v26.4.23.1
*唯一标识：05c63eb6-3fc5-4986-8746-0d051e5118c4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/10/13 16:01:17
*描述：
*
*=====================================================================
*修改标记
*修改时间：2020/10/13 16:01:17
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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
