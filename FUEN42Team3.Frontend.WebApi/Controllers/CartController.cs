using Microsoft.AspNetCore.Mvc;
using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUEN42Team3.Frontend.WebApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using System.ComponentModel.DataAnnotations;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private const int MAX_PER_ITEM = 15; // 單品最大數量上限

        public CartController(AppDbContext context, IWebHostEnvironment env, IConfiguration config)
        {
            _db = context;
            _env = env;
            _config = config;
        }

        private int? GetCurrentMemberId()
        {
            var val = User.FindFirst("MemberId")?.Value;
            if (int.TryParse(val, out var id)) return id;
            return null;
        }

        /// <summary>
        /// 合併匿名（本地）購物車到目前使用者購物車。
        /// 用於使用者登入/註冊完成後，將未登入前加入的商品寫回資料庫。
        /// </summary>
        [HttpPost("merge")]
        public async Task<ActionResult<CartDTO>> MergeCart([FromBody] MergeCartRequest req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });
            if (req == null || req.Items == null || req.Items.Count == 0)
            {
                // 沒有要合併的項目，直接回傳當前購物車
                var emptyDto = await BuildCartDto(mid.Value);
                return Ok(new { success = true, data = emptyDto });
            }

            // 確保購物車主檔存在
            await EnsureCart(mid.Value);

            // 清理不合法項目，避免 500
            var validItems = (req.Items ?? new List<MergeCartItemRequest>())
                .Where(x => x != null && x.ProductId > 0 && x.Quantity > 0)
                .ToList();
            if (validItems.Count == 0)
            {
                var dtoNone = await BuildCartDto(mid.Value);
                return Ok(new { success = true, data = dtoNone });
            }

            // 先將相同 ProductId 的數量彙總，避免重複插入造成唯一鍵衝突
            var grouped = validItems
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToList();

            // 取出所有涉及的產品與庫存
            var productIds = grouped.Select(x => x.ProductId).Distinct().ToList();
            var prodStocks = await _db.Products
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                .Select(p => new { p.Id, p.Quantity })
                .ToDictionaryAsync(p => p.Id, p => p.Quantity);

            foreach (var item in grouped)
            {
                if (item.ProductId <= 0 || item.Quantity <= 0) continue;
                if (!prodStocks.TryGetValue(item.ProductId, out var stock)) continue;
                var cap = Math.Min(stock, MAX_PER_ITEM);

                // 嘗試先累加數量（同時將軟刪的項目復活）
                var affected = await _db.ShoppingCartItems
                    .Where(i => i.CartId == mid && i.ProductId == item.ProductId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(i => i.IsDeleted, _ => false)
                        .SetProperty(i => i.DeletedAt, _ => (DateTime?)null)
                        .SetProperty(i => i.Quantity, i => (i.Quantity + item.Quantity) > cap ? cap : (i.Quantity + item.Quantity))
                        .SetProperty(i => i.UpdatedAt, _ => FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now));

                if (affected == 0)
                {
                    _db.ShoppingCartItems.Add(new ShoppingCartItem
                    {
                        CartId = mid.Value,
                        ProductId = item.ProductId,
                        Quantity = Math.Min(item.Quantity, cap),
                        CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now,
                        IsDeleted = false
                    });
                }
            }

            await _db.SaveChangesAsync();

            // 合併完成後回傳最新購物車
            var dto = await BuildCartDto(mid.Value);
            return Ok(new { success = true, data = dto });
        }

        [HttpGet]
        public async Task<ActionResult<CartDTO>> GetCartDefault()
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });

            // 確保購物車主檔存在（避免外鍵問題）
            await EnsureCart(mid.Value);

            var dto = await BuildCartDto(mid.Value);
            return Ok(new { success = true, data = dto });
        }

        // GET: api/Cart/5
        [HttpGet("{userId:int}")]
        public async Task<ActionResult<CartDTO>> GetCart(int userId)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });
            if (userId != mid.Value) return Forbid();

            var exists = await _db.Members.AnyAsync(m => m.Id == userId);
            if (!exists) return NotFound(new { success = false, message = "找不到該用戶" });

            var dto = await BuildCartDto(userId);
            return Ok(new { success = true, data = dto });
        }



        // POST: api/Cart/AddItem
        [HttpPost("items")]
        public async Task<ActionResult<CartDTO>> AddItem(AddCartItemRequest req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });
            if (req == null || req.ProductId <= 0 || req.Quantity <= 0)
                return BadRequest(new { success = false, message = "參數不正確" });

            // 確保購物車主檔存在
            await EnsureCart(mid.Value);

            var product = await _db.Products
                .Where(p => p.Id == req.ProductId && !p.IsDeleted)
                .Select(p => new { p.Id, p.Quantity })
                .FirstOrDefaultAsync();

            if (product == null) return NotFound(new { success = false, message = "找不到商品" });

            // 先嘗試更新（資料庫層累加；EF Core 7+ 可用 ExecuteUpdate）
            var cap = Math.Min(product.Quantity, MAX_PER_ITEM);

            var affected = await _db.ShoppingCartItems
                    .Where(i => i.CartId == mid && i.ProductId == req.ProductId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(i => i.IsDeleted, _ => false)
                        .SetProperty(i => i.DeletedAt, _ => (DateTime?)null)
                        .SetProperty(i => i.Quantity, i => (i.Quantity + req.Quantity) > cap ? cap : (i.Quantity + req.Quantity))
                .SetProperty(i => i.UpdatedAt, _ => FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now));

            if (affected == 0)
            {
                // 沒有就插入
                _db.ShoppingCartItems.Add(new ShoppingCartItem
                {
                    CartId = mid.Value,
                    ProductId = req.ProductId,
                    Quantity = Math.Min(req.Quantity, cap),
                    CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now,
                    IsDeleted = false
                });
                await _db.SaveChangesAsync();
            }

            // 回傳最新購物車
            var dto = await BuildCartDto(mid.Value);
            return Ok(new { success = true, data = dto });
        }


        // PUT: api/Cart/UpdateQuantity/5
        [HttpPut("items/{itemId:int}")]
        public async Task<ActionResult<CartDTO>> UpdateQuantity(int itemId, UpdateQuantityRequest req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });
            if (req == null || req.Quantity < 0)
                return BadRequest(new { success = false, message = "數量不正確" });

            if (req.Quantity == 0)
            {
                // 直接刪除該筆項目（硬刪除）
                await _db.ShoppingCartItems
                    .Where(i => i.Id == itemId && i.CartId == mid)
                    .ExecuteDeleteAsync();
            }
            else
            {
                // 與 Product.Quantity 以及 MAX_PER_ITEM 做 clamp
                var item = await _db.ShoppingCartItems
                    .Where(i => i.Id == itemId && i.CartId == mid && !i.IsDeleted)
                    .Select(i => new { i.Id, i.ProductId })
                    .FirstOrDefaultAsync();

                if (item != null)
                {
                    var stock = await _db.Products
                        .Where(p => p.Id == item.ProductId && !p.IsDeleted)
                        .Select(p => p.Quantity)
                        .FirstOrDefaultAsync();

                    var cap = Math.Min(stock, MAX_PER_ITEM);
                    var newQty = Math.Min(req.Quantity, cap);

                    await _db.ShoppingCartItems
                        .Where(i => i.Id == itemId && i.CartId == mid && !i.IsDeleted)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(i => i.Quantity, _ => newQty)
                            .SetProperty(i => i.UpdatedAt, _ => FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now));
                }
            }

            var dto = await BuildCartDto(mid.Value);
            return Ok(new { success = true, data = dto });
        }

        [HttpDelete("items/{itemId:int}")]
        public async Task<ActionResult<CartDTO>> RemoveItem(int itemId)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });
            // 直接刪除該筆項目（硬刪除）
            await _db.ShoppingCartItems
                .Where(i => i.Id == itemId && i.CartId == mid)
                .ExecuteDeleteAsync();

            var dto = await BuildCartDto(mid.Value);
            return Ok(new { success = true, data = dto });
        }

        // 允許以 ProductId 刪除目前使用者購物車項目（當前端僅知道 productId 而非 itemId 時使用）
        [HttpDelete("items/by-product/{productId:int}")]
        public async Task<ActionResult<CartDTO>> RemoveItemByProduct(int productId)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });

            // 刪除符合當前會員與指定商品的購物車項目（硬刪除）
            await _db.ShoppingCartItems
                .Where(i => i.CartId == mid && i.ProductId == productId)
                .ExecuteDeleteAsync();

            var dto = await BuildCartDto(mid.Value);
            return Ok(new { success = true, data = dto });
        }

        // DELETE: api/Cart/items
        // 清空目前使用者購物車（硬刪除所有項目）
        [HttpDelete("items")]
        public async Task<ActionResult<CartDTO>> ClearCart()
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized(new { success = false, message = "未登入" });

            // 直接刪除目前使用者購物車所有項目（硬刪除）
            await _db.ShoppingCartItems
                .Where(i => i.CartId == mid)
                .ExecuteDeleteAsync();

            var dto = await BuildCartDto(mid.Value);
            return Ok(new { success = true, data = dto });
        }


        // POST: api/Cart/ApplyCoupon
        [HttpPost("ApplyCoupon")]
        public async Task<ActionResult<CartDTO>> ApplyCoupon([FromBody] string couponCode, int userId)
        {
            // 這裡應該實作套用優惠券的邏輯
            // 實際應用中，需要驗證優惠券有效性、計算折扣等

            // 假設套用成功，返回更新後的購物車
            var cart = await GetCart(userId);
            if (cart.Result is not null) return cart.Result;
            return Ok(cart.Value);
        }

        // POST: api/Cart/UsePoints
        [HttpPost("UsePoints")]
        public async Task<ActionResult<CartDTO>> UsePoints([FromBody] int points, int userId)
        {
            // 這裡應該實作使用點數的邏輯
            // 實際應用中，需要檢查用戶點數是否足夠、計算折扣等

            // 假設使用點數成功，返回更新後的購物車
            var cart = await GetCart(userId);
            if (cart.Result is not null) return cart.Result;
            return Ok(cart.Value);
        }

        // 將購物車資料組裝抽出，方便授權/匿名共用
        private async Task<CartDTO> BuildCartDto(int userId)
        {
            var now = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;

            // 確保購物車主檔存在
            await EnsureCart(userId);

            var cartRows = await _db.ShoppingCartItems
                .Where(i => i.CartId == userId && !i.IsDeleted)
                .Select(i => new
                {
                    ItemId = i.Id,
                    Qty = i.Quantity,
                    Product = new
                    {
                        i.Product.Id,
                        i.Product.ProductName,
                        // 部分資料可能缺少分類，避免 Null 導致 500
                        CategoryName = i.Product.Category == null ? null : i.Product.Category.CategoryName,
                        i.Product.BasePrice,
                        i.Product.SpecialPrice,
                        i.Product.SpecialPriceStartDate,
                        i.Product.SpecialPriceEndDate,
                        Stock = i.Product.Quantity,
                        ImageUrl = i.Product.ProductImages
                            .OrderByDescending(pi => pi.IsMainImage)
                            .Select(pi => pi.ImagePath)
                            .FirstOrDefault()
                    }
                })
                .AsNoTracking()
                .ToListAsync();

            var dto = new CartDTO
            {
                Id = userId,
                UserId = userId,
                CartItems = new List<CartItemDTO>(),
                // 與前台一致：滿 2000 免運（配送方式未知時先採用較常見門檻）
                FreeShippingThreshold = 2000,
                SelectedItems = new List<int>(),
                // 可用點數改為使用 PointLots 剩餘且未過期的總和，確保包含排程贈點
                UserPoints = await _db.PointLots
                    .Where(l => l.MemberId == userId
                        && l.RemainingPoints > 0
                        && l.ExpiresAt > now)
                    .SumAsync(l => (int?)l.RemainingPoints) ?? 0
            };

            decimal subtotal = 0m, originalSubtotal = 0m, itemDiscount = 0m;

            foreach (var r in cartRows)
            {
                bool onSale =
                    r.Product.SpecialPrice.HasValue &&
                    (r.Product.SpecialPriceStartDate == null || r.Product.SpecialPriceStartDate <= now) &&
                    (r.Product.SpecialPriceEndDate == null || r.Product.SpecialPriceEndDate >= now);

                var price = onSale ? r.Product.SpecialPrice!.Value : r.Product.BasePrice;
                decimal? original = onSale ? r.Product.BasePrice : (decimal?)null;

                dto.CartItems.Add(new CartItemDTO
                {
                    Id = r.ItemId,
                    ProductId = r.Product.Id,
                    Name = r.Product.ProductName,
                    Category = r.Product.CategoryName ?? "未分類",
                    Price = price,
                    OriginalPrice = original,
                    Quantity = r.Qty,
                    Stock = r.Product.Stock,
                    Image = string.IsNullOrEmpty(r.Product.ImageUrl) ? "/img/products/default.jpg" : r.Product.ImageUrl,
                    SelectedOptions = new Dictionary<string, string>()
                });

                dto.SelectedItems.Add(r.ItemId);

                var itemTotal = price * r.Qty;
                var itemOriginalTotal = (original ?? price) * r.Qty;
                subtotal += itemTotal;
                originalSubtotal += itemOriginalTotal;
                itemDiscount += (itemOriginalTotal - itemTotal);
            }

            dto.Subtotal = subtotal;
            dto.OriginalSubtotal = originalSubtotal;
            dto.ItemDiscount = itemDiscount;
            dto.ShippingFee = subtotal >= dto.FreeShippingThreshold ? 0 : 60;
            dto.TotalDiscount = itemDiscount;
            dto.FinalTotal = dto.Subtotal + dto.ShippingFee;

            try
            {
                var activeRules = await _db.GiftRules
                    .Where(r => !r.IsDeleted
                        && (r.StartDate == null || r.StartDate <= now)
                        && (r.EndDate == null || r.EndDate >= now))
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.StartDate,
                        r.EndDate,
                        r.ConditionType,
                        r.ConditionValue,
                        Gifts = r.GiftRuleItems
                            .Where(x => !x.IsDeleted)
                            .Select(x => new
                            {
                                x.Quantity,
                                Gift = new
                                {
                                    x.Gift.Id,
                                    x.Gift.Name,
                                    x.Gift.Description,
                                    x.Gift.ImageUrl
                                }
                            }).ToList()
                    })
                    .AsNoTracking()
                    .ToListAsync();

                var allPromotions = activeRules.Select(r => new GiftPromotionDTO
                {
                    Id = r.Id,
                    Name = r.Name,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    Type = r.ConditionType,
                    Threshold = r.ConditionValue,
                    Description = r.Name,
                    Gifts = r.Gifts.Select(g => new GiftDTO
                    {
                        Id = g.Gift.Id.ToString(),
                        Name = g.Gift.Name,
                        Description = g.Gift.Description,
                        Image = string.IsNullOrEmpty(g.Gift.ImageUrl) ? "/img/products/default.jpg" : g.Gift.ImageUrl,
                        Quantity = g.Quantity
                    }).ToList()
                }).ToList();

                int? birthMonth = await _db.MemberProfiles
                    .Where(mp => mp.MemberId == userId)
                    .Select(mp => mp.Birthdate.HasValue ? (int?)mp.Birthdate.Value.Month : null)
                    .FirstOrDefaultAsync();

                var totalPoints = dto.UserPoints;
                var selectedCount = dto.SelectedItems?.Count ?? 0;
                var amount = dto.Subtotal;

                bool IsQualified(GiftPromotionDTO p) => p.Type switch
                {
                    "amount" => amount >= p.Threshold,
                    "quantity" => selectedCount >= p.Threshold,
                    "member_points" => totalPoints >= p.Threshold,
                    "birthday_month" => birthMonth.HasValue && birthMonth.Value == now.Month,
                    _ => false
                };

                dto.AllGiftPromotions = allPromotions;
                dto.QualifiedGifts = allPromotions.Where(IsQualified).ToList();
            }
            catch
            {
                dto.AllGiftPromotions = new();
                dto.QualifiedGifts = new();
            }

            return dto;
        }

        // 確保購物車主檔存在（以 MemberId 作為 Cart 主鍵）
        private async Task<ShoppingCart> EnsureCart(int memberId)
        {
            // 先抓該會員的購物車（無論是否軟刪除），避免主鍵衝突
            var cart = await _db.ShoppingCarts
                .FirstOrDefaultAsync(c => c.MemberId == memberId);

            if (cart == null)
            {
                cart = new ShoppingCart
                {
                    MemberId = memberId,
                    CreatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now,
                    IsDeleted = false
                };
                _db.ShoppingCarts.Add(cart);
                await _db.SaveChangesAsync();
                return cart;
            }

            if (cart.IsDeleted)
            {
                cart.IsDeleted = false;
                cart.DeletedAt = null;
                cart.UpdatedAt = FUEN42Team3.Frontend.WebApi.Models.Services.TaipeiTime.Now;
                await _db.SaveChangesAsync();
            }

            return cart;
        }
    }
}
