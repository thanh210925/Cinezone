using CINEMA.Models;

namespace CINEMA.ViewModels
{
    public class EmployeeDashboardViewModel
    {
        public int TotalEmployees { get; set; }

        public int ActiveEmployees { get; set; }

        public int LockedEmployees { get; set; }

        public int TotalBranches { get; set; }

        public int TotalPositions { get; set; }

        public List<string> BranchLabels { get; set; } = new();

        public List<int> BranchValues { get; set; } = new();

        public List<string> PositionLabels { get; set; } = new();

        public List<int> PositionValues { get; set; } = new();

        public List<Admin> RecentLogins { get; set; } = new();
    }
}