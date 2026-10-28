using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TraiderAssistant.Infrastructure.Entities;

namespace TraiderAssistant.Infrastructure
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<OptimizationRun> OptimizationRuns { get; set; }
        public DbSet<TradeLog> TradeLogs { get; set; }
        public DbSet<ActiveStrategy> ActiveStrategies { get; set; }
    }
}
