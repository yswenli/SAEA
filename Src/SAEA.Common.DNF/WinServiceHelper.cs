/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Commom
*文件名： WinServiceHelper
*版本号： v4.2.3.1
*唯一标识：045fba7e-77f2-416f-be74-4c91bf634110
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/30 16:13:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/30 16:13:26
*修改人： yswenli
*版本号： v4.2.3.1
*描述：
*
*****************************************************************************/
using System;
using System.ServiceProcess;

namespace SAEA.Common
{
    /// <summary>
    /// win服务工具类
    /// </summary>
    public class WinServiceHelper
    {
        #region 安装Windows服务

        /// <summary>
        ///     安装Windows服务
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="name"></param>
        /// <param name="display"></param>
        /// <param name="description"></param>
        public static void Install(string filePath, string name, string display, string description)
        {
            Console.WriteLine("开始安装服务：");
            Console.WriteLine("--> 服务名称：" + name);
            Console.WriteLine("--> 显示名称：" + display);
            Console.WriteLine("--> 服务描述：" + description);
            Console.WriteLine("");
            var ok = new WinServiceManager().InstallService(filePath, name, display, description);
            if (ok)
                Console.WriteLine("服务安装完毕.");
            else
                Console.WriteLine("安装失败，在安装过程中发生错误，请用管理员模式尝试.");
        }

        /// <summary>
        /// 安装并启动服务
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="name"></param>
        /// <param name="display"></param>
        /// <param name="description"></param>
        public static void InstallAndStart(string filePath, string name, string display, string description)
        {
            if (!WinServiceHelper.Exists(name))
            {
                WinServiceHelper.Install(filePath, name, display, description);
            }
            WinServiceHelper.Start(name);
        }

        #endregion

        #region 卸载Windows服务

        /// <summary>
        ///     卸载Windows服务
        /// </summary>
        /// <param name="name"></param>
        public static void Unstall(string name)
        {
            if (Exists(name))
            {
                if (IsStarted(name))
                    Stop(name);
                Console.WriteLine("开始卸载服务：");
                Console.WriteLine("--> 服务名称：" + name);
                Console.WriteLine("");
                var ok = false;
                try
                {
                    ok = new WinServiceManager().UnInstallService(name);
                }
                catch
                {
                }
                if (ok)
                    Console.WriteLine("服务卸载完毕.");
                else
                    Console.WriteLine("卸载失败，在卸载过程中发生错误.");
            }
        }

        #endregion

        #region 服务是否存在

        /// <summary>
        ///     服务是否存在
        /// </summary>
        /// <param name="NameService"></param>
        /// <returns></returns>
        public static bool Exists(string NameService)
        {
            if (!string.IsNullOrEmpty(NameService))
            {
                var services = ServiceController.GetServices();
                foreach (var s in services)
                    if (s.ServiceName.ToLower() == NameService.ToLower())
                        return true;
            }
            return false;
        }

        #endregion

        #region 判断window服务是否运行

        /// <summary>
        ///     判断某个Windows服务是否运行
        /// </summary>
        /// <returns></returns>
        public static bool IsStarted(string serviceName)
        {
            var psc = new ServiceController(serviceName);
            var bStartStatus = false;
            try
            {
                if (!psc.Status.Equals(ServiceControllerStatus.Stopped))
                    bStartStatus = true;

                return bStartStatus;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        #endregion

        #region  修改服务的启动项

        /// <summary>
        ///     修改服务的启动项 2为自动,3为手动
        /// </summary>
        /// <param name="startType"></param>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        public static bool ChangeServiceStartType(int startType, string serviceName)
        {
            return new WinServiceManager().ChangeServiceStartType(startType, serviceName);
        }

        #endregion

        #region 启动服务

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="serviceName"></param>
        public static void Start(string serviceName)
        {
            if (Exists(serviceName))
            {
                var service = new ServiceController(serviceName);
                if ((service.Status != ServiceControllerStatus.Running) &&
                    (service.Status != ServiceControllerStatus.StartPending))
                    service.Start();
            }
        }

        #endregion

        #region 停止服务

        /// <summary>
        /// 停止服务
        /// </summary>
        /// <param name="serviceName"></param>
        public static void Stop(string serviceName)
        {
            if (Exists(serviceName))
            {
                var service = new ServiceController(serviceName);
                if (service.Status == ServiceControllerStatus.Running)
                    service.Stop();
            }
        }

        #endregion
    }
}
