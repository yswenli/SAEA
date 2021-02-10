using System;
using System.IO;
using System.Text;

namespace SAEA.Audio.NAudio.SoundFont
{
    internal class PresetBuilder : StructureBuilder<Preset>
    {
        private Preset lastPreset;

        public override int Length
        {
            get
            {
                return 38;
            }
        }

        public Preset[] Presets
        {
            get
            {
                return this.data.ToArray();
            }
        }

        public override Preset Read(BinaryReader br)
        {
            Preset preset = new Preset();
            string text = Encoding.UTF8.GetString(br.ReadBytes(20), 0, 20);
            if (text.IndexOf('\0') >= 0)
            {
                text = text.Substring(0, text.IndexOf('\0'));
            }
            preset.Name = text;
            preset.PatchNumber = br.ReadUInt16();
            preset.Bank = br.ReadUInt16();
            preset.startPresetZoneIndex = br.ReadUInt16();
            preset.library = br.ReadUInt32();
            preset.genre = br.ReadUInt32();
            preset.morphology = br.ReadUInt32();
            if (this.lastPreset != null)
            {
                this.lastPreset.endPresetZoneIndex = (ushort)(preset.startPresetZoneIndex - 1);
            }
            this.data.Add(preset);
            this.lastPreset = preset;
            return preset;
        }

        public override void Write(BinaryWriter bw, Preset preset)
        {
        }

        public void LoadZones(Zone[] presetZones)
        {
            for (int i = 0; i < this.data.Count - 1; i++)
            {
                Preset preset = this.data[i];
                preset.Zones = new Zone[(int)(preset.endPresetZoneIndex - preset.startPresetZoneIndex + 1)];
                Array.Copy(presetZones, (int)preset.startPresetZoneIndex, preset.Zones, 0, preset.Zones.Length);
            }
            this.data.RemoveAt(this.data.Count - 1);
        }
    }
}
