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
*命名空间：SAEA.Audio.Core
*文件名： TrueSpeechChatCodec
*版本号： v26.4.23.1
*唯一标识：46107947-4599-4c19-85e0-b531ecec63d0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/10 15:16:38
*描述：TrueSpeechChatCodec编解码类
*
*=====================================================================
*修改标记
*修改时间：2021/02/10 15:16:38
*修改人： yswenli
*版本号： v26.4.23.1
*描述：TrueSpeechChatCodec编解码类
*
*****************************************************************************/
using SAEA.Audio.Base.NAudio.Wave;

namespace SAEA.Audio.Core
{
    /// <summary>
    /// DSP Group TrueSpeech codec, using ACM
    /// n.b. Windows XP came with a TrueSpeech codec built in
    /// - looks like Windows 7 doesn't
    /// </summary>
    class TrueSpeechChatCodec : AcmChatCodec
    {
        public TrueSpeechChatCodec()
            : base(new WaveFormat(8000, 16, 1), new TrueSpeechWaveFormat())
        {
        }
    
        public override string Name => "DSP Group TrueSpeech";
    }
}
