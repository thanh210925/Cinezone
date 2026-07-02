using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CINEMA.Models;

namespace CINEMA.Services
{
    public class AssociationRule
    {
        public List<string> LHS { get; set; } = new List<string>();
        public List<string> RHS { get; set; } = new List<string>();
        public double Support { get; set; }
        public double Confidence { get; set; }
        public double Lift { get; set; }
    }

    public class ComboRecommendation
    {
        public Combo Combo { get; set; } = null!;
        public bool IsPurchasedBefore { get; set; }
        public int PurchaseCount { get; set; }
        public bool IsAprioriRecommended { get; set; }
        public double AprioriConfidence { get; set; }
        public double PriorityScore { get; set; }
    }

    public class RecommendationEngine
    {
        private readonly CinemaContext _context;
        
        private static List<AssociationRule> _cachedRules = new List<AssociationRule>();
        private static DateTime _lastRun = DateTime.MinValue;
        private static readonly object _lock = new object();

        public RecommendationEngine(CinemaContext context)
        {
            _context = context;
        }

        public List<AssociationRule> GetRules(bool forceRefresh = false)
        {
            if (forceRefresh || DateTime.Now - _lastRun > TimeSpan.FromMinutes(10) || !_cachedRules.Any())
            {
                lock (_lock)
                {
                    if (forceRefresh || DateTime.Now - _lastRun > TimeSpan.FromMinutes(10) || !_cachedRules.Any())
                    {
                        try
                        {
                            _cachedRules = MineRules();
                            _lastRun = DateTime.Now;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Error mining association rules: " + ex.Message);
                        }
                    }
                }
            }
            return _cachedRules;
        }

        private List<AssociationRule> MineRules()
        {
            // 1. Lấy tất cả các hóa đơn đã thanh toán hoặc hoàn thành
            var orders = _context.Orders
                .Include(o => o.OrderCombos)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                            .ThenInclude(m => m.Genres)
                .Where(o => o.Status == "Paid" || o.Status == "Completed" || o.PaidAt != null)
                .ToList();

            var transactions = new List<HashSet<string>>();

            foreach (var o in orders)
            {
                var itemset = new HashSet<string>();

                // Thêm các Combo mua trong hóa đơn
                foreach (var oc in o.OrderCombos)
                {
                    if (oc.ComboId.HasValue)
                    {
                        itemset.Add($"Combo_{oc.ComboId.Value}");
                    }
                }

                // Thêm các Phim và Thể loại phim trong hóa đơn
                foreach (var t in o.Tickets)
                {
                    if (t.Showtime?.Movie != null)
                    {
                        itemset.Add($"Movie_{t.Showtime.MovieId}");
                        if (t.Showtime.Movie.Genres != null)
                        {
                            foreach (var g in t.Showtime.Movie.Genres)
                            {
                                itemset.Add($"Genre_{g.GenreId}");
                            }
                        }
                    }
                }

                if (itemset.Any())
                {
                    transactions.Add(itemset);
                }
            }

            if (transactions.Count < 3)
            {
                // Quá ít dữ liệu để chạy Apriori, trả về danh sách trống hoặc chạy với ngưỡng cực thấp để demo
                return new List<AssociationRule>();
            }

            // Thiết lập ngưỡng
            double minSupport = 0.02; // 2%
            double minConfidence = 0.1; // 10%
            
            // Chạy Apriori
            var rules = RunApriori(transactions, minSupport, minConfidence);
            return rules;
        }

        private List<AssociationRule> RunApriori(List<HashSet<string>> transactions, double minSupport, double minConfidence)
        {
            int n = transactions.Count;
            var rules = new List<AssociationRule>();

            // 1. Đếm tần suất các mục 1 phần tử
            var itemCounts = new Dictionary<string, int>();
            foreach (var t in transactions)
            {
                foreach (var item in t)
                {
                    if (!itemCounts.ContainsKey(item))
                        itemCounts[item] = 0;
                    itemCounts[item]++;
                }
            }

            // Lọc ra các Frequent 1-itemsets
            var frequent1 = itemCounts
                .Where(kvp => (double)kvp.Value / n >= minSupport)
                .ToDictionary(kvp => new HashSet<string> { kvp.Key }, kvp => kvp.Value, HashSetEqualityComparer.Instance);

            var allFrequentItemsets = new Dictionary<HashSet<string>, int>(HashSetEqualityComparer.Instance);
            foreach (var kvp in frequent1)
            {
                allFrequentItemsets[kvp.Key] = kvp.Value;
            }

            var currentFrequent = frequent1;
            int k = 2;

            while (currentFrequent.Any())
            {
                // Sinh candidates k-itemsets
                var candidates = GenerateCandidates(currentFrequent.Keys.ToList(), k);
                if (!candidates.Any())
                    break;

                // Đếm tần suất candidates trong transactions
                var candidateCounts = new Dictionary<HashSet<string>, int>(HashSetEqualityComparer.Instance);
                foreach (var c in candidates)
                {
                    candidateCounts[c] = 0;
                }

                foreach (var t in transactions)
                {
                    foreach (var c in candidates)
                    {
                        if (c.All(item => t.Contains(item)))
                        {
                            candidateCounts[c]++;
                        }
                    }
                }

                // Lọc theo minSupport
                var nextFrequent = candidateCounts
                    .Where(kvp => (double)kvp.Value / n >= minSupport)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, HashSetEqualityComparer.Instance);

                if (!nextFrequent.Any())
                    break;

                foreach (var kvp in nextFrequent)
                {
                    allFrequentItemsets[kvp.Key] = kvp.Value;
                }

                currentFrequent = nextFrequent;
                k++;
            }

            // 2. Sinh luật kết hợp từ các Frequent Itemsets
            foreach (var kvp in allFrequentItemsets)
            {
                var itemset = kvp.Key;
                int countAll = kvp.Value;

                if (itemset.Count < 2)
                    continue;

                double supportAll = (double)countAll / n;

                // Sinh tất cả các tập con
                var subsets = GetSubsets(itemset.ToList());
                foreach (var lhsList in subsets)
                {
                    if (lhsList.Count == 0 || lhsList.Count == itemset.Count)
                        continue;

                    var lhs = new HashSet<string>(lhsList);
                    var rhs = new HashSet<string>(itemset.Except(lhs));

                    // Chỉ sinh luật nếu vế phải (RHS) chứa các mục Combo để phục vụ gợi ý Combo bắp nước
                    if (!rhs.All(x => x.StartsWith("Combo_")))
                        continue;

                    if (allFrequentItemsets.TryGetValue(lhs, out int countLhs))
                    {
                        double confidence = (double)countAll / countLhs;
                        if (confidence >= minConfidence)
                        {
                            double supportLhs = (double)countLhs / n;
                            var rhsSet = new HashSet<string>(rhs);
                            
                            allFrequentItemsets.TryGetValue(rhsSet, out int countRhs);
                            double supportRhs = (double)countRhs / n;

                            double lift = supportRhs > 0 ? confidence / supportRhs : 0;

                            rules.Add(new AssociationRule
                            {
                                LHS = lhs.ToList(),
                                RHS = rhs.ToList(),
                                Support = supportAll,
                                Confidence = confidence,
                                Lift = lift
                            });
                        }
                    }
                }
            }

            return rules;
        }

        private List<HashSet<string>> GenerateCandidates(List<HashSet<string>> frequentKMinus1, int k)
        {
            var candidates = new List<HashSet<string>>();
            int count = frequentKMinus1.Count;

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    var list1 = frequentKMinus1[i].OrderBy(x => x).ToList();
                    var list2 = frequentKMinus1[j].OrderBy(x => x).ToList();

                    // Join step: nếu chung k-2 phần tử đầu
                    bool canJoin = true;
                    for (int m = 0; m < k - 2; m++)
                    {
                        if (list1[m] != list2[m])
                        {
                            canJoin = false;
                            break;
                        }
                    }

                    if (canJoin)
                    {
                        var candidate = new HashSet<string>(frequentKMinus1[i]);
                        candidate.Add(list2[k - 2]);

                        // Pruning step: kiểm tra tất cả các tập con k-1 của candidate
                        if (HasFrequentSubsets(candidate, frequentKMinus1, k - 1))
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
            }

            return candidates;
        }

        private bool HasFrequentSubsets(HashSet<string> candidate, List<HashSet<string>> frequentKMinus1, int kMinus1)
        {
            var subsets = GetSubsets(candidate.ToList());
            foreach (var s in subsets)
            {
                if (s.Count == kMinus1)
                {
                    var set = new HashSet<string>(s);
                    if (!frequentKMinus1.Any(f => f.SetEquals(set)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private List<List<string>> GetSubsets(List<string> items)
        {
            var subsets = new List<List<string>>();
            int subsetCount = 1 << items.Count;
            for (int i = 0; i < subsetCount; i++)
            {
                var subset = new List<string>();
                for (int j = 0; j < items.Count; j++)
                {
                    if ((i & (1 << j)) != 0)
                    {
                        subset.Add(items[j]);
                    }
                }
                subsets.Add(subset);
            }
            return subsets;
        }

        public List<ComboRecommendation> GetRecommendedCombos(int movieId, List<int> genreIds, int? customerId)
        {
            var result = new List<ComboRecommendation>();
            var allCombos = _context.Combos.Where(c => c.IsActive == true).ToList();

            // 1. Nếu khách hàng đã đăng nhập, lấy danh sách combo khách đã từng mua
            var purchasedComboIds = new Dictionary<int, int>(); // ComboId -> Frequency
            if (customerId.HasValue)
            {
                var pastOrders = _context.Orders
                    .Where(o => o.CustomerId == customerId.Value && (o.Status == "Paid" || o.Status == "Completed" || o.PaidAt != null))
                    .Include(o => o.OrderCombos)
                    .ToList();

                foreach (var order in pastOrders)
                {
                    foreach (var oc in order.OrderCombos)
                    {
                        if (oc.ComboId.HasValue && oc.Quantity.HasValue)
                        {
                            if (!purchasedComboIds.ContainsKey(oc.ComboId.Value))
                                purchasedComboIds[oc.ComboId.Value] = 0;
                            purchasedComboIds[oc.ComboId.Value] += oc.Quantity.Value;
                        }
                    }
                }
            }

            // 2. Lấy luật từ Apriori
            var rules = GetRules();
            
            // Tìm các Combo được gợi ý bởi luật Apriori cho Movie hoặc Genres này
            var aprioriComboScores = new Dictionary<int, double>(); // ComboId -> Max Confidence/Score

            // Các khóa cần tìm ở LHS
            var searchKeys = new List<string> { $"Movie_{movieId}" };
            foreach (var gid in genreIds)
            {
                searchKeys.Add($"Genre_{gid}");
            }

            foreach (var rule in rules)
            {
                // Nếu LHS của luật khớp với phim hoặc bất kỳ thể loại nào của phim này
                if (rule.LHS.Any(item => searchKeys.Contains(item)))
                {
                    foreach (var rhsItem in rule.RHS)
                    {
                        if (rhsItem.StartsWith("Combo_") && int.TryParse(rhsItem.Substring(6), out int comboId))
                        {
                            if (!aprioriComboScores.ContainsKey(comboId) || aprioriComboScores[comboId] < rule.Confidence)
                            {
                                aprioriComboScores[comboId] = rule.Confidence;
                            }
                        }
                    }
                }
            }

            // 3. Tạo danh sách khuyến nghị
            foreach (var combo in allCombos)
            {
                var rec = new ComboRecommendation
                {
                    Combo = combo,
                    IsPurchasedBefore = purchasedComboIds.ContainsKey(combo.ComboId),
                    PurchaseCount = purchasedComboIds.ContainsKey(combo.ComboId) ? purchasedComboIds[combo.ComboId] : 0
                };

                if (aprioriComboScores.TryGetValue(combo.ComboId, out double confidence))
                {
                    rec.IsAprioriRecommended = true;
                    rec.AprioriConfidence = confidence;
                }

                // Tính điểm ưu tiên (Priority Score) để sắp xếp
                // Mua nhiều trước đây -> Ưu tiên cao nhất.
                // Được gợi ý bởi Apriori -> Ưu tiên thứ hai.
                double priority = 0;
                if (rec.IsPurchasedBefore)
                {
                    priority += 100 + rec.PurchaseCount; // Đảm bảo lớn hơn Apriori Confidence
                }
                if (rec.IsAprioriRecommended)
                {
                    priority += rec.AprioriConfidence * 10;
                }
                rec.PriorityScore = priority;

                result.Add(rec);
            }

            // Sắp xếp giảm dần theo điểm ưu tiên
            return result.OrderByDescending(r => r.PriorityScore).ToList();
        }

        // Dùng để so sánh 2 HashSet trong Dictionary
        private class HashSetEqualityComparer : IEqualityComparer<HashSet<string>>
        {
            public static readonly HashSetEqualityComparer Instance = new HashSetEqualityComparer();

            public bool Equals(HashSet<string>? x, HashSet<string>? y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                return x.SetEquals(y);
            }

            public int GetHashCode(HashSet<string> obj)
            {
                int hash = 0;
                if (obj != null)
                {
                    foreach (var item in obj)
                    {
                        hash ^= item.GetHashCode();
                    }
                }
                return hash;
            }
        }
    }
}
