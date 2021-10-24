/****************************************************************************
*项目名称：SAEA.Audio.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio.Model
*类 名 称：DataInfo
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/2/10 11:03:06
*描述：
*=====================================================================
*修改时间：2021/2/10 11:03:06
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.Audio.Model
{
    public class DataInfo
    {
        public string ChannelID { get; set; }

        public byte[] Data { get; set; }
    }
}
