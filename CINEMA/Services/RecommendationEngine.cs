using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CINEMA.Models;
using Newtonsoft.Json;

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
        private readonly GeminiService _geminiService;
        
        private static List<AssociationRule> _cachedRules = new List<AssociationRule>();
        private static DateTime _lastRun = DateTime.MinValue;
        private static readonly object _lock = new object();

        // Bộ nhớ đệm cho Hộp đen Gemini AI (lưu trong 5 phút để tránh gọi API liên tục làm chậm trang)
        private static readonly Dictionary<string, (List<int> MovieIds, DateTime CachedAt)> _geminiCache = 
            new Dictionary<string, (List<int> MovieIds, DateTime CachedAt)>();
        private static readonly object _cacheLock = new object();

        public RecommendationEngine(CinemaContext context, GeminiService geminiService)
        {
            _context = context;
            _geminiService = geminiService;
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

                    // Chỉ sinh luật nếu vế phải (RHS) chứa toàn bộ các mục Combo hoặc toàn bộ các mục Phim để phục vụ gợi ý
                    bool isAllCombo = rhs.All(x => x.StartsWith("Combo_"));
                    bool isAllMovie = rhs.All(x => x.StartsWith("Movie_"));
                    if (!isAllCombo && !isAllMovie)
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

        public async Task<List<Movie>> GetRecommendedMovies(int? customerId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var thirtyDaysAgo = today.AddDays(-30);

            // 1. Lấy danh sách phim đã xem/đã mua của khách hàng này để loại trừ
            var viewedMovieIds = new HashSet<int>();
            var watchedMovieTitles = new List<string>();
            if (customerId.HasValue)
            {
                var views = _context.UserMovieViews
                    .Where(v => v.CustomerId == customerId.Value)
                    .Select(v => new { v.MovieId, v.Movie.Title })
                    .ToList();
                foreach (var v in views)
                {
                    viewedMovieIds.Add(v.MovieId);
                    if (!watchedMovieTitles.Contains(v.Title))
                    {
                        watchedMovieTitles.Add(v.Title);
                    }
                }

                // Thêm các phim khách hàng đã tương tác gần đây (từ UserActivityLogs)
                var interactedTitles = _context.UserActivityLogs
                    .Where(l => l.CustomerId == customerId.Value && l.MovieId != null)
                    .Select(l => l.Movie.Title)
                    .Distinct()
                    .ToList();

                foreach (var title in interactedTitles)
                {
                    if (!watchedMovieTitles.Contains(title))
                    {
                        watchedMovieTitles.Add(title);
                    }
                }
            }

            // 2. Lấy danh sách thể loại yêu thích (Top 3) dựa trên lịch sử hoạt động
            var targetGenreIds = new List<int>();
            var userInteractedItems = new HashSet<string>(); // Lưu cả các phim/thể loại người dùng đã tương tác để khớp vế LHS của Apriori

            if (customerId.HasValue)
            {
                // Lấy hoạt động của khách hàng này
                var activityLogs = _context.UserActivityLogs
                    .Where(l => l.CustomerId == customerId.Value)
                    .Include(l => l.Movie)
                        .ThenInclude(m => m.Genres)
                    .Include(l => l.Genre)
                    .ToList();

                foreach (var log in activityLogs)
                {
                    if (log.MovieId.HasValue)
                    {
                        userInteractedItems.Add($"Movie_{log.MovieId.Value}");
                        if (log.Movie.Genres != null)
                        {
                            foreach (var g in log.Movie.Genres)
                            {
                                userInteractedItems.Add($"Genre_{g.GenreId}");
                            }
                        }
                    }
                    if (log.GenreId.HasValue)
                    {
                        userInteractedItems.Add($"Genre_{log.GenreId.Value}");
                    }
                }

                // Top 3 thể loại của riêng khách hàng
                targetGenreIds = activityLogs
                    .SelectMany(l => {
                        var list = new List<Genre>();
                        if (l.Genre != null) list.Add(l.Genre);
                        if (l.Movie?.Genres != null) list.AddRange(l.Movie.Genres);
                        return list;
                    })
                    .GroupBy(g => g.GenreId)
                    .OrderByDescending(group => group.Count())
                    .Select(group => group.Key)
                    .Take(3)
                    .ToList();
            }

            // Nếu không đăng nhập hoặc chưa có lịch sử, lấy xu hướng chung
            if (!targetGenreIds.Any())
            {
                var generalLogs = _context.UserActivityLogs
                    .Where(l => l.MovieId != null || l.GenreId != null)
                    .Include(l => l.Movie)
                        .ThenInclude(m => m.Genres)
                    .Include(l => l.Genre)
                    .ToList();

                targetGenreIds = generalLogs
                    .SelectMany(l => {
                        var list = new List<Genre>();
                        if (l.Genre != null) list.Add(l.Genre);
                        if (l.Movie?.Genres != null) list.AddRange(l.Movie.Genres);
                        return list;
                    })
                    .GroupBy(g => g.GenreId)
                    .OrderByDescending(group => group.Count())
                    .Select(group => group.Key)
                    .Take(3)
                    .ToList();
            }

            // Nếu khách hàng chưa đăng nhập hoặc không có tương tác cá nhân, sử dụng xu hướng chung để khớp Apriori
            if (userInteractedItems.Count == 0)
            {
                foreach (var gId in targetGenreIds)
                {
                    userInteractedItems.Add($"Genre_{gId}");
                }
            }

            // Lấy tất cả phim đang chiếu/sắp chiếu và chưa xem để chấm điểm
            var allMovies = _context.Movies
                .Include(m => m.Genres)
                .Where(m => m.IsActive == true && m.ReleaseDate.HasValue && m.ReleaseDate <= today && !viewedMovieIds.Contains(m.MovieId))
                .ToList();

            // -----------------------------------------------------------------
            // HỘP ĐEN (BLACK BOX - GEMINI AI) GỢI Ý
            // -----------------------------------------------------------------
            var geminiRecommendedIds = new List<int>();
            if (customerId.HasValue && watchedMovieTitles.Any() && allMovies.Any())
            {
                string cacheKey = $"user_{customerId.Value}";
                bool gotCache = false;

                lock (_cacheLock)
                {
                    if (_geminiCache.TryGetValue(cacheKey, out var cacheEntry) && DateTime.Now - cacheEntry.CachedAt < TimeSpan.FromMinutes(5))
                    {
                        geminiRecommendedIds = cacheEntry.MovieIds;
                        gotCache = true;
                    }
                }

                if (!gotCache)
                {
                    var moviesContext = string.Join("\n", allMovies.Select(m => $"{m.MovieId} - {m.Title} ({string.Join(", ", m.Genres.Select(g => g.Name))})"));
                    var historyContext = string.Join(", ", watchedMovieTitles);

                    string prompt = $@"
Bạn là hệ thống gợi ý phim AI (hộp đen) cho rạp phim CineZone.
Dưới đây là danh sách các phim đang chiếu tại rạp của chúng tôi:
{moviesContext}

Dưới đây là danh sách các bộ phim khách hàng này đã từng xem hoặc quan tâm:
{historyContext}

Hãy phân tích sở thích ẩn, chiều sâu nội dung phim họ thích để chọn ra tối đa 4 bộ phim phù hợp nhất trong danh sách đang chiếu.
Trả về duy nhất một mảng JSON chứa các MovieId được chọn, ví dụ: [3037, 3039]
Chỉ trả về JSON, không giải thích gì thêm.";

                    try
                    {
                        var rawResponse = await _geminiService.Ask(prompt);
                        var cleanJson = ExtractJsonArray(rawResponse);
                        if (!string.IsNullOrEmpty(cleanJson))
                        {
                            var ids = JsonConvert.DeserializeObject<List<int>>(cleanJson);
                            if (ids != null)
                            {
                                geminiRecommendedIds = ids;
                                lock (_cacheLock)
                                {
                                    _geminiCache[cacheKey] = (geminiRecommendedIds, DateTime.Now);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Lỗi gọi Gemini AI gợi ý phim: " + ex.Message);
                    }
                }
            }

            // 3. Khớp luật Apriori cho phim (Hộp trắng)
            var rules = GetRules();
            var aprioriMovieScores = new Dictionary<int, double>(); // MovieId -> Max Confidence

            if (userInteractedItems.Any())
            {
                foreach (var rule in rules)
                {
                    // Nếu LHS chứa các phần tử khách hàng đã tương tác (như xem phim hay quan tâm thể loại đó)
                    if (rule.LHS.Any(item => userInteractedItems.Contains(item)))
                    {
                        foreach (var rhsItem in rule.RHS)
                        {
                            if (rhsItem.StartsWith("Movie_") && int.TryParse(rhsItem.Substring(6), out int mId))
                            {
                                if (!aprioriMovieScores.ContainsKey(mId) || aprioriMovieScores[mId] < rule.Confidence)
                                {
                                    aprioriMovieScores[mId] = rule.Confidence;
                                }
                            }
                        }
                    }
                }
            }

            var scoredMovies = new List<(Movie Movie, double Score)>();

            foreach (var movie in allMovies)
            {
                double score = 0;

                // A. Điểm từ luật Apriori (Hộp trắng)
                if (aprioriMovieScores.TryGetValue(movie.MovieId, out double aprioriConf))
                {
                    score += aprioriConf * 10.0;
                }

                // B. Điểm từ Hộp đen (Gemini AI)
                if (geminiRecommendedIds.Contains(movie.MovieId))
                {
                    score += 8.0;
                }

                // C. Điểm cá nhân hóa cho Phim mới (nếu được phát hành trong vòng 30 ngày qua)
                bool isNewMovie = movie.ReleaseDate.HasValue && movie.ReleaseDate.Value >= thirtyDaysAgo;
                if (isNewMovie)
                {
                    // Đếm số lượng thể loại của phim mới trùng khớp với các thể loại yêu thích (Top 3)
                    int matchingGenresCount = movie.Genres.Count(g => targetGenreIds.Contains(g.GenreId));
                    if (matchingGenresCount > 0)
                    {
                        // Phim mới và có thể loại yêu thích của người dùng -> tăng độ ưu tiên mạnh mẽ
                        score += matchingGenresCount * 5.0; 
                    }
                }

                if (score > 0)
                {
                    scoredMovies.Add((movie, score));
                }
            }

            // Sắp xếp các phim được chấm điểm giảm dần theo Score, sau đó đến ngày phát hành mới nhất
            var recommended = scoredMovies
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Movie.ReleaseDate)
                .Select(x => x.Movie)
                .Take(8)
                .ToList();

            return recommended;
        }

        private string ExtractJsonArray(string rawResponse)
        {
            if (string.IsNullOrEmpty(rawResponse)) return "";
            try
            {
                dynamic jsonObj = JsonConvert.DeserializeObject(rawResponse);
                string text = jsonObj.candidates[0].content.parts[0].text;
                text = text.Trim();
                
                int startIdx = text.IndexOf('[');
                int endIdx = text.LastIndexOf(']');
                
                if (startIdx >= 0 && endIdx > startIdx)
                {
                    return text.Substring(startIdx, endIdx - startIdx + 1);
                }
            }
            catch
            {
                int startIdx = rawResponse.IndexOf('[');
                int endIdx = rawResponse.LastIndexOf(']');
                if (startIdx >= 0 && endIdx > startIdx)
                {
                    return rawResponse.Substring(startIdx, endIdx - startIdx + 1);
                }
            }
            return "";
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
