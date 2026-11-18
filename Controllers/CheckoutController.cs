
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechMarket.Data;
using TechMarket.Models;
using TechMarket.ViewModels;

namespace TechMarket.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CheckoutController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Lấy giỏ hàng của khách hàng hiện tại
        private async Task<Cart?> GetCartAsync(string userId)
        {
            return await _context.Carts
                .Where(c => c.UserId == userId)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.ProductVariant)
                .FirstOrDefaultAsync();
        }

        // Chuẩn bị dữ liệu hiển thị trên trang thanh toán
        private async Task PrepareCheckoutView(Cart cart, string userId)
        {
            ViewBag.CartItems = cart.CartItems;

            ViewBag.TotalAmount = cart.CartItems.Sum(
                item => item.Product!.Price * item.Quantity);

            // Lấy danh sách địa chỉ đã lưu của khách hàng
            ViewBag.ShippingAddresses = await _context.ShippingAddresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.Id)
                .ToListAsync();
        }

        // GET: Checkout/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var cart = await GetCartAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index", "Cart");
            }

            foreach (var item in cart.CartItems)
            {
                if (item.Product == null ||
                    item.ProductVariant == null ||
                    item.ProductVariant.ProductId != item.ProductId ||
                    !item.Product.IsActive ||
                    !item.ProductVariant.IsActive ||
                    item.ProductVariant.Quantity < item.Quantity)
                {
                    TempData["Error"] =
                        "Một số sản phẩm trong giỏ hàng không còn đủ tồn kho. Vui lòng kiểm tra lại.";

                    return RedirectToAction("Index", "Cart");
                }
            }

            await PrepareCheckoutView(cart, userId);

            // Tự động lấy địa chỉ mặc định
            var defaultAddress = await _context.ShippingAddresses
                .FirstOrDefaultAsync(a =>
                    a.UserId == userId && a.IsDefault);

            var model = new CheckoutViewModel();

            if (defaultAddress != null)
            {
                model.ReceiverName = defaultAddress.ReceiverName;
                model.Phone = defaultAddress.Phone;
                model.ShippingAddress = defaultAddress.Address;
            }

            return View(model);
        }

        // POST: Checkout/PlaceOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(
            CheckoutViewModel model)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var cart = await GetCartAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index", "Cart");
            }

            // Chỉ hỗ trợ thanh toán khi nhận hàng (COD)
            if (model.PaymentMethod != "COD")
            {
                ModelState.AddModelError(
                    nameof(model.PaymentMethod),
                    "Phương thức thanh toán không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                await PrepareCheckoutView(cart, userId);
                return View("Index", model);
            }

            // Kiểm tra thông tin sản phẩm trước khi đặt hàng
            foreach (var item in cart.CartItems)
            {
                if (item.Product == null ||
                    item.ProductVariant == null ||
                    item.ProductVariant.ProductId != item.ProductId ||
                    !item.Product.IsActive ||
                    !item.ProductVariant.IsActive ||
                    item.ProductVariant.Quantity < item.Quantity)
                {
                    TempData["Error"] =
                        "Sản phẩm không còn đủ tồn kho hoặc đã ngừng kinh doanh. Vui lòng kiểm tra giỏ hàng.";

                    return RedirectToAction("Index", "Cart");
                }

                // Kiểm tra cửa hàng còn hoạt động
                bool storeIsActive = await _context.Stores
                    .AnyAsync(s =>
                        s.Id == item.Product.StoreId &&
                        s.IsActive);

                if (!storeIsActive)
                {
                    TempData["Error"] =
                        "Một cửa hàng có sản phẩm trong giỏ hàng hiện không hoạt động.";

                    return RedirectToAction("Index", "Cart");
                }
            }

            // Bắt đầu transaction
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                decimal totalAmount = cart.CartItems.Sum(
                    item => item.Product!.Price * item.Quantity);

                // Tạo đơn hàng tổng
                var order = new Order
                {
                    UserId = userId,
                    ReceiverName = model.ReceiverName,
                    Phone = model.Phone,
                    ShippingAddress = model.ShippingAddress,
                    Note = model.Note,
                    PaymentMethod = "COD",
                    Status = "Chờ xác nhận",
                    TotalAmount = totalAmount,
                    CreatedAt = DateTime.Now
                };

                _context.Orders.Add(order);

                // Lưu đơn hàng để lấy OrderId
                await _context.SaveChangesAsync();

                // Tạo chi tiết đơn hàng
                foreach (var item in cart.CartItems)
                {
                    var product = item.Product!;
                    var variant = item.ProductVariant!;

                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = product.Id,
                        ProductVariantId = variant.Id,
                        StoreId = product.StoreId,

                        ProductName = product.Name,
                        SKU = variant.SKU,
                        Size = variant.Size,
                        Color = variant.Color,

                        UnitPrice = product.Price,
                        Quantity = item.Quantity,
                        TotalPrice = product.Price * item.Quantity
                    };

                    _context.OrderDetails.Add(orderDetail);
                }

                await _context.SaveChangesAsync();

                // Tạo StoreOrder riêng cho từng cửa hàng
                var storeIds = cart.CartItems
                    .Select(item => item.Product!.StoreId)
                    .Distinct()
                    .ToList();

                foreach (var storeId in storeIds)
                {
                    var storeOrder = new StoreOrder
                    {
                        OrderId = order.Id,
                        StoreId = storeId,
                        Status = "Chờ xác nhận",
                        CreatedAt = DateTime.Now
                    };

                    _context.StoreOrders.Add(storeOrder);
                }

                await _context.SaveChangesAsync();

                // Trừ tồn kho từng biến thể
                foreach (var item in cart.CartItems)
                {
                    int affectedRows = await _context.ProductVariants
                        .Where(v =>
                            v.Id == item.ProductVariantId &&
                            v.ProductId == item.ProductId &&
                            v.IsActive &&
                            v.Quantity >= item.Quantity)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(
                                v => v.Quantity,
                                v => v.Quantity - item.Quantity));

                    if (affectedRows == 0)
                    {
                        await transaction.RollbackAsync();

                        TempData["Error"] =
                            "Tồn kho vừa thay đổi. Vui lòng kiểm tra lại giỏ hàng.";

                        return RedirectToAction("Index", "Cart");
                    }
                }

                // Cập nhật tổng tồn kho của từng sản phẩm
                var productIds = cart.CartItems
                    .Select(item => item.ProductId)
                    .Distinct()
                    .ToList();

                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToListAsync();

                foreach (var product in products)
                {
                    product.Quantity = await _context.ProductVariants
                        .Where(v => v.ProductId == product.Id)
                        .SumAsync(v => (int?)v.Quantity) ?? 0;
                }

                // Xóa sản phẩm trong giỏ hàng
                _context.CartItems.RemoveRange(cart.CartItems);

                await _context.SaveChangesAsync();

                // Hoàn tất transaction
                await transaction.CommitAsync();

                TempData["Success"] =
                    "Đặt hàng thành công! Cảm ơn bạn đã mua hàng.";

                return RedirectToAction(
                    "Index",
                    "OrderHistory");
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Đã xảy ra lỗi khi đặt hàng. Vui lòng thử lại.";

                return RedirectToAction("Index");
            }
        }
    }
}
