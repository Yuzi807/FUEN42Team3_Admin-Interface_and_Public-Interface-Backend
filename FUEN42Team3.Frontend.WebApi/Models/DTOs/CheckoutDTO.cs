using System;
using System.Collections.Generic;

namespace FUEN42Team3.Frontend.WebApi.Dtos
{
    /// <summary>
    /// 配送通道（用來判斷是宅配還是超商取貨）
    /// </summary>
    public enum DeliveryChannel
    {
        HomeDelivery = 1,  // 宅配
        CvsPickup = 2   // 超商取貨
    }

    /// <summary>
    /// 結帳資料傳輸物件（支援宅配 & 超商取貨）
    /// </summary>
    public class CheckoutDTO
    {
        public List<CartItemDTO> CartItems { get; set; }

        /// <summary>配送方式 ID（對應你資料庫的 DeliveryMethod.Id，例如：宅配、7-11、全家…）</summary>
        public int DeliveryMethodId { get; set; }

        /// <summary>配送通道（宅配 / 超商）— 供後端驗證判斷哪組欄位必填</summary>
        public DeliveryChannel DeliveryChannel { get; set; }

        /// <summary>若為超商取貨：物流子類別（UNIMARTC2C/FAMIC2C/HILIFEC2C/OKMARTC2C）</summary>
        public string LogisticsSubType { get; set; } // 可留空給宅配

        /// <summary>是否取貨付款（超商代收）</summary>
        public bool IsCollection { get; set; }

        /// <summary>配送資訊（宅配用）</summary>
        public ShippingInfoDTO ShippingInfo { get; set; }

        /// <summary>超商取貨資訊（CVS 用）</summary>
        public CvsPickupDTO CvsPickup { get; set; }

        /// <summary>付款資訊</summary>
        public PaymentInfoDTO PaymentInfo { get; set; }

        /// <summary>發票資訊</summary>
        public InvoiceInfoDTO InvoiceInfo { get; set; }

        // ---- 計價相關（保持你的原設計） ----
        public decimal Subtotal { get; set; }
        public decimal ItemDiscount { get; set; }
        public decimal CouponDiscount { get; set; }
        public decimal PointsDiscount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Total { get; set; }
        public decimal FreeShippingThreshold { get; set; }
        public int EstimatedPoints { get; set; }
        public CouponDTO AppliedCoupon { get; set; }
        public int PointsToUse { get; set; }
        public int UserPoints { get; set; }
    }

    /// <summary>
    /// 宅配配送資訊（CVS 時可忽略）
    /// </summary>
    public class ShippingInfoDTO
    {
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }

        // 宅配時才需要；CVS 時可以留空
        public string ZipCode { get; set; }
        public string City { get; set; }
        public string District { get; set; }
        public string Address { get; set; }
    }

    /// <summary>
    /// 超商取貨資訊（CVS 用）
    /// </summary>
    public class CvsPickupDTO
    {
        /// <summary>門市代碼（例如 7-11 的 StoreId）</summary>
        public string StoreId { get; set; }

        /// <summary>門市名稱</summary>
        public string StoreName { get; set; }

        /// <summary>門市地址</summary>
        public string Address { get; set; }

        /// <summary>門市電話（若供應商有回傳）</summary>
        public string Telephone { get; set; }

        /// <summary>供應商原始回傳 JSON（可選）</summary>
        public string RawJson { get; set; }
    }

    /// <summary>
    /// 付款資訊資料傳輸物件
    /// </summary>
    public class PaymentInfoDTO
    {
        public string Method { get; set; }
        public CreditCardInfoDTO CreditCard { get; set; }
    }

    public class CreditCardInfoDTO
    {
        public string CardholderName { get; set; }
        public string CardNumber { get; set; }
        public string ExpirationMonth { get; set; }
        public string ExpirationYear { get; set; }
        public string Cvv { get; set; }
    }

    public class InvoiceInfoDTO
    {
        public string Type { get; set; }       // personal, company, donate
        public string TaxId { get; set; }
        public string CompanyName { get; set; }
        public string DonationCode { get; set; }
        public string CarrierType { get; set; }
        public string CarrierId { get; set; }
    }
}
