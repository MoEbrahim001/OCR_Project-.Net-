using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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
using Ocr.Domain.Storage;

using Ocr.Infrastructure.OCR;
using Ocr.Infrastructure.Persistence;
using Ocr.Infrastructure.Persistence.UnitOfWork;
using Ocr.Infrastructure.Persistence.Repositories;
using Ocr.Infrastructure.Storage;

using ocr.infrastructure.Storage;

using Ocr.WebApi.Services;

using QuestPDF.Infrastructure;


// =====================================================
// QuestPDF
// =====================================================

QuestPDF.Settings.License =
    LicenseType.Evaluation;

QuestPDF.Settings.UseSystemFonts = true;


// =====================================================
// Builder
// =====================================================

var builder =
    WebApplication.CreateBuilder(args);


// Standalone application URL
builder.WebHost.UseUrls(
    "http://127.0.0.1:5100"
);


// =====================================================
// Database
// =====================================================

var databaseProvider =
    builder.Configuration["DatabaseProvider"]
    ?? "SqlServer";

var useSqlite =
    databaseProvider.Equals(
        "Sqlite",
        StringComparison.OrdinalIgnoreCase
    );

Console.WriteLine(
    $"DatabaseProvider = {databaseProvider}"
);

Console.WriteLine(
    $"UseSqlite = {useSqlite}"
);


if (useSqlite)
{
    var dataDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .CommonApplicationData
            ),
            "OcrSuiteDemo",
            "Data"
        );

    Directory.CreateDirectory(
        dataDirectory
    );

    var databasePath =
        Path.Combine(
            dataDirectory,
            "OcrSuite.db"
        );

    Console.WriteLine(
        $"SQLite Path = {databasePath}"
    );

    builder.Services.AddDbContext<AppDbContext>(
        options =>
        {
            options.UseSqlite(
                $"Data Source={databasePath}"
            );
        }
    );
}
else
{
    var connString =
        builder.Configuration
            .GetConnectionString("Default")
        ??
        "Server=localhost;" +
        "Database=OcrDb;" +
        "Trusted_Connection=True;" +
        "TrustServerCertificate=True;";

    builder.Services.AddDbContext<AppDbContext>(
        options =>
        {
            options.UseSqlServer(
                connString
            );
        }
    );
}


// =====================================================
// Image Storage
// =====================================================

builder.Services.Configure<ImageStorageOptions>(
    builder.Configuration
        .GetSection("ImageStorage")
);

builder.Services.AddScoped<
    IImageStorage,
    LocalImageStorage
>();


// =====================================================
// CORS
// =====================================================

const string AllowAngular =
    "AllowAngular";

const string AllowDevAll =
    "AllowDevAll";


builder.Services.AddCors(options =>
{
    // Angular development server
    options.AddPolicy(
        AllowAngular,
        policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:4200",
                    "https://localhost:4200"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders(
                    "Content-Disposition"
                );
        }
    );


    // Development policy
    options.AddPolicy(
        AllowDevAll,
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders(
                    "Content-Disposition"
                );
        }
    );
});


// =====================================================
// Controllers + JSON
// =====================================================

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options
            .JsonSerializerOptions
            .PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase;

        options
            .JsonSerializerOptions
            .Encoder =
                JavaScriptEncoder.Create(
                    UnicodeRanges.All
                );

        options
            .JsonSerializerOptions
            .Converters
            .Add(
                new DateOnlyJsonConverter()
            );

        options
            .JsonSerializerOptions
            .Converters
            .Add(
                new NullableDateOnlyJsonConverter()
            );
    });


// =====================================================
// Swagger
// =====================================================

builder.Services
    .AddEndpointsApiExplorer();


builder.Services.AddSwaggerGen(
    options =>
    {
        options.MapType<DateOnly>(
            () =>
                new OpenApiSchema
                {
                    Type = "string",
                    Format = "date"
                }
        );

        options.MapType<DateOnly?>(
            () =>
                new OpenApiSchema
                {
                    Type = "string",
                    Format = "date",
                    Nullable = true
                }
        );
    }
);


// =====================================================
// Dependency Injection
// =====================================================


// --------------------
// Records / Persistence
// --------------------

builder.Services.AddScoped<
    IUnitOfWork,
    UnitOfWork
>();

builder.Services.AddScoped<
    IRecordRepository,
    RecordRepository
>();

builder.Services.AddScoped<
    IRecordService,
    RecordService
>();


// --------------------
// OCR
// --------------------

builder.Services.AddHttpClient<
    IOcrClient,
    OcrClient
>();

builder.Services.AddSingleton<
    IOcrParser,
    OcrParser
>();


// Start Python OCR automatically
builder.Services.AddHostedService<
    PythonOcrProcessService
>();


// =====================================================
// Build Application
// =====================================================

var app =
    builder.Build();


// =====================================================
// Initialize Database
// =====================================================

if (useSqlite)
{
    using var scope =
        app.Services.CreateScope();

    var db =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    db.Database.EnsureCreated();

    Console.WriteLine(
        "SQLite database initialized."
    );
}


// =====================================================
// HTTP Pipeline
// =====================================================

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseSwagger();

    app.UseSwaggerUI();
}


// =====================================================
// HTTPS
// =====================================================

// Disabled for local standalone application.
// No development HTTPS certificate is required.

// app.UseHttpsRedirection();


// =====================================================
// Angular Static Files
// =====================================================

app.UseDefaultFiles();


// Serve Angular wwwroot
app.UseStaticFiles();


// =====================================================
// National ID Image Static Files
// =====================================================

var imageRoot =
    app.Configuration[
        "ImageStorage:RootPath"
    ];


if (!string.IsNullOrWhiteSpace(
        imageRoot
    ))
{
    imageRoot =
        Path.GetFullPath(
            imageRoot
        );


    var frontImagesPath =
        Path.Combine(
            imageRoot,
            "Front"
        );


    var backImagesPath =
        Path.Combine(
            imageRoot,
            "Back"
        );


    // Ensure directories exist
    Directory.CreateDirectory(
        frontImagesPath
    );

    Directory.CreateDirectory(
        backImagesPath
    );


    // ---------------------------------------------
    // FRONT
    //
    // URL:
    // /Front/2026/10/file.jpg
    //
    // Physical file:
    // {ImageStorage RootPath}\Front\2026\10\file.jpg
    // ---------------------------------------------

    app.UseStaticFiles(
        new StaticFileOptions
        {
            FileProvider =
                new PhysicalFileProvider(
                    frontImagesPath
                ),

            RequestPath =
                "/Front"
        }
    );


    // ---------------------------------------------
    // BACK
    //
    // URL:
    // /Back/2026/10/file.jpg
    //
    // Physical file:
    // {ImageStorage RootPath}\Back\2026\10\file.jpg
    // ---------------------------------------------

    app.UseStaticFiles(
        new StaticFileOptions
        {
            FileProvider =
                new PhysicalFileProvider(
                    backImagesPath
                ),

            RequestPath =
                "/Back"
        }
    );


    Console.WriteLine(
        $"Image Storage Root = {imageRoot}"
    );
}


// =====================================================
// CORS Middleware
// =====================================================

if (app.Environment.IsDevelopment())
{
    app.UseCors(
        AllowDevAll
    );
}
else
{
    app.UseCors(
        AllowAngular
    );
}


// =====================================================
// Controllers
// =====================================================

app.MapControllers();


// =====================================================
// Angular SPA Fallback
// =====================================================

app.MapFallbackToFile(
    "index.html"
);


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
    private const string Format =
        "yyyy-MM-dd";


    public override DateOnly Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return DateOnly.Parse(
            reader.GetString()!
        );
    }


    public override void Write(
        Utf8JsonWriter writer,
        DateOnly value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(
            value.ToString(
                Format
            )
        );
    }
}


public sealed class NullableDateOnlyJsonConverter
    : JsonConverter<DateOnly?>
{
    private const string Format =
        "yyyy-MM-dd";


    public override DateOnly? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (
            reader.TokenType
            ==
            JsonTokenType.Null
        )
        {
            return null;
        }


        return DateOnly.Parse(
            reader.GetString()!
        );
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
            value.Value.ToString(
                Format
            )
        );
    }
}