using CINEMA.DTOs;
using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.CustomerApi
{
    [ApiController]
    [Route("api/combos")]
    public class CombosApiController : ControllerBase
    {
        private readonly CinemaContext _context;

        public CombosApiController(CinemaContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách Bắp & Nước (Combo) đang mở bán
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCombos()
        {
            var combos = await _context.Combos
                .Where(c => c.IsActive == true)
                .AsNoTracking()
                .Select(c => new ComboDto
                {
                    ComboId = c.ComboId,
                    Name = c.Name,
                    Description = c.Description,
                    Price = c.Price ?? 0,
                    ImageUrl = c.ImageUrl
                })
                .ToListAsync();

            return Ok(combos);
        }
    }
}
