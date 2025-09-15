using FUEN42Team3.Backend.Models.ViewModels;
using FUEN42Team3.Models.Services;
using FUEN42Team3.Models.ViewModel;
using FUEN42Team3.Backend.Models.ViewModels;
using FUEN42Team3.Models.Services;
using FUEN42Team3.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly OrderService _orderService;
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 100;

        public OrdersController(OrderService orderService)
        {
            _orderService = orderService;
        }

        public IActionResult Order(int pageNumber = 1, int pageSize = 10)
        {
            // 檢查參數值，若不合理就使用預設值
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = DefaultPageSize;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            // 將分頁信息傳遞給視圖
            ViewBag.CurrentPage = pageNumber;
            ViewBag.PageSize = pageSize;
            ViewBag.MaxPageSize = MaxPageSize;

            return View();
        }
    }
}
