using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SECAnalyzer.Database.Models;

namespace SECAnalyzer.Database.ApplicationDbContext
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            optionsBuilder.UseSqlite("Data Source=SQLLiteDatabase.db");

            return new AppDbContext(optionsBuilder.Options);
        }
    }

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<DocumentItem> Documents { get; set; }
    }
}
