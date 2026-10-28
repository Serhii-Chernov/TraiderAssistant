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
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<OptimizationRun> OptimizationRuns { get; set; }
        public DbSet<TradeLog> TradeLogs { get; set; }
        public DbSet<ActiveStrategy> ActiveStrategies { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<OptimizationRun>()
                .HasIndex(run => new
                {
                    run.Asset,
                    run.Timeframe,
                    run.CandidateSetVersion,
                    run.EngineVersion,
                    run.CandidateIndex,
                    run.PeriodStart,
                    run.PeriodEnd
                })
                .IsUnique();
        }
    }
}