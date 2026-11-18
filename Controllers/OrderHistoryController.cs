
    using System.Security.Claims;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using TechMarket.Data;

    namespace TechMarket.Controllers
    {
        [Authorize(Roles = "Customer")]
        public class OrderHistoryController : Controller
        {
            private readonly ApplicationDbContext _context;

            public OrderHistoryController(ApplicationDbContext context)
            {
                _context = context;
            }

            // GET: OrderHistory/Index
            [HttpGet]
            public async Task<IActionResult> Index()
            {
                var userId = User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                    return Challenge();

                var orders = await _context.Orders
                    .Where(o => o.UserId == userId)
                    .Include(o => o.OrderDetails)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return View(orders);
            }

            // GET: OrderHistory/Details/5
            [HttpGet]
            public async Task<IActionResult> Details(int id)
            {
                var userId = User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                    return Challenge();

                var order = await _context.Orders
                    .Where(o => o.Id == id && o.UserId == userId)
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync();

                if (order == null)
                    return NotFound();

                return View(order);
            }

            // POST: OrderHistory/Cancel/5
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Cancel(int id)
            {
                var userId = User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                    return Challenge();

                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o =>
                        o.Id == id && o.UserId == userId);

                if (order == null)
                    return NotFound();

                // Chỉ cho phép hủy đơn chưa giao hàng
                if (order.Status != "Chờ xác nhận" &&
                    order.Status != "Đang giao hàng")
                {
                    TempData["Error"] =
                        "Đơn hàng hiện tại không thể hủy.";

                    return RedirectToAction(nameof(Details),
                        new { id });
                }

                var storeOrders = await _context.StoreOrders
                    .Where(so => so.OrderId == order.Id)
                    .ToListAsync();

                // Không hủy nếu có cửa hàng đã giao hoặc đã hủy
                if (storeOrders.Any(so =>
                    so.Status == "Đã giao hàng" ||
                    so.Status == "Đã hủy"))
                {
                    TempData["Error"] =
                        "Không thể hủy đơn hàng vì có sản phẩm đã được giao hoặc đã hủy.";

                    return RedirectToAction(nameof(Details),
                        new { id });
                }

                using var transaction =
                    await _context.Database.BeginTransactionAsync();

                try
                {
                    // Hoàn lại số lượng tồn kho
                    foreach (var detail in order.OrderDetails)
                    {
                        var variant = await _context.ProductVariants
                            .FirstOrDefaultAsync(v =>
                                v.Id == detail.ProductVariantId);

                        if (variant != null)
                        {
                            variant.Quantity += detail.Quantity;
                        }
                    }

                    await _context.SaveChangesAsync();

                    // Cập nhật lại tổng tồn kho sản phẩm
                    var productIds = order.OrderDetails
                        .Select(d => d.ProductId)
                        .Distinct()
                        .ToList();

                    foreach (var productId in productIds)
                    {
                        var product = await _context.Products
                            .FirstOrDefaultAsync(p =>
                                p.Id == productId);

                        if (product != null)
                        {
                            product.Quantity =
                                await _context.ProductVariants
                                    .Where(v => v.ProductId == productId)
                                    .SumAsync(v => v.Quantity);
                        }
                    }

                    // Cập nhật trạng thái đơn hàng
                    order.Status = "Đã hủy";

                    // Cập nhật trạng thái đơn của từng cửa hàng
                    foreach (var storeOrder in storeOrders)
                    {
                        storeOrder.Status = "Đã hủy";
                        storeOrder.UpdatedAt = DateTime.Now;
                    }

                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] =
                        "Hủy đơn hàng thành công. Số lượng sản phẩm đã được hoàn lại kho.";

                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Có lỗi xảy ra khi hủy đơn hàng. Vui lòng thử lại.";

                    return RedirectToAction(nameof(Details),
                        new { id });
                }
            }
        }
    }
