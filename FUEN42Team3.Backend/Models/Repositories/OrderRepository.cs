using FUEN42Team3.Backend.Models.DTOs;
using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Models.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;
        public OrderRepository(AppDbContext context) => _context = context;

        public async Task<List<OrderDto>> GetAllAsync()
        {
            return await _context.Orders.AsNoTracking()
                .Where(o => !o.IsDeleted)
                .Select(o => new OrderDto
                {
                    Id = o.Id,
                    UserId = o.MemberId,
                    TotalAmount = o.TotalAmount,
                    ShippingFee = o.ShippingFee,
                    UsedPoints = o.UsedPoints,
                    Category = o.OrderType == "1" ? "預購" : "現貨",
                    Status = o.Status != null ? o.Status.StatusName : "待處理",
                    OrderDate = o.OrderDate,
                    PaymentMethod = o.PaymentMethod != null ? o.PaymentMethod.MethodName : null,
                    DeliveryMethod = o.DeliveryMethod != null ? o.DeliveryMethod.ShippingName : null,
                    RecipientName = o.RecipientName,
                    Phone = o.RecipientPhone,
                    Address = o.ShippingAddress,
                    ZipCode = o.ShippingZipCode,
                    Details = o.OrderDetails
                        .Where(od => !od.IsDeleted)
                        .Select(od => new OrderDetailDto
                        {
                            ProductName = od.ProductName,
                            Quantity = od.Quantity,
                            UnitPrice = od.UnitPrice,
                            DiscountAmount = od.Discount,
                            DiscountPercent = null
                        }).ToList(),
                    Gifts = o.OrderGifts
                        .Select(g => new GiftDto
                        {
                            GiftProductName = g.Gift != null ? g.Gift.Name : string.Empty,
                            Quantity = g.Quantity
                        }).ToList()
                })
                .ToListAsync();
        }

        public async Task<OrderDto?> GetByIdAsync(int id)
        {
            return await _context.Orders.AsNoTracking()
                .Where(o => o.Id == id && !o.IsDeleted)
                .Select(o => new OrderDto
                {
                    Id = o.Id,
                    UserId = o.MemberId,
                    TotalAmount = o.TotalAmount,
                    ShippingFee = o.ShippingFee,
                    UsedPoints = o.UsedPoints,
                    Category = o.OrderType == "1" ? "預購" : "現貨",
                    Status = o.Status != null ? o.Status.StatusName : "待處理",
                    OrderDate = o.OrderDate,
                    PaymentMethod = o.PaymentMethod != null ? o.PaymentMethod.MethodName : null,
                    DeliveryMethod = o.DeliveryMethod != null ? o.DeliveryMethod.ShippingName : null,
                    RecipientName = o.RecipientName,
                    Phone = o.RecipientPhone,
                    Address = o.ShippingAddress,
                    ZipCode = o.ShippingZipCode,
                    Details = o.OrderDetails
                        .Where(od => !od.IsDeleted)
                        .Select(od => new OrderDetailDto
                        {
                            ProductId = od.ProductId, // 確保返回 ProductId
                            ProductName = od.ProductName,
                            Quantity = od.Quantity,
                            UnitPrice = od.UnitPrice,
                            DiscountAmount = od.Discount,
                            DiscountPercent = null
                        }).ToList(),
                    Gifts = o.OrderGifts
                        .Select(g => new GiftDto
                        {
                            GiftId = g.GiftId, // 同樣返回 GiftId
                            GiftProductName = g.Gift != null ? g.Gift.Name : string.Empty,
                            Quantity = g.Quantity
                        }).ToList()
                })
                .FirstOrDefaultAsync();
        }

        public async Task<int> CreateAsync(OrderDto dto)
        {
            var order = new Order
            {
                MemberId = dto.UserId,
                OrderNumber = $"ORD{FUEN42Team3.Backend.Models.Services.TaipeiTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}",
                TotalAmount = dto.TotalAmount,
                ShippingFee = dto.ShippingFee,
                UsedPoints = dto.UsedPoints ?? 0,
                OrderType = dto.Category == "預購" ? "1" : "0",
                StatusId = await GetStatusIdByNameAsync(dto.Status ?? "待處理"),
                OrderDate = FUEN42Team3.Backend.Models.Services.TaipeiTime.Now,
                RecipientName = dto.RecipientName,
                RecipientPhone = dto.Phone,
                ShippingAddress = dto.Address,
                ShippingZipCode = dto.ZipCode,
                CreatedAt = FUEN42Team3.Backend.Models.Services.TaipeiTime.Now,
                IsDeleted = false,
                PaymentMethodId = await GetPaymentMethodIdAsync(dto.PaymentMethod),
                DeliveryMethodId = await GetDeliveryMethodIdAsync(dto.DeliveryMethod),
                PointsEarned = 0
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // OrderDetail 建立部分
            if (dto.Details?.Any() == true)
            {
                foreach (var d in dto.Details)
                {
                    int productId = d.ProductId > 0 ? d.ProductId : await GetProductIdByNameAsync(d.ProductName);
                    _context.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = productId,
                        ProductName = d.ProductName,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        Discount = d.DiscountAmount,
                        IsDeleted = false
                    });
                }
            }

            // OrderGift 建立部分
            if (dto.Gifts?.Any() == true)
            {
                foreach (var g in dto.Gifts)
                {
                    int giftId = g.GiftId > 0 ? g.GiftId : await GetGiftIdByNameAsync(g.GiftProductName);
                    if (giftId > 0)
                    {
                        _context.OrderGifts.Add(new OrderGift
                        {
                            OrderId = order.Id,
                            GiftId = giftId,
                            Quantity = g.Quantity
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            return order.Id;
        }

        public async Task<bool> UpdateAsync(OrderDto dto)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .Include(o => o.OrderGifts)
                .FirstOrDefaultAsync(o => o.Id == dto.Id && !o.IsDeleted);

            if (order == null) return false;

            // 1. 驗證用戶ID
            var memberExists = await _context.Members.AnyAsync(m => m.Id == dto.UserId);
            if (!memberExists)
            {
                Console.WriteLine($"用戶ID無效: {dto.UserId}");
                return false;
            }

            // 2. 驗證狀態
            var statusId = await GetStatusIdByNameAsync(dto.Status ?? "待處理");
            if (statusId <= 0)
            {
                Console.WriteLine($"訂單狀態無效: {dto.Status}");
                return false;
            }

            // 3. 驗證付款方式
            var paymentMethodId = await GetPaymentMethodIdAsync(dto.PaymentMethod);
            if (paymentMethodId <= 0)
            {
                Console.WriteLine($"付款方式無效: {dto.PaymentMethod}");
                return false;
            }

            // 4. 驗證運送方式
            var deliveryMethodId = await GetDeliveryMethodIdAsync(dto.DeliveryMethod);
            if (deliveryMethodId <= 0)
            {
                Console.WriteLine($"運送方式無效: {dto.DeliveryMethod}");
                return false;
            }

            // 更新訂單資料
            order.MemberId = dto.UserId;
            order.TotalAmount = dto.TotalAmount;
            order.ShippingFee = dto.ShippingFee;
            order.UsedPoints = dto.UsedPoints ?? 0;
            order.OrderType = dto.Category == "預購" ? "1" : "0";
            order.StatusId = statusId;
            order.RecipientName = dto.RecipientName;
            order.RecipientPhone = dto.Phone;
            order.ShippingAddress = dto.Address;
            order.ShippingZipCode = dto.ZipCode;
            order.PaymentMethodId = paymentMethodId;
            order.DeliveryMethodId = deliveryMethodId;
            order.UpdatedAt = FUEN42Team3.Backend.Models.Services.TaipeiTime.Now;

            // OrderDetails：先標記舊的為刪除，再新增新的
            foreach (var d in order.OrderDetails) d.IsDeleted = true;

            // 5. 驗證並新增訂單明細
            if (dto.Details?.Any() == true)
            {
                bool hasValidDetails = false;
                foreach (var d in dto.Details)
                {
                    int productId = d.ProductId > 0 ? d.ProductId : await GetProductIdByNameAsync(d.ProductName);
                    if (productId <= 0)
                    {
                        Console.WriteLine($"找不到產品: {d.ProductName}");
                        continue;
                    }

                    hasValidDetails = true;
                    _context.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = productId,
                        ProductName = d.ProductName,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        Discount = d.DiscountAmount,
                        IsDeleted = false,
                        CreatedAt = FUEN42Team3.Backend.Models.Services.TaipeiTime.Now,
                        Subtotal = d.Quantity * d.UnitPrice - (d.DiscountAmount ?? 0)
                    });
                }

                if (!hasValidDetails)
                {
                    Console.WriteLine("訂單沒有有效的產品明細");
                    return false;
                }
            }
            else
            {
                Console.WriteLine("訂單沒有產品明細");
                return false;
            }

            // 6. 驗證並新增贈品
            _context.OrderGifts.RemoveRange(order.OrderGifts);
            if (dto.Gifts?.Any() == true)
            {
                foreach (var g in dto.Gifts)
                {
                    var giftId = g.GiftId > 0 ? g.GiftId : await GetGiftIdByNameAsync(g.GiftProductName);
                    if (giftId <= 0)
                    {
                        Console.WriteLine($"找不到贈品: {g.GiftProductName}");
                        continue;
                    }

                    _context.OrderGifts.Add(new OrderGift
                    {
                        OrderId = order.Id,
                        GiftId = giftId,
                        Quantity = g.Quantity,
                        IsDeleted = false,
                        CreatedAt = FUEN42Team3.Backend.Models.Services.TaipeiTime.Now,
                        CreatedBy = dto.UserId
                    });
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新訂單失敗: {ex.Message}");
                // 記錄詳細錯誤
                if (ex.InnerException != null)
                    Console.WriteLine($"內部錯誤: {ex.InnerException.Message}");
                return false;
            }
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return false;

            order.IsDeleted = true;
            order.DeletedAt = FUEN42Team3.Backend.Models.Services.TaipeiTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateStatusAsync(int id, string status)
        {
            try
            {
                var order = await _context.Orders.FindAsync(id);
                if (order == null)
                {
                    Console.WriteLine($"找不到ID為 {id} 的訂單");
                    return false;
                }

                // 檢查狀態是否有效
                var statusId = await GetStatusIdByNameAsync(status);
                if (statusId <= 0)
                {
                    Console.WriteLine($"狀態名稱 {status} 無效");
                    return false;
                }

                order.StatusId = statusId;
                order.UpdatedAt = FUEN42Team3.Backend.Models.Services.TaipeiTime.Now;

                // 根據狀態設置相應的日期
                if (status == "已出貨") order.ShipDate = FUEN42Team3.Backend.Models.Services.TaipeiTime.Now;
                if (status == "已完成") order.CompletionDate = FUEN42Team3.Backend.Models.Services.TaipeiTime.Now;

                // 使用更具體的 SaveChanges 方法來獲取更多的錯誤信息
                var changes = await _context.SaveChangesAsync();
                Console.WriteLine($"更新訂單 {id} 狀態為 {status}，影響了 {changes} 條記錄");

                return changes > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新訂單 {id} 狀態時發生錯誤: {ex.Message}");
                // 在這裡可以記錄更詳細的異常信息
                Console.WriteLine($"異常詳情: {ex}");
                throw; // 重新拋出異常，讓上層處理
            }
        }

        // --- helpers ---
        private async Task<int> GetPaymentMethodIdAsync(string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                // 從資料庫獲取默認的付款方式ID
                var defaultId = await _context.PaymentMethods
                    .Where(p => p.IsActive && !p.IsDeleted)
                    .OrderBy(p => p.Id)
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync();

                // 確保至少返回一個有效ID
                return defaultId > 0 ? defaultId : 1;
            }

            var id = await _context.PaymentMethods
                .Where(p => p.MethodName == name && p.IsActive && !p.IsDeleted)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            // 如果找不到，返回默認值
            if (id <= 0)
            {
                Console.WriteLine($"找不到付款方式: {name}，使用預設付款方式");
                // 再次嘗試獲取有效的默認值
                return await _context.PaymentMethods
                    .Where(p => p.IsActive && !p.IsDeleted)
                    .OrderBy(p => p.Id)
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync();
            }

            return id;
        }

        private async Task<int> GetDeliveryMethodIdAsync(string? name)
        {
            if (string.IsNullOrEmpty(name)) return 1; // 默認值，您可以根據需要調整

            var id = await _context.DeliveryMethods
                .Where(d => d.ShippingName == name && d.IsActive && !d.IsDeleted)
                .Select(d => d.Id)
                .FirstOrDefaultAsync();

            return id > 0 ? id : 1; // 如果找不到，返回默認值
        }

        private async Task<int> GetStatusIdByNameAsync(string name)
        {
            var statusId = await _context.OrderStatuses
                .Where(s => s.StatusName == name)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();

            // 如果找不到狀態，則使用預設值 (1 - 通常是「待處理」)
            if (statusId == 0)
            {
                // 記錄錯誤
                Console.WriteLine($"找不到狀態名稱: {name}，使用預設狀態ID: 1");
                return 1;
            }

            return statusId;
        }

        private async Task<int> GetGiftIdByNameAsync(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return 0; // 贈品可以為空，返回0表示沒有贈品

            var id = await _context.Gifts
                .Where(g => g.Name == name && !g.IsDeleted)
                .Select(g => g.Id)
                .FirstOrDefaultAsync();

            if (id <= 0)
                Console.WriteLine($"找不到贈品: {name}");

            return id;
        }

        private async Task<int> GetProductIdByNameAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("產品名稱為空");
                return 0;
            }

            var id = await _context.Products
                .Where(p => p.ProductName == name && !p.IsDeleted)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            if (id <= 0)
                Console.WriteLine($"找不到產品: {name}");

            return id;
        }

        // 修正 CS4032：將 GetValidStatusesAsync 方法加上 async 修飾並正確回傳 Task<List<string>>
        public async Task<List<string>> GetValidStatusesAsync()
        {
            var validStatuses = await _context.OrderStatuses
                .Select(s => s.StatusName)
                .ToListAsync();
            return validStatuses;
        }

        // 將 PaymentMethodDto 的 Description 屬性改為 null，因為 PaymentMethod 類別沒有 Description 屬性
        public async Task<List<PaymentMethodDto>> GetAllPaymentMethodsAsync()
        {
            return await _context.PaymentMethods
                .Where(p => p.IsActive && !p.IsDeleted)
                .Select(p => new PaymentMethodDto
                {
                    Id = p.Id,
                    MethodName = p.MethodName,
                    Description = string.Empty // 修正：PaymentMethod 沒有 Description 屬性
                })
                .ToListAsync();
        }

        public List<CategoryDto> GetOrderCategories()
        {
            // 訂單類型通常是固定的，這裡返回預定義的值
            return new List<CategoryDto>
            {
                new CategoryDto { Value = "0", Text = "現貨" },
                new CategoryDto { Value = "1", Text = "預購" }
            };
        }

        public async Task<List<DeliveryMethodDto>> GetAllDeliveryMethodsAsync()
        {
            return await _context.DeliveryMethods
                .Where(d => d.IsActive && !d.IsDeleted)
                .Select(d => new DeliveryMethodDto
                {
                    Id = d.Id,
                    ShippingName = d.ShippingName,
                    BaseShippingCost = d.BaseShippingCost
                })
                .ToListAsync();
        }

        public async Task<(List<OrderDto> Orders, int TotalCount, int TotalPages, int CurrentPage, int PageSize)> GetOrdersPagedAsync(int pageNumber, int pageSize)
        {
            // 確保參數有效
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            // 獲取符合條件的訂單總數
            var totalCount = await _context.Orders
                .Where(o => !o.IsDeleted)
                .CountAsync();

            // 計算總頁數
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // 獲取當前頁的訂單
            var orders = await _context.Orders.AsNoTracking()
                .Where(o => !o.IsDeleted)
                .OrderByDescending(o => o.OrderDate) // 依日期降序排列，最新的訂單在前
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderDto
                {
                    Id = o.Id,
                    UserId = o.MemberId,
                    TotalAmount = o.TotalAmount,
                    ShippingFee = o.ShippingFee,
                    UsedPoints = o.UsedPoints,
                    Category = o.OrderType == "1" ? "預購" : "現貨",
                    Status = o.Status != null ? o.Status.StatusName : "待處理",
                    OrderDate = o.OrderDate,
                    PaymentMethod = o.PaymentMethod != null ? o.PaymentMethod.MethodName : null,
                    DeliveryMethod = o.DeliveryMethod != null ? o.DeliveryMethod.ShippingName : null,
                    RecipientName = o.RecipientName,
                    Phone = o.RecipientPhone,
                    Address = o.ShippingAddress,
                    ZipCode = o.ShippingZipCode,
                    Details = o.OrderDetails
                        .Where(od => !od.IsDeleted)
                        .Select(od => new OrderDetailDto
                        {
                            ProductId = od.ProductId,
                            ProductName = od.ProductName,
                            Quantity = od.Quantity,
                            UnitPrice = od.UnitPrice,
                            DiscountAmount = od.Discount,
                            DiscountPercent = null
                        }).ToList(),
                    Gifts = o.OrderGifts
                        .Select(g => new GiftDto
                        {
                            GiftId = g.GiftId,
                            GiftProductName = g.Gift != null ? g.Gift.Name : string.Empty,
                            Quantity = g.Quantity
                        }).ToList()
                })
                .ToListAsync();

            return (orders, totalCount, totalPages, pageNumber, pageSize);
        }
    }
}
