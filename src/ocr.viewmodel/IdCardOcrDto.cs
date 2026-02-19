using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ocr.viewmodel
{
    public class IdCardOcrDto
    {
        public string? Name { get; set; }
        public string? NationalId { get; set; }
        public string? Address { get; set; }
        public string? Dob { get; set; }
        public int? Age { get; set; }

        public string? Profession { get; set; }
        public string? Gender { get; set; }
        public string? Religion { get; set; }
        public string? MaritalStatus { get; set; }
        public string? HusbandName { get; set; }
        public string? ExpiryDate { get; set; }
    }
}
