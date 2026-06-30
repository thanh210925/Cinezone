using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class LogController : Controller
    {
        private readonly CinemaContext _context;

        public LogController(CinemaContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var logs = await _context.ActivityLogs
                .Include(x => x.Admin)
                .OrderByDescending(x => x.LogDate)
                .ToListAsync();

            return View(logs);
        }
    }
}