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
*命名空间：SAEA.RPC.Provider
*文件名： NoticeCollection
*版本号： v26.4.23.1
*唯一标识：c17a025d-ae52-43ff-a510-c76d85b1f65c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/03/28 18:05:06
*描述：NoticeCollection接口
*
*=====================================================================
*修改标记
*修改时间：2019/03/28 18:05:06
*修改人： yswenli
*版本号： v26.4.23.1
*描述：NoticeCollection接口
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