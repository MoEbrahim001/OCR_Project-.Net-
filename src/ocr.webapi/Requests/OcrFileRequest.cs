using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Ocr.WebApi.Requests
{
    public class OcrFileRequest
    {
 public IFormFile FrontImage { get; set; } = default!;
      public IFormFile BackImage { get; set; } = default!;
        public int? Threshold { get; set; }
    }
}
