using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ocr.viewmodel
{
    public sealed class BackExtractDto
    {
        public string? Profession { get; set; }

        public string? Religion { get; set; }

        public string? Gender { get; set; }

        public string? MaritalStatus { get; set; }

        public string? EndDate { get; set; }

        public string? HusbandName { get; set; }

        public string? Image { get; set; }
    }
}
