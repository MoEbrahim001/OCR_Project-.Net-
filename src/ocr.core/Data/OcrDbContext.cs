using Microsoft.EntityFrameworkCore;
using Ocr.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ocr.core.Data
{
    public class OcrDbContext: DbContext
    {
        public OcrDbContext(DbContextOptions<OcrDbContext> options)
          : base(options)
        {
        }

        public DbSet<Record> Records { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            
        }
    }
}

