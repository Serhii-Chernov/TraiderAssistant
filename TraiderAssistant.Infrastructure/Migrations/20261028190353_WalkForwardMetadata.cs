using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraiderAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WalkForwardMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CandidateIndex",
                table: "OptimizationRuns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CandidateSetVersion",
                table: "OptimizationRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EngineVersion",
                table: "OptimizationRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossLoss",
                table: "OptimizationRuns",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossProfit",
                table: "OptimizationRuns",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                table: "OptimizationRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                table: "OptimizationRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitFactor",
                table: "OptimizationRuns",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SharpeRatio",
                table: "OptimizationRuns",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Slippage",
                table: "OptimizationRuns",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TradingFee",
                table: "OptimizationRuns",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CandidateSetVersion",
                table: "ActiveStrategies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectionMethod",
                table: "ActiveStrategies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrainingEnd",
                table: "ActiveStrategies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrainingStart",
                table: "ActiveStrategies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WalkForwardScore",
                table: "ActiveStrategies",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationRuns_Asset_Timeframe_CandidateSetVersion_Engine~",
                table: "OptimizationRuns",
                columns: new[] { "Asset", "Timeframe", "CandidateSetVersion", "EngineVersion", "CandidateIndex", "PeriodStart", "PeriodEnd" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OptimizationRuns_Asset_Timeframe_CandidateSetVersion_Engine~",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "CandidateIndex",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "CandidateSetVersion",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "EngineVersion",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "GrossLoss",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "GrossProfit",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "ProfitFactor",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "SharpeRatio",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "Slippage",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "TradingFee",
                table: "OptimizationRuns");

            migrationBuilder.DropColumn(
                name: "CandidateSetVersion",
                table: "ActiveStrategies");

            migrationBuilder.DropColumn(
                name: "SelectionMethod",
                table: "ActiveStrategies");

            migrationBuilder.DropColumn(
                name: "TrainingEnd",
                table: "ActiveStrategies");

            migrationBuilder.DropColumn(
                name: "TrainingStart",
                table: "ActiveStrategies");

            migrationBuilder.DropColumn(
                name: "WalkForwardScore",
                table: "ActiveStrategies");
        }
    }
}
