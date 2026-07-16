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

        public Task UpdateMovieBayesianRatingAsync(object movieId)
        {
            throw new NotImplementedException();
        }
    }
}