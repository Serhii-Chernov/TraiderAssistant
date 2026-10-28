using Microsoft.EntityFrameworkCore;
using TechnicalAnalysis.Domain;
using TechnicalAnalysis.Domain.Services;
using TraiderAssistant.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddSingleton<StrategyProviderService>();
builder.Services.AddSingleton<TechnicalAnalysisService>();
builder.Services.AddSingleton<BinanceService>();
builder.Services.AddScoped<BacktestService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<MachineLearningService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var strategyProvider = scope.ServiceProvider.GetRequiredService<StrategyProviderService>();
    await strategyProvider.LoadStrategiesFromDatabaseAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();


public partial class Program { }
