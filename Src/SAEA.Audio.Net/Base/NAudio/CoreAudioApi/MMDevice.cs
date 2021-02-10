using SAEA.Audio.NAudio.CoreAudioApi.Interfaces;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
    public class MMDevice : IDisposable
    {
        private readonly IMMDevice deviceInterface;

        private PropertyStore propertyStore;

        private AudioMeterInformation audioMeterInformation;

        private AudioEndpointVolume audioEndpointVolume;

        private AudioSessionManager audioSessionManager;

        private static Guid IID_IAudioMeterInformation = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");

        private static Guid IID_IAudioEndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");

        private static Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");

        private static Guid IDD_IAudioSessionManager = new Guid("BFA971F1-4D5E-40BB-935E-967039BFBEE4");

        public AudioClient AudioClient
        {
            get
            {
                return this.GetAudioClient();
            }
        }

        public AudioMeterInformation AudioMeterInformation
        {
            get
            {
                if (this.audioMeterInformation == null)
                {
                    this.GetAudioMeterInformation();
                }
                return this.audioMeterInformation;
            }
        }

        public AudioEndpointVolume AudioEndpointVolume
        {
            get
            {
                if (this.audioEndpointVolume == null)
                {
                    this.GetAudioEndpointVolume();
                }
                return this.audioEndpointVolume;
            }
        }

        public AudioSessionManager AudioSessionManager
        {
            get
            {
                if (this.audioSessionManager == null)
                {
                    this.GetAudioSessionManager();
                }
                return this.audioSessionManager;
            }
        }

        public PropertyStore Properties
        {
            get
            {
                if (this.propertyStore == null)
                {
                    this.GetPropertyInformation();
                }
                return this.propertyStore;
            }
        }

        public string FriendlyName
        {
            get
            {
                if (this.propertyStore == null)
                {
                    this.GetPropertyInformation();
                }
                if (this.propertyStore.Contains(PropertyKeys.PKEY_Device_FriendlyName))
                {
                    return (string)this.propertyStore[PropertyKeys.PKEY_Device_FriendlyName].Value;
                }
                return "Unknown";
            }
        }

        public string DeviceFriendlyName
        {
            get
            {
                if (this.propertyStore == null)
                {
                    this.GetPropertyInformation();
                }
                if (this.propertyStore.Contains(PropertyKeys.PKEY_DeviceInterface_FriendlyName))
                {
                    return (string)this.propertyStore[PropertyKeys.PKEY_DeviceInterface_FriendlyName].Value;
                }
                return "Unknown";
            }
        }

        public string IconPath
        {
            get
            {
                if (this.propertyStore == null)
                {
                    this.GetPropertyInformation();
                }
                if (this.propertyStore.Contains(PropertyKeys.PKEY_Device_IconPath))
                {
                    return (string)this.propertyStore[PropertyKeys.PKEY_Device_IconPath].Value;
                }
                return "Unknown";
            }
        }

        public string ID
        {
            get
            {
                string result;
                Marshal.ThrowExceptionForHR(this.deviceInterface.GetId(out result));
                return result;
            }
        }

        public DataFlow DataFlow
        {
            get
            {
                DataFlow result;
                (this.deviceInterface as IMMEndpoint).GetDataFlow(out result);
                return result;
            }
        }

        public DeviceState State
        {
            get
            {
                DeviceState result;
                Marshal.ThrowExceptionForHR(this.deviceInterface.GetState(out result));
                return result;
            }
        }

        private void GetPropertyInformation()
        {
            IPropertyStore store;
            Marshal.ThrowExceptionForHR(this.deviceInterface.OpenPropertyStore(StorageAccessMode.Read, out store));
            this.propertyStore = new PropertyStore(store);
        }

        private AudioClient GetAudioClient()
        {
            object obj;
            Marshal.ThrowExceptionForHR(this.deviceInterface.Activate(ref MMDevice.IID_IAudioClient, ClsCtx.ALL, IntPtr.Zero, out obj));
            return new AudioClient(obj as IAudioClient);
        }

        private void GetAudioMeterInformation()
        {
            object obj;
            Marshal.ThrowExceptionForHR(this.deviceInterface.Activate(ref MMDevice.IID_IAudioMeterInformation, ClsCtx.ALL, IntPtr.Zero, out obj));
            this.audioMeterInformation = new AudioMeterInformation(obj as IAudioMeterInformation);
        }

        private void GetAudioEndpointVolume()
        {
            object obj;
            Marshal.ThrowExceptionForHR(this.deviceInterface.Activate(ref MMDevice.IID_IAudioEndpointVolume, ClsCtx.ALL, IntPtr.Zero, out obj));
            this.audioEndpointVolume = new AudioEndpointVolume(obj as IAudioEndpointVolume);
        }

        private void GetAudioSessionManager()
        {
            object obj;
            Marshal.ThrowExceptionForHR(this.deviceInterface.Activate(ref MMDevice.IDD_IAudioSessionManager, ClsCtx.ALL, IntPtr.Zero, out obj));
            this.audioSessionManager = new AudioSessionManager(obj as IAudioSessionManager);
        }

        internal MMDevice(IMMDevice realDevice)
        {
            this.deviceInterface = realDevice;
        }

        public override string ToString()
        {
            return this.FriendlyName;
        }

        public void Dispose()
        {
            AudioEndpointVolume expr_06 = this.audioEndpointVolume;
            if (expr_06 != null)
            {
                expr_06.Dispose();
            }
            AudioSessionManager expr_17 = this.audioSessionManager;
            if (expr_17 != null)
            {
                expr_17.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        ~MMDevice()
        {
            this.Dispose();
        }
    }
}
