/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| _f 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ _f 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Audio.Core
*文件名： MicrosoftAdpcmChatCodec
*版本号： v26.4.23.1
*唯一标识：6d3ea372-06c6-4e4f-8375-6d542616d883
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/11 16:46:45
*描述：微软ADPCM音频编解码类
*
*=====================================================================
*修改标记
*修改时间：2021/3/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：微软ADPCM音频编解码类
*
*****************************************************************************/

using SAEA.Audio.Base.NAudio.Wave;

namespace SAEA.Audio.Core
{
    class MicrosoftAdpcmChatCodec : AcmChatCodec
    {
        public MicrosoftAdpcmChatCodec()
            : base(new WaveFormat(8000, 16, 1), new AdpcmWaveFormat(8000,1))
        {
        }

        public override string Name => "Microsoft ADPCM";
    }
}
