using CINEMA.Models;

namespace CINEMA.Services
{
    public interface IJwtService
    {
        string GenerateCustomerToken(Customer customer);
        string GenerateAdminToken(Admin admin);
        Task<RefreshToken> GenerateAndSaveRefreshTokenAsync(int userId, string userType);
        Task<RefreshToken?> ValidateRefreshTokenAsync(string token);
        Task RevokeRefreshTokenAsync(string token);
    }
}
