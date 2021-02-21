using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces
{
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2 : IAudioSessionManager
    {
        /// <summary>
        /// Retrieves an audio session control.
        /// </summary>
        /// <param name="sessionId">A new or existing session ID.</param>
        /// <param name="streamFlags">Audio session flags.</param>
        /// <param name="sessionControl">Receives an <see cref="IAudioSessionControl"/> interface for the audio session.</param>
        /// <returns>An HRESULT code indicating whether the operation succeeded of failed.</returns>
        [PreserveSig]
        new int GetAudioSessionControl(
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
        new int GetSimpleAudioVolume(
            [In, Optional] [MarshalAs(UnmanagedType.LPStruct)] Guid sessionId,
            [In] [MarshalAs(UnmanagedType.U4)] UInt32 streamFlags,
            [Out] [MarshalAs(UnmanagedType.Interface)] out ISimpleAudioVolume audioVolume);

        [PreserveSig]
        int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);

        [PreserveSig]
        int RegisterSessionNotification(IAudioSessionNotification sessionNotification);

        [PreserveSig]
        int UnregisterSessionNotification(IAudioSessionNotification sessionNotification);

        [PreserveSig]
        int RegisterDuckNotification(string sessionId, IAudioSessionNotification audioVolumeDuckNotification);

        [PreserveSig]
        int UnregisterDuckNotification(IntPtr audioVolumeDuckNotification);
    }
}
