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
*命名空间：SAEA.RedisSocket.Base
*文件名： RedirectHandler
*版本号： v26.4.23.1
*唯一标识：88ca912d-7301-4bcb-838f-58fe4543d758
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/11/05 23:13:59
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/11/05 23:13:59
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.RedisSocket.Interface;
using SAEA.RedisSocket.Model;

namespace SAEA.RedisSocket.Base
{
    public delegate IResult RedirectHandler(string ipPort, OperationType operationType, params object[] args);
}