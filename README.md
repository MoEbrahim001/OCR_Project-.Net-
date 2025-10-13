# OCR Suite (.NET 8, Web API + Clean Architecture)

Projects:
- `ocr.model` — EF Core model & `AppDbContext` (database-first friendly)
- `ocr.viewmodel` — DTOs
- `ocr.domain` — Interfaces (repositories, services, UnitOfWork)
- `ocr.core` — Implementations (repositories, services, UnitOfWork, OCR client)
- `ocr.webapi` — Web API (controllers), hosts Swagger & CORS

Database:
- SQL Server; default connection in `src/ocr.webapi/appsettings.json`.
- Recommended DB name: **OcrDb**.

Run:
```bash
dotnet restore
dotnet build
dotnet run --project src/ocr.webapi/ocr.webapi.csproj
```
Swagger: https://localhost:7100/swagger
