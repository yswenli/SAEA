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
*命名空间：SAEA.Http.Model
*文件名： IWebHost
*版本号： v26.4.23.1
*唯一标识：19746f8a-94cc-4004-9c64-2afc80d67f33
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：IWebHost接口
*
*=====================================================================
*修改标记
*修改时间：2018/10/28 14:19:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：IWebHost接口
*
*****************************************************************************/
using SAEA.Http.Base;
using SAEA.Sockets.Interface;

namespace SAEA.Http.Model
{

    public interface IWebHost
    {
        bool IsRunning { get; set; }

        WebConfig WebConfig { get; set; }

        HttpUtility HttpUtility { get; }

        object RouteParam { get; set; }

        /// <summary>
        /// 自定义异常事件
        /// </summary>
        event ExceptionHandler OnException;

        void Start();

        void Send(IUserToken userToken, byte[] data);

        void Disconnect(IUserToken userToken);

        void End(IUserToken userToken, byte[] data);

        void Stop();
    }
}