using CINEMA.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CINEMA.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendNewMovieNotificationAsync(List<Movie> movies, List<Customer> customers, string baseUrl);
    }
}
