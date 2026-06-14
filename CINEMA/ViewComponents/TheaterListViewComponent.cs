using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CINEMA.Models;

namespace CINEMA.ViewComponents
{
    public class TheaterListViewComponent : ViewComponent
    {
        private readonly CinemaContext _context;

        public TheaterListViewComponent(CinemaContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Lấy danh sách rạp từ DB
            var theaters = await _context.Theaters.ToListAsync();
            return View(theaters);
        }
    }
}