using System.Threading.Tasks;

namespace CINEMA.Services
{
    public interface IMovieService
    {
        // Phải đảm bảo chỗ này là (int movieId)
        Task UpdateMovieBayesianRatingAsync(int movieId);
        Task UpdateMovieBayesianRatingAsync(object movieId);
        Task UpdateUserReputationAsync(int customerId);
    }
}