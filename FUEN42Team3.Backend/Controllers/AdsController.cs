using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]

    public class AdsController : Controller
    {
        private readonly AppDbContext _context;
        public AdsController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Marquee()
        {
            // 以linq載入資料庫真實跑馬燈資料
            var marquees = _context.Marquees
                .Where(m => m.IsActive)
                .OrderBy(m => m.SortOrder)
                .ToList();

            return View(marquees);
        }

        public IActionResult Carousel() //主頁輪播
        {
            var viewModel = new AdsViewModel
            {
                CarouselList = new List<CarouselViewModel>
                {
                    new CarouselViewModel { Id = 1, Title = "歡迎來到我們的商店", SubTitle = "最新商品上架中", Image = "/images/carousel1.jpg", Link = "/products/new-arrivals", IsActive = true, SortOrder = 1 },
                    new CarouselViewModel { Id = 2, Title = "限時優惠", SubTitle = "全館滿額免運費", Image = "/images/carousel2.jpg", Link = "/promotions/free-shipping", IsActive = true, SortOrder = 2 }
                }
            };
            return View(viewModel);
        }
        public IActionResult Popup() //彈窗
        {
            var viewModel = new AdsViewModel
            {
                PopupList = new List<PopupViewModel>
                {
                    new PopupViewModel { Id = 1, Title = "限時優惠", Image = "/images/popup1.jpg", Link = "/promotions/limited-time", IsActive = true, SortOrder = 1 },
                    new PopupViewModel { Id = 2, Title = "新品上市", Image = "/images/popup2.jpg", Link = "/products/new-arrivals", IsActive = true, SortOrder = 2 }
                }
            };
            return View(viewModel);
        }
        
    }
}
