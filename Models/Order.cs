
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TechMarket.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Tên người nhận")]
        public string ReceiverName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        [Display(Name = "Địa chỉ giao hàng")]
        public string ShippingAddress { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Ghi chú")]
        public string? Note { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Phương thức thanh toán")]
        public string PaymentMethod { get; set; } = "COD";

        [Required]
        [StringLength(50)]
        [Display(Name = "Trạng thái đơn hàng")]
        public string Status { get; set; } = "Chờ xác nhận";

        [Range(0, 10000000000)]
        [Display(Name = "Tổng tiền")]
        public decimal TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<OrderDetail> OrderDetails { get; set; }
            = new List<OrderDetail>();
    }
}
