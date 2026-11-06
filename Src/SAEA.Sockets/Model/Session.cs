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
*唯一标识：421286b0-ecca-4ee4-8b3c-4adcf7244fad
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/10/13 16:01:17
*描述：Session接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Model
{
    /// <summary>
    /// 会话标识的默认实现，便于非连接场景直接按 ID 传递
    /// </summary>
    public class Session : ISession
    {
        /// <summary>
        /// 会话标识
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 以会话标识初始化
        /// </summary>
        /// <param name="id">会话标识</param>
        public Session(string id)
        {
            ID = id;
        }

        /// <summary>
        /// 返回会话标识；隐藏 object.ToString，以 Session 静态类型调用时得到 ID
        /// </summary>
        /// <returns>会话标识，可能为 null</returns>
        public new string ToString()
        {
            return ID;
        }
    }
}
