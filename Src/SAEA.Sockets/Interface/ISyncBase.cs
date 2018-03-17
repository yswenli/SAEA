using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Sockets.Interface
{
    public interface ISyncBase
    {
        /// <summary>
        /// 同步对象，用于lock
        /// </summary>
        object SyncLocker
        {
            get;
        }
    }
}
