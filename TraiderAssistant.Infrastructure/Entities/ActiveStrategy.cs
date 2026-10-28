using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraiderAssistant.Infrastructure.Entities
{
    public class ActiveStrategy
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Asset { get; set; }

        [Required]
        public string Timeframe { get; set; }

        public string IndicatorWeightsJson { get; set; }

        public int BuyThreshold { get; set; }
        public int SellThreshold { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
    }
}
