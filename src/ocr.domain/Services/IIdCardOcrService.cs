using Microsoft.AspNetCore.Http;
using Ocr.Domain.Services;
using ocr.viewmodel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ocr.domain.Services
{
    public interface IIdCardOcrService
    {
        Task<IdCardOcrDto> ExtractAsync(
            IFormFile frontImage,
            IFormFile backImage,
            int threshold,
            CancellationToken ct = default);
    }

    public class IdCardOcrService : IIdCardOcrService
    {
        private readonly IOcrClient _ocrClient;
        private readonly IOcrParser _parser;

        public IdCardOcrService(IOcrClient ocrClient, IOcrParser parser)
        {
            _ocrClient = ocrClient;
            _parser = parser;
        }

        public async Task<IdCardOcrDto> ExtractAsync(
            IFormFile frontImage,
            IFormFile backImage,
            int threshold,
            CancellationToken ct = default)
        {
            await using var fStream = frontImage.OpenReadStream();
            var frontExtraction = await _ocrClient.ExtractFrontAsync(
                fStream,
                frontImage.FileName,
                frontImage.ContentType,
                threshold,
                ct);

            var front = _parser.ParseFront(frontExtraction);
          

            await using var bStream = backImage.OpenReadStream();
            var backExtraction = await _ocrClient.ExtractBackAsync(
                bStream,
                backImage.FileName,
                backImage.ContentType,
                threshold,
                ct);

            var back = _parser.ParseBack(backExtraction);
          

            var dto = new IdCardOcrDto
            {
                Name = front.Name,
                NationalId = front.NationalId,
                Address = front.Address,
                Dob = front.Dob,
                Age = front.Age,

                Profession = back.proffession,
                Gender = back.Gender,
                Religion = back.Religion,
                MaritalStatus = back.MaritalStatus,
                HusbandName = back.HusbandName,
                ExpiryDate = back.ExpiryDate
            };

            return dto;
        }
    }
}
