using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [ApiController]
    [Route("api/faqs")]
    public class FaqsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        public FaqsController(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? q)
        {
            var query = _db.Faqs.Where(f => f.IsActive);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var kw = q.Trim();
                query = query.Where(f => (f.Question != null && f.Question.Contains(kw))
                                      || (f.Keywords != null && f.Keywords.Contains(kw)));
            }
            var list = await query.OrderBy(f => f.Id).Select(f => new { f.Id, f.Question, f.Answer, f.Keywords }).ToListAsync();
            return Ok(list);
        }

        // 僅限開發環境：快速插入 Demo FAQ
        [HttpPost("seed")]
        [Authorize] // 防止未授權隨意觸發
        public async Task<IActionResult> Seed()
        {
            if (!_env.IsDevelopment()) return Forbid();
            if (await _db.Faqs.AnyAsync()) return Ok(new { inserted = 0, note = "已有資料，略過" });
            var rows = new[]{
                new Faq{ Question="運費怎麼計算？", Answer="台灣本島單筆滿 NT$2,000 免運，未達收超商 NT$50 運費或宅配 NT$100；離島依物流報價。", Keywords="運費,免運,shipping", IsActive=true },
                new Faq{ Question="有哪些付款方式？", Answer="支援信用卡（VISA、MasterCard、JCB、銀聯卡）、超商取貨付款、ATM 轉帳、超商代碼繳費、超商條碼繳費。部分付款方式可能有金額限制。", Keywords="付款,支付,信用卡,超商取貨,貨到付款,COD,ATM轉帳,超商代碼,超商條碼", IsActive=true },
                new Faq{ Question="可以貨到付款嗎？", Answer="可以，部分大型或客製商品不適用，以結帳頁為準。", Keywords="貨到付款,COD", IsActive=true },
                new Faq{ Question="出貨需要多久？", Answer="現貨 1–2 個工作天出貨，預購依商品頁標示時間出貨。", Keywords="出貨,出貨時間,配送,到貨", IsActive=true },
                new Faq{ Question="如何查詢訂單狀態？", Answer="登入會員中心 → 訂單紀錄可查看；或提供訂單編號給客服查詢。", Keywords="訂單狀態,查詢,order", IsActive=true },
                new Faq{ Question="想更改收件地址", Answer="為保障出貨效率，已成立訂單如需更改地址，請立即聯繫客服協助。", Keywords="改地址,地址變更,收件,物流", IsActive=true },
                new Faq{ Question="發票如何開立？", Answer="提供電子發票，將寄送至您填寫的 Email。", Keywords="發票,電子發票,invoice", IsActive=true },
                new Faq{ Question="會員點數怎麼用？", Answer="結帳時可折抵，依活動規則可能有使用上限與效期。", Keywords="點數,折抵,points", IsActive=true },
                new Faq{ Question="客服時間", Answer="週一至週五 09:30–18:00（國定假日休息）。", Keywords="客服時間,營業時間,service", IsActive=true }
            };
            _db.Faqs.AddRange(rows);
            var n = await _db.SaveChangesAsync();
            return Ok(new { inserted = n });
        }
    }
}
