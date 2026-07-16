using CINEMA.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace CINEMA.Services
{
    public class MovieService : IMovieService
    {
        private readonly CinemaContext _context;

        public MovieService()
        {
        }

        public MovieService(CinemaContext context)
        {
            _context = context;
        }

        // Phải đảm bảo chỗ này cũng là (int movieId)
        public async Task UpdateMovieBayesianRatingAsync(int movieId)
        {
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie == null) return;

            var movieReviews = await _context.Reviews
                                             .Where(r => r.MovieId == movieId && r.IsHidden != true)
                                             .ToListAsync();

            movie.VoteCount = movieReviews.Count;
            movie.RatingAverage = movie.VoteCount > 0 ? movieReviews.Average(r => r.Rating) : 0;

            double m = 5.0;
            var totalReviews = _context.Reviews.Where(r => r.IsHidden != true);
            double C = await totalReviews.AnyAsync() ? await totalReviews.AverageAsync(r => r.Rating) : 0;

            if (movie.VoteCount == 0)
            {
                movie.BayesianRating = 0;
            }
            else
            {
                double v = movie.VoteCount;
                double R = movie.RatingAverage;
                movie.BayesianRating = ((v / (v + m)) * R) + ((m / (v + m)) * C);
            }

            _context.Movies.Update(movie);
            await _context.SaveChangesAsync();
        }
        public async Task UpdateUserReputationAsync(int customerId)
        {
            // Tìm khách hàng
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null) return;

            // Email của khách ẩn danh không bị trừ điểm uy tín
            if (customer.Email == "anonymous@cinezone.com")
            {
                customer.ReputationScore = 100;
                _context.Customers.Update(customer);
                await _context.SaveChangesAsync();
                return;
            }

            // Đếm số lượng review bị báo cáo của user này
            int reportCount = await _context.Reviews
                                            .CountAsync(r => r.CustomerId == customerId && r.HasReport == true);

            // Đếm số lượng review bị Admin ẩn đi của user này
            int hiddenCount = await _context.Reviews
                                            .CountAsync(r => r.CustomerId == customerId && r.IsHidden == true);

            // Tính điểm uy tín: Bắt đầu từ 100%, trừ đi điểm phạt
            int reputation = 100 - (reportCount * 5) - (hiddenCount * 10);

            // Đảm bảo điểm nằm trong khoảng 0 -> 100
            if (reputation < 0) reputation = 0;
            if (reputation > 100) reputation = 100;

            customer.ReputationScore = reputation;
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
        }
        public Task UpdateMovieBayesianRatingAsync(object movieId)
        {
            throw new NotImplementedException();
        }
    }
}