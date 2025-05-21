using Microsoft.EntityFrameworkCore;


namespace Entities.Data
{
    public partial class EfDbContext : DbContext
    {
        public EfDbContext(DbContextOptions<EfDbContext> options) : base(options) { }
    }
}
