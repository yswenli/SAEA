/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Provider
*文件名： NoticeCollection
*版本号： v6.0.0.1
*唯一标识：9555d1ce-23a8-4302-b470-7abffdebcdfa
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 17:35:38
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 17:35:38
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAEA.RPC.Provider
{
    class NoticeCollection
    {
        ConcurrentDictionary<string, IUserToken> _collection = new ConcurrentDictionary<string, IUserToken>();


        public async Task Set(IUserToken userToken)
        {
            await Task.Run(() =>
            {
                if(userToken!=null && userToken.Socket!=null && userToken.Socket.Connected)
                {
                    _collection.AddOrUpdate(userToken.ID, userToken, (k, v) =>
                    {
                        return userToken;
                    });
                }
            });
        }


        public async Task<List<IUserToken>> GetListAsync()
        {
            return await Task.Run(() =>
            {
                List<IUserToken> result = new List<IUserToken>();

                if (!_collection.IsEmpty)
                {
                    var list = _collection.Values.ToList();

                    if (list != null && list.Any())
                    {
                        foreach (var item in list)
                        {
                            if (item.Socket == null || !item.Socket.Connected)
                            {
                                _collection.TryRemove(item.ID, out IUserToken userToken);
                            }
                        }
                    }

                    result = _collection.Values.ToList();
                }
                return result;
            });
        }

    }


}
