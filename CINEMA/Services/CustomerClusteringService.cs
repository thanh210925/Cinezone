using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CINEMA.Models;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Services
{
    public class CustomerDataPoint
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;

        // Raw Features
        public double TicketsPerMonth { get; set; }
        public double ComboSpending { get; set; }
        public double WeekendRatio { get; set; }

        // Normalized Features for K-Means [0.0, 1.0]
        public double[] NormalizedFeatures { get; set; } = Array.Empty<double>();

        public int ClusterId { get; set; } = -1;
    }

    public class ClusterProfile
    {
        public int ClusterId { get; set; }
        public string Label { get; set; } = string.Empty;
        public double AvgTicketsPerMonth { get; set; }
        public double AvgComboSpending { get; set; }
        public double AvgWeekendRatio { get; set; }
        public int CustomerCount { get; set; }
        public double Percentage { get; set; }
        public string MarketingStrategy { get; set; } = string.Empty;
    }

    public class ClusteringResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int K { get; set; }
        public List<CustomerDataPoint> Customers { get; set; } = new();
        public List<ClusterProfile> ClusterProfiles { get; set; } = new();
    }

    public class CustomerClusteringService
    {
        private readonly CinemaContext _context;

        public CustomerClusteringService(CinemaContext context)
        {
            _context = context;
        }

        public async Task<ClusteringResult> ClusterCustomersAsync(int k = 3)
        {
            var result = new ClusteringResult { K = k };

            try
            {
                // 1. Fetch active customers with their transactions
                var dbCustomers = await _context.Customers
                    .Include(c => c.Orders)
                        .ThenInclude(o => o.Tickets)
                            .ThenInclude(t => t.Showtime)
                    .Include(c => c.Orders)
                        .ThenInclude(o => o.OrderCombos)
                    .ToListAsync();

                // 2. Map and extract features
                var points = new List<CustomerDataPoint>();
                var now = DateTime.Now;

                foreach (var c in dbCustomers)
                {
                    // Only count orders that are Paid (Status contains 'thanh toán')
                    var paidOrders = c.Orders
                        .Where(o => o.Status != null && o.Status.ToLower().Contains("thanh toán"))
                        .ToList();

                    var totalTickets = paidOrders.SelectMany(o => o.Tickets).Count();

                    // Calculate months since account creation
                    var createdDate = c.CreatedAt ?? now.AddMonths(-1);
                    double months = (now - createdDate).TotalDays / 30.0;
                    if (months < 1.0) months = 1.0;

                    double ticketsPerMonth = totalTickets / months;
                    double comboSpending = paidOrders.Sum(o => o.OrderCombos.Sum(oc => (double)((oc.UnitPrice ?? 0) * (oc.Quantity ?? 0))));

                    // Calculate weekend ratio (Saturday = DayOfWeek.Saturday, Sunday = DayOfWeek.Sunday)
                    int weekendTickets = paidOrders.SelectMany(o => o.Tickets)
                        .Count(t => t.Showtime != null && t.Showtime.StartTime != null &&
                                    (t.Showtime.StartTime.Value.DayOfWeek == DayOfWeek.Saturday ||
                                     t.Showtime.StartTime.Value.DayOfWeek == DayOfWeek.Sunday));

                    double weekendRatio = totalTickets == 0 ? 0.0 : (double)weekendTickets / totalTickets;

                    points.Add(new CustomerDataPoint
                    {
                        CustomerId = c.CustomerId,
                        FullName = c.FullName,
                        Email = c.Email,
                        TicketsPerMonth = ticketsPerMonth,
                        ComboSpending = comboSpending,
                        WeekendRatio = weekendRatio
                    });
                }

                // 3. Check if there is enough data for clustering
                if (points.Count < k)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Hệ thống hiện có {points.Count} khách hàng, không đủ để phân tách thành {k} cụm. Vui lòng thêm khách hàng mới hoặc chọn số cụm nhỏ hơn.";
                    return result;
                }

                // 4. Min-Max Normalization
                double maxTickets = points.Max(p => p.TicketsPerMonth);
                double minTickets = points.Min(p => p.TicketsPerMonth);
                double maxCombo = points.Max(p => p.ComboSpending);
                double minCombo = points.Min(p => p.ComboSpending);
                double maxWeekend = points.Max(p => p.WeekendRatio);
                double minWeekend = points.Min(p => p.WeekendRatio);

                foreach (var p in points)
                {
                    double normTickets = (maxTickets == minTickets) ? 0.0 : (p.TicketsPerMonth - minTickets) / (maxTickets - minTickets);
                    double normCombo = (maxCombo == minCombo) ? 0.0 : (p.ComboSpending - minCombo) / (maxCombo - minCombo);
                    double normWeekend = (maxWeekend == minWeekend) ? 0.0 : (p.WeekendRatio - minWeekend) / (maxWeekend - minWeekend);

                    p.NormalizedFeatures = new double[] { normTickets, normCombo, normWeekend };
                }

                // 5. Run K-Means Clustering
                RunKMeans(points, k);

                // 6. Calculate Cluster Profiles (centroids and stats)
                var profiles = new List<ClusterProfile>();
                for (int i = 0; i < k; i++)
                {
                    var clusterPoints = points.Where(p => p.ClusterId == i).ToList();
                    var profile = new ClusterProfile
                    {
                        ClusterId = i,
                        CustomerCount = clusterPoints.Count,
                        Percentage = points.Count == 0 ? 0.0 : Math.Round(clusterPoints.Count * 100.0 / points.Count, 1)
                    };

                    if (clusterPoints.Any())
                    {
                        profile.AvgTicketsPerMonth = Math.Round(clusterPoints.Average(p => p.TicketsPerMonth), 2);
                        profile.AvgComboSpending = Math.Round(clusterPoints.Average(p => p.ComboSpending), 0);
                        profile.AvgWeekendRatio = Math.Round(clusterPoints.Average(p => p.WeekendRatio), 2);
                    }

                    profiles.Add(profile);
                }

                // 7. Dynamic Labeling of Clusters
                LabelClusters(profiles);

                result.Success = true;
                result.Customers = points;
                result.ClusterProfiles = profiles;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Lỗi tính toán phân cụm: {ex.Message}";
            }

            return result;
        }

        private void RunKMeans(List<CustomerDataPoint> points, int k, int maxIterations = 100)
        {
            var rand = new Random(42); // Seeded for deterministic runs

            // Initialize centroids using k random distinct points
            var centroids = points.Select(p => p.NormalizedFeatures.ToArray())
                                  .OrderBy(x => rand.Next())
                                  .Take(k)
                                  .ToList();

            bool changed = true;
            int iter = 0;

            while (changed && iter < maxIterations)
            {
                changed = false;
                iter++;

                // Assign each point to the nearest centroid
                foreach (var p in points)
                {
                    int bestCluster = 0;
                    double minDistance = double.MaxValue;

                    for (int i = 0; i < k; i++)
                    {
                        double dist = GetDistance(p.NormalizedFeatures, centroids[i]);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            bestCluster = i;
                        }
                    }

                    if (p.ClusterId != bestCluster)
                    {
                        p.ClusterId = bestCluster;
                        changed = true;
                    }
                }

                // Recompute centroids as mean of all assigned points
                for (int i = 0; i < k; i++)
                {
                    var clusterPoints = points.Where(p => p.ClusterId == i).ToList();
                    if (clusterPoints.Any())
                    {
                        int featuresCount = centroids[i].Length;
                        var newCentroid = new double[featuresCount];
                        for (int f = 0; f < featuresCount; f++)
                        {
                            newCentroid[f] = clusterPoints.Average(p => p.NormalizedFeatures[f]);
                        }
                        centroids[i] = newCentroid;
                    }
                }
            }
        }

        private double GetDistance(double[] f1, double[] f2)
        {
            double sum = 0;
            for (int i = 0; i < f1.Length; i++)
            {
                double diff = f1[i] - f2[i];
                sum += diff * diff;
            }
            return Math.Sqrt(sum);
        }

        private void LabelClusters(List<ClusterProfile> profiles)
        {
            // Reset labels
            foreach (var p in profiles)
            {
                p.Label = string.Empty;
            }

            // 1. Identify "Mọt phim" -> Highest AvgTicketsPerMonth
            var motPhim = profiles.OrderByDescending(p => p.AvgTicketsPerMonth).First();
            motPhim.Label = "Mọt phim (Thành viên Tích cực)";
            motPhim.MarketingStrategy = "Tặng bắp nước miễn phí khi mua 3 vé; Gửi thông tin về phim bom tấn mới nhất; Nhân đôi điểm tích lũy.";

            var remaining = profiles.Where(p => string.IsNullOrEmpty(p.Label)).ToList();
            if (!remaining.Any()) return;

            // 2. Identify "Tín đồ bắp nước / Combo" -> Highest AvgComboSpending in remaining
            var comboLover = remaining.OrderByDescending(p => p.AvgComboSpending).First();
            if (comboLover.AvgComboSpending > 20000)
            {
                comboLover.Label = "Tín đồ Bắp Nước & Combo";
                comboLover.MarketingStrategy = "Gửi mã giảm giá 20% cho các Combo bắp nước mới; Ưu đãi mua vé tặng kèm nước ngọt cỡ lớn.";
                remaining.Remove(comboLover);
            }

            if (!remaining.Any()) return;

            // 3. Identify "Khách hàng cuối tuần" -> Highest AvgWeekendRatio in remaining
            var weekendViewer = remaining.OrderByDescending(p => p.AvgWeekendRatio).First();
            if (weekendViewer.AvgWeekendRatio >= 0.5)
            {
                weekendViewer.Label = "Khách hàng cuối tuần";
                weekendViewer.MarketingStrategy = "Gửi ưu đãi giảm giá vé ngày thường để kích cầu xem phim giữa tuần; Tặng voucher giảm giá suất chiếu muộn tối thứ 7.";
                remaining.Remove(weekendViewer);
            }

            // 4. Remaining fallback
            int generalCount = 1;
            foreach (var r in remaining)
            {
                if (r.AvgTicketsPerMonth < 0.5 && r.AvgComboSpending < 30000)
                {
                    r.Label = "Khách vãng lai / Ít tương tác";
                    r.MarketingStrategy = "Gửi mã giảm giá 30% vé xem phim để kích cầu quay lại rạp; Tặng voucher sinh nhật.";
                }
                else
                {
                    r.Label = $"Khách hàng phổ thông (Nhóm {generalCount++})";
                    r.MarketingStrategy = "Gửi bản tin khuyến mãi hàng tuần; Đề xuất phim hot theo thể loại quan tâm.";
                }
            }
        }
    }
}
