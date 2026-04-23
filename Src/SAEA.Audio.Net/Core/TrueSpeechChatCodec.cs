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
*文件名： TrueSpeechChatCodec
*版本号： v26.4.23.1
*唯一标识：374cda21-534e-402b-9aea-9092ea945290
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/11 16:46:45
*描述：TrueSpeech音频编解码类
*
*=====================================================================
*修改标记
*修改时间：2021/3/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：TrueSpeech音频编解码类
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
