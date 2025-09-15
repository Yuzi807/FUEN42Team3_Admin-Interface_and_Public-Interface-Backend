using System;
using System.Collections.Generic;

namespace FUEN42Team3.Frontend.WebApi.Dtos
{
    /// <summary>
    /// 訂單詳情資料傳輸物件
    /// </summary>
    public class OrderDetailDTO
    {
        /// <summary>
        /// 訂單編號
        /// </summary>
        public string OrderNumber { get; set; }

        /// <summary>
        /// 訂單狀態 (pending, confirmed, processing, shipped, delivered, cancelled, returned)
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 付款狀態 (pending, paid, failed, refunded)
        /// </summary>
        public string PaymentStatus { get; set; }

        /// <summary>
        /// 訂單建立時間
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 訂單項目列表
        /// </summary>
        public List<OrderItemDTO> Items { get; set; }

        /// <summary>
        /// 配送資訊
        /// </summary>
        public ShippingInfoDTO ShippingInfo { get; set; }

        /// <summary>
        /// 付款資訊
        /// </summary>
        public OrderPaymentInfoDTO PaymentInfo { get; set; }

        /// <summary>
        /// 發票資訊
        /// </summary>
        public InvoiceInfoDTO InvoiceInfo { get; set; }

        /// <summary>
        /// 價格資訊
        /// </summary>
        public OrderPricingDTO Pricing { get; set; }

        /// <summary>
        /// 訂單時間軸
        /// </summary>
        public List<OrderTimelineDTO> Timeline { get; set; }

        /// <summary>
        /// 是否可以取消訂單
        /// </summary>
        public bool CanCancel { get; set; }

        /// <summary>
        /// 是否可以申請退貨
        /// </summary>
        public bool CanReturn { get; set; }
    }

    /// <summary>
    /// 訂單項目資料傳輸物件
    /// </summary>
    public class OrderItemDTO
    {
        /// <summary>
        /// 項目ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 產品ID
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// 產品名稱
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 產品類別
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 銷售價格
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 原始價格
        /// </summary>
        public decimal? OriginalPrice { get; set; }

        /// <summary>
        /// 數量
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// 產品圖片URL
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// 已選擇的選項 (例如：顏色、尺寸等)
        /// </summary>
        public Dictionary<string, string> SelectedOptions { get; set; }
    }

    /// <summary>
    /// 訂單付款資訊資料傳輸物件
    /// </summary>
    public class OrderPaymentInfoDTO
    {
        /// <summary>
        /// 付款方式
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 信用卡資訊 (信用卡付款專用)
        /// </summary>
        public CreditCardSummaryDTO CreditCard { get; set; }
    }

    /// <summary>
    /// 信用卡摘要資料傳輸物件 (只包含部分資訊用於顯示)
    /// </summary>
    public class CreditCardSummaryDTO
    {
        /// <summary>
        /// 卡號後四碼
        /// </summary>
        public string LastFourDigits { get; set; }
    }

    /// <summary>
    /// 訂單價格資訊資料傳輸物件
    /// </summary>
    public class OrderPricingDTO
    {
        /// <summary>
        /// 商品小計
        /// </summary>
        public decimal Subtotal { get; set; }

        /// <summary>
        /// 商品折扣金額
        /// </summary>
        public decimal ItemDiscount { get; set; }

        /// <summary>
        /// 優惠券折扣金額
        /// </summary>
        public decimal CouponDiscount { get; set; }

        /// <summary>
        /// 點數折抵金額
        /// </summary>
        public decimal PointsDiscount { get; set; }

        /// <summary>
        /// 折扣總額
        /// </summary>
        public decimal TotalDiscount { get; set; }

        /// <summary>
        /// 運費
        /// </summary>
        public decimal Shipping { get; set; }

        /// <summary>
        /// 總計金額
        /// </summary>
        public decimal Total { get; set; }
    }

    /// <summary>
    /// 訂單時間軸項目資料傳輸物件
    /// </summary>
    public class OrderTimelineDTO
    {
        /// <summary>
        /// 事件標題
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 事件時間
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool Completed { get; set; }

        /// <summary>
        /// 事件描述
        /// </summary>
        public string Description { get; set; }
    }
}
