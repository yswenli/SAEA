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
*命名空间：SAEA.DNS.Model
*文件名： ErroredEventArgs
*版本号： v26.4.23.1
*唯一标识：8cf99309-df02-40b2-a149-53edf36e7904
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 22:58:12
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/29 22:58:12
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.DNS.Model
{
    public class ErroredEventArgs : EventArgs
    {
        public ErroredEventArgs(Exception e)
        {
            Exception = e;
        }

        public Exception Exception
        {
            get;
            private set;
        }
    }
}
