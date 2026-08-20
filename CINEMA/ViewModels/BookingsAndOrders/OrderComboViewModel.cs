namespace CINEMA.ViewModels
{
    public class OrderComboViewModel
    {
        public string ComboName { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public decimal? Total => (Price ?? 0) * (Quantity ?? 0);
    }
}
