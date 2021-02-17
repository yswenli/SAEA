using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.SoundFont
{
    internal class ZoneBuilder : StructureBuilder<Zone>
    {
        private Zone lastZone;

        public Zone[] Zones
        {
            get
            {
                return this.data.ToArray();
            }
        }

        public override int Length
        {
            get
            {
                return 4;
            }
        }

        public override Zone Read(BinaryReader br)
        {
            Zone zone = new Zone();
            zone.generatorIndex = br.ReadUInt16();
            zone.modulatorIndex = br.ReadUInt16();
            if (this.lastZone != null)
            {
                this.lastZone.generatorCount = (ushort)(zone.generatorIndex - this.lastZone.generatorIndex);
                this.lastZone.modulatorCount = (ushort)(zone.modulatorIndex - this.lastZone.modulatorIndex);
            }
            this.data.Add(zone);
            this.lastZone = zone;
            return zone;
        }

        public override void Write(BinaryWriter bw, Zone zone)
        {
        }

        public void Load(Modulator[] modulators, Generator[] generators)
        {
            for (int i = 0; i < this.data.Count - 1; i++)
            {
                Zone zone = this.data[i];
                zone.Generators = new Generator[(int)zone.generatorCount];
                Array.Copy(generators, (int)zone.generatorIndex, zone.Generators, 0, (int)zone.generatorCount);
                zone.Modulators = new Modulator[(int)zone.modulatorCount];
                Array.Copy(modulators, (int)zone.modulatorIndex, zone.Modulators, 0, (int)zone.modulatorCount);
            }
            this.data.RemoveAt(this.data.Count - 1);
        }
    }
}
