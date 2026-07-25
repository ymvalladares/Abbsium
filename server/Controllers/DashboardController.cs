using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Entitys;

namespace Server.Controllers
{
    
    [Authorize(Roles = "Admin")]
    public class DashboardController : Base_Control_Api
    {
        private readonly DbContext_app _context;

        public DashboardController(DbContext_app context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var totalUsers = await _context.User_Data.CountAsync();
            var totalDealers = await _context.Dealers.CountAsync();
            var totalCars = await _context.Cars.CountAsync();
            var totalOrders = await _context.Order.CountAsync();

            var orders = await _context.Order.ToListAsync();
            var totalRevenue = orders.Where(o => o.Status == "Completed").Sum(o => o.Amount);
            var activeSubscriptions = orders.Count(o => o.Status == "Completed" && o.PlanMode == "subscription" && !string.IsNullOrEmpty(o.SubscriptionId));
            var pendingOrders = orders.Count(o => o.Status == "pending" || o.Status == "Past Due");
            var failedPayments = orders.Count(o => o.Status == "Payment Failed");
            var cancelledSubscriptions = orders.Count(o => o.Status == "Cancelled");

            var now = DateTime.UtcNow;
            var monthlyRevenue = orders
                .Where(o => o.Status == "Completed" && o.CreatedAt.Month == now.Month && o.CreatedAt.Year == now.Year)
                .Sum(o => o.Amount);

            var todayRevenue = orders
                .Where(o => o.Status == "Completed" && o.CreatedAt.Date == now.Date)
                .Sum(o => o.Amount);

            var todayOrders = orders
                .Count(o => o.CreatedAt.Date == now.Date);

            var monthlyOrders = orders
                .Count(o => o.CreatedAt.Month == now.Month && o.CreatedAt.Year == now.Year);

            var newUsersThisMonth = await _context.User_Data
                .CountAsync(u => u.CreatedAt.Month == now.Month && u.CreatedAt.Year == now.Year);

            var activeDealers = await _context.Dealers.CountAsync(d => d.IsActive);

            return Ok(new
            {
                totalUsers,
                totalDealers,
                totalCars,
                totalOrders,
                totalRevenue,
                activeSubscriptions,
                pendingOrders,
                failedPayments,
                cancelledSubscriptions,
                monthlyRevenue,
                todayRevenue,
                todayOrders,
                monthlyOrders,
                newUsersThisMonth,
                activeDealers
            });
        }

        [HttpGet("revenue-by-month")]
        public async Task<IActionResult> GetRevenueByMonth()
        {
            var orders = await _context.Order
                .Where(o => o.Status == "Completed")
                .ToListAsync();

            var monthlyData = orders
                .GroupBy(o => new { Year = o.CreatedAt.Year, Month = o.CreatedAt.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    revenue = g.Sum(o => o.Amount),
                    orders = g.Count()
                })
                .ToList();

            return Ok(monthlyData);
        }

        [HttpGet("recent-orders")]
        public async Task<IActionResult> GetRecentOrders([FromQuery] int count = 5)
        {
            var orders = await _context.Order
                .Include(o => o.User_data)
                .OrderByDescending(o => o.CreatedAt)
                .Take(count)
                .Select(o => new
                {
                    o.Id,
                    o.Amount,
                    o.Status,
                    o.PlanMode,
                    o.ServiceType,
                    o.CreatedAt,
                    userName = o.User_data.UserName,
                    userEmail = o.User_data.Email
                })
                .ToListAsync();

            return Ok(orders);
        }

        [HttpGet("plan-distribution")]
        public async Task<IActionResult> GetPlanDistribution()
        {
            var orders = await _context.Order
                .Where(o => o.Status == "Completed")
                .ToListAsync();

            var distribution = orders
                .GroupBy(o => o.ServiceType)
                .Select(g => new
                {
                    plan = g.Key,
                    count = g.Count(),
                    revenue = g.Sum(o => o.Amount)
                })
                .OrderByDescending(x => x.count)
                .ToList();

            return Ok(distribution);
        }

        [HttpGet("user-growth")]
        public async Task<IActionResult> GetUserGrowth()
        {
            var users = await _context.User_Data.ToListAsync();

            var growth = users
                .GroupBy(u => new { Year = u.CreatedAt.Year, Month = u.CreatedAt.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    newUsers = g.Count()
                })
                .ToList();

            return Ok(growth);
        }
    }
}
