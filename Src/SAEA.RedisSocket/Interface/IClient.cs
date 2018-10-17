/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.RedisSocket.Interface
*文件名： IClient
*版本号： V2.2.0.1
*唯一标识：3806bd74-f304-42b2-ab04-3e219828fa60
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 16:16:57
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 16:16:57
*修改人： yswenli
*版本号： V2.2.0.1
*描述：
*
*****************************************************************************/
using SAEA.RedisSocket.Core;

namespace SAEA.RedisSocket.Interface
{
    public interface IClient
    {
        bool IsMaster
        {
            get;
        }

        int DBIndex
        {
            get;
        }

        bool IsConnected { get; set; }

        string Auth(string password);

        string Ping();

        bool Select(int dbIndex = 0);

        int DBSize();

        string Type(string key);

        string Info(string section = "all");

        string SlaveOf(string ipPort = "");        

        RedisDataBase GetDataBase(int dbIndex = -1);
    }
}
