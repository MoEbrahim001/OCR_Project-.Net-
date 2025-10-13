using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ocr.viewmodel
{
    public sealed class BackExtractDto
    {
        public string? Profession { get; set; }     // كان proffession
        public string? Religion { get; set; }
        public string? Gender { get; set; }
        public string? MaritalStatus { get; set; }  // كان marital_status
        public string? EndDate { get; set; }        // كان enddate
        public string? HusbandName { get; set; }    // كان husband_name
        public string? Image { get; set; }
    }
}
