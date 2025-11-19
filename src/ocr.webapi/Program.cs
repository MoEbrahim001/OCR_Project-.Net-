using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

using Ocr.Core.Services;
using Ocr.Core.UnitOfWork;
using Ocr.Domain.Services;
using Ocr.Domain.UnitOfWork;
using Ocr.Model;
using Ocr.Core.Repositories;
using Ocr.Domain.Repositories;
using ocr.core.Services;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// Database
// --------------------
var connString = builder.Configuration.GetConnectionString("Default")
                 ?? "Server=localhost;Database=OcrDb;Trusted_Connection=True;TrustServerCertificate=True;";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(connString));

// --------------------
// CORS
// --------------------
const string AllowAngular = "AllowAngular";
const string AllowDevAll = "AllowDevAll";

builder.Services.AddCors(options =>
{
    // Strict policy for your Angular app (use this in Production)
    options.AddPolicy(AllowAngular, policy =>
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Content-Disposition"));

    // Relaxed policy for local development (Swagger, tools, etc.)
    options.AddPolicy(AllowDevAll, policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Content-Disposition"));
});


// --------------------
// Controllers + JSON
// --------------------
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opt.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        // Support DateOnly/DateOnly?
        opt.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
        opt.JsonSerializerOptions.Converters.Add(new NullableDateOnlyJsonConverter());
    });

// --------------------
// Swagger
// --------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
    c.MapType<DateOnly?>(() => new OpenApiSchema { Type = "string", Format = "date", Nullable = true });
});

// --------------------
// DI
// --------------------
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IRecordRepository, RecordRepository>();
builder.Services.AddScoped<IRecordService, RecordService>();
builder.Services.AddScoped<IdCardOcrService, IdCardOcrService>();

// HttpClient(s)
builder.Services.AddHttpClient<IOcrClient, OcrClient>();
builder.Services.AddSingleton<IOcrParser, OcrParser>();

var app = builder.Build();

// --------------------
// Pipeline
// --------------------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Optional: redirect root to swagger
app.MapGet("/", context =>
{
    context.Response.Redirect("/swagger");
    return Task.CompletedTask;
});
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    // Swagger + any origin allowed in Dev
    app.UseCors("AllowDevAll");
}
else
{
    // Only Angular allowed in Prod
    app.UseCors(AllowAngular);
}

app.MapControllers();


app.Run();


// --------------------
// JSON Converters
// --------------------
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateOnly.Parse(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format));
}

public sealed class NullableDateOnlyJsonConverter : JsonConverter<DateOnly?>
{
    private const string Format = "yyyy-MM-dd";
    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : DateOnly.Parse(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStringValue(value.Value.ToString(Format));
    }
}
