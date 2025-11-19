using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ocr.model.Entities
{
    public class IdCard
    {
        public int Id { get; set; }            // PK
        public string? Name { get; set; }
        public string? Address { get; set; }
        public DateTime? DOB { get; set; }
        public string? NationalID { get; set; }
        public string? Gender { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Profession { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Religion { get; set; }

        public byte[]? FrontImage { get; set; }
        public byte[]? BackImage { get; set; }
        public byte[]? FaceImage { get; set; }
    }
}
