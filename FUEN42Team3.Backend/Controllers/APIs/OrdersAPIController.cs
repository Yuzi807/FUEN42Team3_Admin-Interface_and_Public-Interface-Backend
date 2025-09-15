using FUEN42Team3.Backend.Models.DTOs;
using FUEN42Team3.Models.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 僅允許已登入的後台人員使用，防止未授權存取訂單資料
    public class OrdersAPIController : ControllerBase
    {
        private readonly OrderService _svc;
        private readonly ILogger<OrdersAPIController> _logger; // 新增的 logger 欄位

        public OrdersAPIController(OrderService svc, ILogger<OrdersAPIController> logger) // 注入 ILogger
        {
            _svc = svc;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetOrders([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                // 驗證參數
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                _logger.LogInformation($"獲取訂單，頁碼：{pageNumber}，每頁大小：{pageSize}");

                // 權限判斷：後台人員可看全部；一般登入者僅能看自己的訂單
                var isBackend = User?.IsInRole("Admin") == true || User?.IsInRole("Manager") == true;
                int.TryParse(User?.FindFirst("MemberId")?.Value, out var memberId);

                var result = await _svc.GetOrdersPagedAsync(pageNumber, pageSize);
                if (!isBackend)
                {
                    // 僅保留屬於目前登入會員的訂單
                    var mid = memberId;
                    result.Orders = result.Orders?.Where(o =>
                    {
                        try
                        {
                            var owner = (o as dynamic)?.MemberId;
                            int ownerId = owner is int ? (int)owner : Convert.ToInt32(owner);
                            return mid > 0 && ownerId == mid;
                        }
                        catch { return false; }
                    }).ToList() ?? new List<FUEN42Team3.Backend.Models.DTOs.OrderDto>();
                    // 重新計算分頁統計（簡化處理：僅就當前頁資料回應，以免牽動倉儲）
                    var cnt = result.Orders.Count;
                    result = (result.Orders, cnt, 1, 1, cnt);
                }

                return Ok(new
                {
                    success = true,
                    data = result.Orders,
                    totalCount = result.TotalCount,
                    totalPages = result.TotalPages,
                    currentPage = result.CurrentPage,
                    pageSize = result.PageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取訂單列表失敗");
                return StatusCode(500, new { success = false, message = "獲取訂單失敗：" + ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            // 取得訂單
            var data = await _svc.GetOrderByIdAsync(id);
            if (data == null)
                return NotFound(new { success = false, message = $"找不到ID為{id}的訂單" });

            // 僅允許：
            // 1) 已登入的後台人員（具有後台權限角色）；或
            // 2) 該訂單的擁有者（登入會員，其 MemberId 與訂單 MemberId 相同）
            try
            {
                // 嘗試自 Claims 取得角色與 MemberId（視後台登入機制而定）
                var isBackend = User?.IsInRole("Admin") == true || User?.IsInRole("Manager") == true;
                var claimMemberId = User?.FindFirst("MemberId")?.Value;
                int memberId = 0;
                int.TryParse(claimMemberId, out memberId);

                // 後台人員可看；否則需比對擁有者
                if (!isBackend)
                {
                    // data 來源 DTO 需含 MemberId；若沒有，保守拒絕
                    var ownerId = (data as dynamic)?.MemberId;
                    int owner = 0;
                    try { owner = ownerId is int ? ownerId : Convert.ToInt32(ownerId); } catch { owner = 0; }

                    if (owner <= 0 || memberId <= 0 || owner != memberId)
                    {
                        // 保守回 404，避免洩漏存在性
                        return NotFound(new { success = false, message = $"找不到ID為{id}的訂單" });
                    }
                }
            }
            catch { /* 若發生例外，一律保守處理 */ return NotFound(new { success = false, message = $"找不到ID為{id}的訂單" }); }

            return Ok(new { success = true, data });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] OrderDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "輸入格式錯誤" });

            var (ok, message, newId) = await _svc.CreateAsync(dto);
            if (!ok) return BadRequest(new { success = false, message });

            // 取回完整資料（用你已存在的查詢邏輯）
            var created = await _svc.GetOrderByIdAsync(newId);
            if (created == null)
                return StatusCode(500, new { success = false, message = "已建立但查無新訂單資料" });

            // 建議用 201 並附 Location
            return CreatedAtAction(nameof(GetOrderById), new { id = newId },
                new { success = true, message, data = created });
        }


        //[HttpPost]
        //public async Task<IActionResult> Create([FromBody] OrderDto dto)
        //{
        //    if (!ModelState.IsValid) return BadRequest(new { success = false, message = "輸入格式錯誤" });

        //    var (ok, message, newId) = await _svc.CreateAsync(dto);
        //    if (!ok) return BadRequest(new { success = false, message });

        //    return Ok(new { success = true, message, data = new { id = newId } });
        //}

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] OrderDto dto)
        {
            try
            {
                if (id != dto.Id)
                    return BadRequest(new { success = false, message = "訂單ID不匹配" });

                // 檢查所有產品ID是否有效
                if (dto.Details?.Any(d => d.ProductId <= 0) == true)
                {
                    var invalidProducts = dto.Details.Where(d => d.ProductId <= 0)
                                                   .Select(d => d.ProductName)
                                                   .ToList();

                    return BadRequest(new
                    {
                        success = false,
                        message = $"以下產品沒有有效ID: {string.Join(", ", invalidProducts)}"
                    });
                }

                var (ok, message) = await _svc.UpdateAsync(dto);
                if (!ok) return BadRequest(new { success = false, message });

                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                // 記錄詳細錯誤
                Console.WriteLine($"更新訂單錯誤: {ex.Message}");

                // 返回友好錯誤訊息
                return StatusCode(500, new
                {
                    success = false,
                    message = "更新訂單時發生錯誤，請確保所有產品ID有效"
                });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var (ok, message) = await _svc.DeleteAsync(id);
            if (!ok) return BadRequest(new { success = false, message });
            return Ok(new { success = true, message });
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusUpdateDto dto)
        {
            try
            {
                if (string.IsNullOrEmpty(dto.Status))
                {
                    return BadRequest(new { success = false, message = "狀態值不能為空" });
                }

                // 記錄詳細的請求內容
                _logger.LogInformation($"嘗試更新訂單 {id} 的狀態為 {dto.Status}");

                var (ok, message) = await _svc.UpdateStatusAsync(id, dto.Status);
                if (!ok)
                {
                    _logger.LogWarning($"更新訂單 {id} 狀態失敗: {message}");
                    return BadRequest(new { success = false, message });
                }

                _logger.LogInformation($"成功更新訂單 {id} 狀態為 {dto.Status}");
                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新訂單 {id} 狀態時發生異常");
                return StatusCode(500, new
                {
                    success = false,
                    message = "更新狀態失敗，請稍後再試",
                    details = ex.Message  // 開發環境可以返回詳細錯誤
                });
            }
        }

        [HttpGet("payment-methods")]
        public async Task<IActionResult> GetPaymentMethods()
        {
            var methods = await _svc.GetAllPaymentMethodsAsync();
            return Ok(new { success = true, data = methods });
        }

        [HttpGet("categories")]
        public IActionResult GetOrderCategories()
        {
            var categories = _svc.GetOrderCategories();
            return Ok(new { success = true, data = categories });
        }

        [HttpGet("delivery-methods")]
        public async Task<IActionResult> GetDeliveryMethods()
        {
            var methods = await _svc.GetAllDeliveryMethodsAsync();
            return Ok(new { success = true, data = methods });
        }

        [HttpGet("statuses")]
        public async Task<IActionResult> GetOrderStatuses()
        {
            try
            {
                // 從資料庫獲取有效的訂單狀態
                var statuses = await _svc.GetValidStatusesAsync();

                // 將狀態轉換為前端所需的格式
                var formattedStatuses = statuses.Select(s => new { statusName = s }).ToList();

                return Ok(new { success = true, data = formattedStatuses });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取訂單狀態失敗");
                return StatusCode(500, new { success = false, message = "獲取訂單狀態失敗: " + ex.Message });
            }
        }
    }
}
