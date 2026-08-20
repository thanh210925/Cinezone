using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class BranchController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public BranchController(CinemaContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string search)
        {
            var query = _context.Theaters.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t =>
                    t.Name.Contains(search) ||
                    (t.Address != null && t.Address.Contains(search)));
            }

            var data = await query
                .OrderBy(t => t.TheaterId)
                .ToListAsync();

            return View(data);
        }
    }
}