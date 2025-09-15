using System;
using System.Collections.Generic;

namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class OrderCreateItemDto
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderCreateGiftDto
    {
        // 前端若能提供 Gift 的 Id，請填入 Id；否則以 Name 嘗試查詢 GiftId
        public int? Id { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class OrderCreateDto
    {
        // 後端會從 JWT 取得 MemberId，不再由前端傳

        // 配送：home-delivery 或 convenience-store
        public string? ShippingMethod { get; set; }

        // 宅配地址簿：若指定，後端會直接帶入該地址；若同時提供 ShippingAddress 欄位則以此為主
        public int? AddressId { get; set; }
        // 若為 true 且提供了宅配地址欄位，後端會在下單時將此地址寫入地址簿；IsDefault 會設定成此值
        public bool SaveAddressToBook { get; set; }

        // 宅配資訊
        public string? RecipientFirstName { get; set; }
        public string? RecipientLastName { get; set; }
        public string? RecipientPhone { get; set; }
        public string? RecipientEmail { get; set; }
        public string? ShippingAddress { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingDistrict { get; set; }
        public string? ShippingZipCode { get; set; }
        public string? Notes { get; set; }

        // 超商取貨（建立訂單時可先空白，選店後由 callback 更新）
        public string? LogisticsSubType { get; set; } // UNIMARTC2C/FAMIC2C/HILIFEC2C/OKMARTC2C
        public bool IsCollection { get; set; }

        // 計價
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal Total { get; set; }

        // 前端實際折抵使用的點數（以 /api/points/redeem 成功結果為準）
        public int PointsUsed { get; set; }

        public List<OrderCreateItemDto> Items { get; set; } = new();
        public List<OrderCreateGiftDto>? Gifts { get; set; } = new();
    }
}
