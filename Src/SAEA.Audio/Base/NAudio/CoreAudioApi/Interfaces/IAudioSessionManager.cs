using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces
{
    /// <summary>
    /// Windows CoreAudio IAudioSessionManager interface
    /// Defined in AudioPolicy.h
    /// </summary>
    [Guid("BFA971F1-4D5E-40BB-935E-967039BFBEE4"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager
    {
        /// <summary>
        /// Retrieves an audio session control.
        /// </summary>
        /// <param name="sessionId">A new or existing session ID.</param>
        /// <param name="streamFlags">Audio session flags.</param>
        /// <param name="sessionControl">Receives an <see cref="IAudioSessionControl"/> interface for the audio session.</param>
        /// <returns>An HRESULT code indicating whether the operation succeeded of failed.</returns>
        [PreserveSig]
        int GetAudioSessionControl(
            [In, Optional] [MarshalAs(UnmanagedType.LPStruct)] Guid sessionId,
            [In] [MarshalAs(UnmanagedType.U4)] UInt32 streamFlags,
            [Out] [MarshalAs(UnmanagedType.Interface)] out IAudioSessionControl sessionControl);

        /// <summary>
        /// Retrieves a simple audio volume control.
        /// </summary>
        /// <param name="sessionId">A new or existing session ID.</param>
        /// <param name="streamFlags">Audio session flags.</param>
        /// <param name="audioVolume">Receives an <see cref="ISimpleAudioVolume"/> interface for the audio session.</param>
        /// <returns>An HRESULT code indicating whether the operation succeeded of failed.</returns>
        [PreserveSig]
        int GetSimpleAudioVolume(
            [In, Optional] [MarshalAs(UnmanagedType.LPStruct)] Guid sessionId,
            [In] [MarshalAs(UnmanagedType.U4)] UInt32 streamFlags,
            [Out] [MarshalAs(UnmanagedType.Interface)] out ISimpleAudioVolume audioVolume);
    }
}
