using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Data
{
    public partial class EfDbContext
    {

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            Data_Mapping(modelBuilder);
            Master_Mapping(modelBuilder);
            UsersMapping(modelBuilder);
        }
    }
}
