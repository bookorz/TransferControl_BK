using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Config
{
    public class SystemConfig
    {
        public string RunMode { get; set; }

        public string OCR1ImgSourcePath { get; set; }
        public string OCR1ImgToJpgPath { get; set; }
        public string OCR2ImgSourcePath { get; set; }
        public string OCR2ImgToJpgPath { get; set; }

    }
}
