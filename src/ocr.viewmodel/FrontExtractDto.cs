using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ocr.viewmodel
{
    public sealed class FrontExtractDto
    {
        public string? name { get; set; }
        public string? ID { get; set; }
        public string? DOB { get; set; }
        public string? address { get; set; }
        public string? image { get; set; }
        public string? face { get; set; }
    }
}
