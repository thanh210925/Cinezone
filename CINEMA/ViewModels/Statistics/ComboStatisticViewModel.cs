using Microsoft.AspNetCore.Mvc;

namespace CINEMA.ViewModels
{
    public class ComboStatisticViewModel
    {
        public string ComboName { get; set; } = "";

        public int QuantitySold { get; set; }

        public decimal Revenue { get; set; }

        public double Percentage { get; set; }
    }
}
