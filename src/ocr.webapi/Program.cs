using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

using Ocr.Core.Abstractions;
using Ocr.Core.Services;

using Ocr.Domain.Repositories;
using Ocr.Domain.Services;
using Ocr.Domain.UnitOfWork;

using Ocr.Infrastructure.OCR;
using Ocr.Infrastructure.Persistence;
using Ocr.Infrastructure.Persistence.Repositories;
using Ocr.Infrastructure.Persistence.UnitOfWork;

var builder = WebApplication.CreateBuilder(args);


// =====================================================
// Database
// =====================================================

var connString =
    builder.Configuration.GetConnectionString("Default")
    ?? "Server=localhost;Database=OcrDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connString);
});


// =====================================================
// CORS
// =====================================================

const string AllowAngular = "AllowAngular";
const string AllowDevAll = "AllowDevAll";

builder.Services.AddCors(options =>
{
    // Production Angular policy
    options.AddPolicy(AllowAngular, policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition");
    });

    // Development policy
    options.AddPolicy(AllowDevAll, policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition");
    });
});


// =====================================================
// Controllers + JSON
// =====================================================

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            JsonNamingPolicy.CamelCase;

        options.JsonSerializerOptions.Encoder =
            JavaScriptEncoder.Create(UnicodeRanges.All);

        options.JsonSerializerOptions.Converters.Add(
            new DateOnlyJsonConverter());

        options.JsonSerializerOptions.Converters.Add(
            new NullableDateOnlyJsonConverter());
    });


// =====================================================
// Swagger
// =====================================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.MapType<DateOnly>(() =>
        new OpenApiSchema
        {
            Type = "string",
            Format = "date"
        });

    options.MapType<DateOnly?>(() =>
        new OpenApiSchema
        {
            Type = "string",
            Format = "date",
            Nullable = true
        });
});


// =====================================================
// Dependency Injection
// =====================================================

// --------------------
// Records / Persistence
// --------------------

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IRecordRepository, RecordRepository>();

builder.Services.AddScoped<IRecordService, RecordService>();


// --------------------
// OCR
// --------------------

// Contract:
// Ocr.Core.Abstractions.IOcrClient
//
// Implementation:
// Ocr.Infrastructure.OCR.OcrClient

builder.Services.AddHttpClient<IOcrClient, OcrClient>();


// OCR response parser
builder.Services.AddSingleton<IOcrParser, OcrParser>();


// =====================================================
// Build Application
// =====================================================

var app = builder.Build();


// =====================================================
// HTTP Pipeline
// =====================================================

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseSwagger();

    app.UseSwaggerUI();
}


// Redirect root URL to Swagger
app.MapGet("/", context =>
{
    context.Response.Redirect("/swagger");

    return Task.CompletedTask;
});


app.UseHttpsRedirection();


// =====================================================
// CORS Middleware
// =====================================================

if (app.Environment.IsDevelopment())
{
    app.UseCors(AllowDevAll);
}
else
{
    app.UseCors(AllowAngular);
}


// =====================================================
// Controllers
// =====================================================

app.MapControllers();


// =====================================================
// Run
// =====================================================

app.Run();


// =====================================================
// JSON Converters
// =====================================================

public sealed class DateOnlyJsonConverter
    : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return DateOnly.Parse(reader.GetString()!);
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateOnly value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(
            value.ToString(Format));
    }
}


public sealed class NullableDateOnlyJsonConverter
    : JsonConverter<DateOnly?>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return DateOnly.Parse(
            reader.GetString()!);
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateOnly? value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(
            value.Value.ToString(Format));
    }
}