using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FUEN42Team3.Frontend.WebApi.Dtos
{
    /// <summary>
    /// 購物車資料傳輸物件
    /// </summary>
    public class CartDTO
    {
        /// <summary>
        /// 購物車ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 使用者ID
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// 購物車項目列表
        /// </summary>
        public List<CartItemDTO> CartItems { get; set; } = new();

        /// <summary>
        /// 商品小計總額
        /// </summary>
        public decimal Subtotal { get; set; }

        /// <summary>
        /// 原始小計總額 (未折扣的價格)
        /// </summary>
        public decimal OriginalSubtotal { get; set; }

        /// <summary>
        /// 商品折扣總額
        /// </summary>
        public decimal ItemDiscount { get; set; }

        /// <summary>
        /// 運費
        /// </summary>
        public decimal ShippingFee { get; set; }

        /// <summary>
        /// 折扣總額 (包含商品折扣、優惠券、點數)
        /// </summary>
        public decimal TotalDiscount { get; set; }

        /// <summary>
        /// 最終總計金額
        /// </summary>
        public decimal FinalTotal { get; set; }

        /// <summary>
        /// 免運費門檻
        /// </summary>
        public decimal FreeShippingThreshold { get; set; }

        /// <summary>
        /// 已套用的優惠券
        /// </summary>
        public CouponDTO? AppliedCoupon { get; set; }

        /// <summary>
        /// 使用的點數
        /// </summary>
        public int PointsToUse { get; set; }

        /// <summary>
        /// 用戶可用點數
        /// </summary>
        public int UserPoints { get; set; }

        /// <summary>
        /// 已選中的購物車項目ID列表
        /// </summary>
        public List<int> SelectedItems { get; set; } = new();

        /// <summary>
        /// 贈品促銷列表
        /// </summary>
        public List<GiftPromotionDTO> QualifiedGifts { get; set; } = new();

        public List<GiftPromotionDTO> AllGiftPromotions { get; set; } = new();
    }

    /// <summary>
    /// 購物車項目資料傳輸物件
    /// </summary>
    public class CartItemDTO
    {
        /// <summary>
        /// 購物車項目ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 產品ID
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// 產品名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 產品類別
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// 目前價格
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 原始價格（未打折）
        /// </summary>
        public decimal? OriginalPrice { get; set; }

        /// <summary>
        /// 數量
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// 庫存數量
        /// </summary>
        public int Stock { get; set; }

        /// <summary>
        /// 產品圖片URL
        /// </summary>
        public string Image { get; set; } = string.Empty;

        /// <summary>
        /// 已選擇的選項 (例如：顏色、尺寸等)
        /// </summary>
        public Dictionary<string, string> SelectedOptions { get; set; } = new();
    }

    /// <summary>
    /// 優惠券資料傳輸物件
    /// </summary>
    public class CouponDTO
    {
        /// <summary>
        /// 優惠券代碼
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 優惠券名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 折扣金額
        /// </summary>
        public decimal Discount { get; set; }

        /// <summary>
        /// 優惠券類型 (fixed: 固定金額, percentage: 百分比)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 最低消費金額
        /// </summary>
        public decimal? MinAmount { get; set; }
    }

    public class AddCartItemRequest
    {
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class UpdateQuantityRequest
    {
        public int Quantity { get; set; }
    }

    public class MergeCartItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }

        // 兼容前端可能使用的欄位名稱：qty、id
        [JsonPropertyName("qty")]
        public int? Qty
        {
            get => null;
            set { if (value.HasValue && value.Value > 0) Quantity = value.Value; }
        }

        // 有些前端可能送 id 當作 productId
        [JsonPropertyName("id")]
        public int? Id
        {
            get => null;
            set { if (value.HasValue && value.Value > 0) ProductId = value.Value; }
        }
    }

    public class MergeCartRequest
    {
        public List<MergeCartItemRequest> Items { get; set; } = new();
    }

    /// <summary>
    /// 贈品促銷資料傳輸物件
    /// </summary>
    public class GiftPromotionDTO
    {
        /// <summary>
        /// 促銷ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 促銷名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 促銷類型 (amount: 滿額, quantity: 滿件數)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 門檻 (金額或件數)
        /// </summary>
        public decimal Threshold { get; set; }

        /// <summary>
        /// 活動開始日期（可為 null）
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// 活動結束日期（可為 null）
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// 贈品列表
        /// </summary>
        public List<GiftDTO> Gifts { get; set; } = new();

        /// <summary>
        /// 促銷描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// 贈品資料傳輸物件
    /// </summary>
    public class GiftDTO
    {
        /// <summary>
        /// 贈品ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 贈品名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 贈品圖片URL
        /// </summary>
        public string Image { get; set; } = string.Empty;

        /// <summary>
        /// 贈品描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 贈品數量
        /// </summary>
        public int Quantity { get; set; }
    }
}
