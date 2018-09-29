using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SAEA.WebAPI.Http.Model
{
    public class FilePart
    {
        public byte[] Data { get; set; }

        public string FileName { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }

        public FilePart(string name, string fileName, string type)
        {
            Name = name;
            FileName = fileName.Split(Path.GetInvalidFileNameChars()).Last();
            Type = type;
        }

        public FilePart(string name, string fileName, string type, byte[] data)
        {
            Name = name;
            FileName = fileName.Split(Path.GetInvalidFileNameChars()).Last();
            Type = type;
            Data = data;
        }
    }
}
