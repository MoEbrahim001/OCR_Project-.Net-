using System.Collections.Generic;

namespace Ocr.WebApi.Requests;

public sealed class ExportSelectedRecordsRequest
{
    public List<int> RecordIds { get; set; } =
        new();
}
