/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Commom
*文件名： WinServiceManager
*版本号： v4.3.1.2
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
*版本号： v4.3.1.2
*描述：
*
*****************************************************************************/
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Common
{
    class WinServiceManager
    {
        #region win32
        // Fields
        public const uint SC_MANAGER_ALL_ACCESS = 0xf003f;
        public const uint SC_MANAGER_CONNECT = 1;
        public const uint SC_MANAGER_CREATE_SERVICE = 2;
        public const uint SC_MANAGER_ENUMERATE_SERVICE = 4;
        public const uint SC_MANAGER_LOCK = 8;
        public const uint SC_MANAGER_MODIFY_BOOT_CONFIG = 0x20;
        public const uint SC_MANAGER_QUERY_LOCK_STATUS = 0x10;
        public const uint SERVICE_ADAPTER = 4;
        public const uint SERVICE_ALL_ACCESS = 0xf01ff;
        public const uint SERVICE_AUTO_START = 2;
        public const uint SERVICE_BOOT_START = 0;
        public const uint SERVICE_CHANGE_CONFIG = 2;
        public const uint SERVICE_CONFIG_DESCRIPTION = 1;
        public const uint SERVICE_CONFIG_FAILURE_ACTIONS = 2;
        public const uint SERVICE_DEMAND_START = 3;
        public const uint SERVICE_DISABLED = 4;
        public const uint SERVICE_DRIVER = 11;
        public const uint SERVICE_ENUMERATE_DEPENDENTS = 8;
        public const uint SERVICE_ERROR_CRITICAL = 3;
        public const uint SERVICE_ERROR_IGNORE = 0;
        public const uint SERVICE_ERROR_NORMAL = 1;
        public const uint SERVICE_ERROR_SEVERE = 2;
        public const uint SERVICE_FILE_SYSTEM_DRIVER = 2;
        public const uint SERVICE_INTERACTIVE_PROCESS = 0x100;
        public const uint SERVICE_INTERROGATE = 0x80;
        public const uint SERVICE_KERNEL_DRIVER = 1;
        public const uint SERVICE_PAUSE_CONTINUE = 0x40;
        public const uint SERVICE_QUERY_CONFIG = 1;
        public const uint SERVICE_QUERY_STATUS = 4;
        public const uint SERVICE_RECOGNIZER_DRIVER = 8;
        public const uint SERVICE_START = 0x10;
        public const uint SERVICE_STOP = 0x20;
        public const uint SERVICE_SYSTEM_START = 1;
        public const uint SERVICE_TYPE_ALL = 0x13f;
        public const uint SERVICE_USER_DEFINED_CONTROL = 0x100;
        public const uint SERVICE_WIN32 = 0x30;
        public const uint SERVICE_WIN32_OWN_PROCESS = 0x10;
        public const uint SERVICE_WIN32_SHARE_PROCESS = 0x20;
        public const uint STANDARD_RIGHTS_REQUIRED = 0xf0000;

        // Methods
        [DllImport("advapi32.dll")]
        public static extern bool ChangeServiceConfig2(IntPtr hService, uint dwInfoLevel, ref string lpInfo);
        [DllImport("advapi32.dll")]
        public static extern bool CloseServiceHandle(IntPtr hSCObject);
        [DllImport("advapi32.dll", EntryPoint = "CreateServiceA")]
        public static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, uint dwDesiredAccess, uint dwServiceType, uint dwStartType, uint dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId, string lpDependencies, string lpServiceStartName, string lpPassword);
        [DllImport("advapi32.dll")]
        public static extern bool DeleteService(IntPtr hService);
        public bool InstallService(string svcPath, string svcName, string svcDisplayName, string svcDescription)
        {
            bool flag = false;
            IntPtr zero = IntPtr.Zero;
            IntPtr hService = IntPtr.Zero;
            try
            {
                zero = OpenSCManager(Environment.MachineName, null, 0xf003f);
                if (zero != IntPtr.Zero)
                {
                    hService = CreateService(zero, svcName, svcDisplayName, 0xf01ff, 0x110, 2, 1, svcPath, null, IntPtr.Zero, null, null, null);
                    if (hService != IntPtr.Zero)
                    {
                        flag = ChangeServiceConfig2(hService, 1, ref svcDescription);
                        CloseServiceHandle(hService);
                        hService = IntPtr.Zero;
                    }
                }
                CloseServiceHandle(zero);
                zero = IntPtr.Zero;
            }
            catch
            {
                flag = false;
            }
            return flag;
        }

        [DllImport("advapi32.dll")]
        public static extern IntPtr OpenSCManager(string lpMachineName, string lpDatabaseName, uint dwDesiredAccess);
        [DllImport("advapi32.dll")]
        public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);
        public bool UnInstallService(string svcName)
        {
            bool flag = false;
            IntPtr zero = IntPtr.Zero;
            IntPtr hService = IntPtr.Zero;
            try
            {
                zero = OpenSCManager(Environment.MachineName, null, 0xf003f);
                if (zero != IntPtr.Zero)
                {
                    hService = OpenService(zero, svcName, 0xf01ff);
                    if (hService != IntPtr.Zero)
                    {
                        flag = DeleteService(hService);
                        CloseServiceHandle(hService);
                        hService = IntPtr.Zero;
                    }
                }
                CloseServiceHandle(hService);
                hService = IntPtr.Zero;
            }
            catch
            {
                flag = false;
            }
            return flag;
        }

        /// <summary>
        /// 修改服务的启动项 2为自动,3为手动
        /// </summary>
        /// <param name="startType"></param>
        /// <param name="serviceName"></param>
        /// <returns></returns>

        public bool ChangeServiceStartType(int startType, string serviceName)
        {
            try
            {
                var regist = Registry.LocalMachine;
                var sysReg = regist.OpenSubKey("SYSTEM");
                var currentControlSet = sysReg.OpenSubKey("CurrentControlSet");
                var services = currentControlSet.OpenSubKey("Services");
                var servicesName = services.OpenSubKey(serviceName, true);
                servicesName.SetValue("Start", startType);
            }
            catch
            {
                return false;
            }
            return true;
        }
        #endregion
    }
}
