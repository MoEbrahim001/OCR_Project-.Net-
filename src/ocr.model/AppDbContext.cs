using Microsoft.EntityFrameworkCore;
using Ocr.Model.Entities;

namespace Ocr.Model;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Record> Records => Set<Record>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Record>().HasIndex(r => r.IdNumber);
        base.OnModelCreating(modelBuilder);
    }
}
