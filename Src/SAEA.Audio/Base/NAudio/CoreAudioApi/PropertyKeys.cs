using System;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi
{
	public static class PropertyKeys
	{
		public static readonly PropertyKey PKEY_DeviceInterface_FriendlyName = new PropertyKey(new Guid(40784238, -18412, 16715, 131, 205, 133, 109, 111, 239, 72, 34), 2);

		public static readonly PropertyKey PKEY_AudioEndpoint_FormFactor = new PropertyKey(new Guid(497408003, -11118, 20189, 140, 35, 224, 192, 255, 238, 127, 14), 0);

		public static readonly PropertyKey PKEY_AudioEndpoint_ControlPanelPageProvider = new PropertyKey(new Guid(497408003, -11118, 20189, 140, 35, 224, 192, 255, 238, 127, 14), 1);

		public static readonly PropertyKey PKEY_AudioEndpoint_Association = new PropertyKey(new Guid(497408003, -11118, 20189, 140, 35, 224, 192, 255, 238, 127, 14), 2);

		public static readonly PropertyKey PKEY_AudioEndpoint_PhysicalSpeakers = new PropertyKey(new Guid(497408003, -11118, 20189, 140, 35, 224, 192, 255, 238, 127, 14), 3);

		public static readonly PropertyKey PKEY_AudioEndpoint_GUID = new PropertyKey(new Guid(497408003, -11118, 20189, 140, 35, 224, 192, 255, 238, 127, 14), 4);

		public static readonly PropertyKey PKEY_AudioEndpoint_Disable_SysFx = new PropertyKey(new Guid(497408003, -11118, 20189, 140, 35, 224, 192, 255, 238, 127, 14), 5);

		public static readonly PropertyKey PKEY_AudioEndpoint_FullRangeSpeakers = new PropertyKey(new Guid(497408003, -11118, 20189, 140, 35, 224, 192, 255, 238, 127, 14), 6);

		public static readonly PropertyKey PKEY_AudioEndpoint_Supports_EventDriven_Mode = new PropertyKey(new Guid(497408003, -11118, 20189, 140, 35, 224, 192, 255, 238, 127, 14), 7);

		public static readonly PropertyKey PKEY_AudioEndpoint_JackSubType = new PropertyKey(new Guid(497408003, -11118, 20189, 140, 35, 224, 192, 255, 238, 127, 14), 8);

		public static readonly PropertyKey PKEY_AudioEngine_DeviceFormat = new PropertyKey(new Guid(-241236403, 2092, 20007, 188, 115, 104, 130, 161, 187, 142, 76), 0);

		public static readonly PropertyKey PKEY_AudioEngine_OEMFormat = new PropertyKey(new Guid(-460911066, 15557, 19666, 186, 70, 202, 10, 154, 112, 237, 4), 3);

		public static readonly PropertyKey PKEY_Device_FriendlyName = new PropertyKey(new Guid(-1537465010, -8420, 20221, 128, 32, 103, 209, 70, 168, 80, 224), 14);

		public static readonly PropertyKey PKEY_Device_IconPath = new PropertyKey(new Guid(630898684, 20647, 18382, 175, 8, 104, 201, 167, 215, 51, 102), 12);
	}
}
