using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Drawing.Printing;
using X.PagedList;
using X.PagedList.Extensions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    public class ForumController : Controller
    {
        private readonly AppDbContext _context;

        public ForumController(AppDbContext context)
        {
            _context = context;
        }


     //分頁、搜尋、時間範圍查詢文章
public async Task<IActionResult> Posts(string? keyword, DateTime? startDate, DateTime? endDate, int page = 1, int pageSize = 10)
    {
        // 1. 建立查詢條件
        var query = _context.Posts
            .Include(p => p.Member)
            .Include(p => p.Type)
            .Include(p => p.Status)
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p =>
                p.Title.Contains(keyword) ||
                p.Member.UserName.Contains(keyword));
        }

        if (startDate.HasValue)
        {
            query = query.Where(p => p.PostTime >= startDate.Value.Date);
        }

        if (endDate.HasValue)
        {
                var next = endDate.Value.Date.AddDays(1);
                query = query.Where(p => p.PostTime < next);
            }

        // 2. 計算總筆數（非同步）
        var totalCount = await query.CountAsync();

        // 3. 取出當頁資料（非同步）
        var pagedPosts = await query
            .OrderByDescending(p => p.PostTime.HasValue) // 先把有時間的排前面
            .ThenByDescending(p => p.PostTime)           // 再依時間新到舊            
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PostsViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Member = p.Member != null ? p.Member.UserName : "未知",
                Category = p.Type != null ? p.Type.Name : "未知",
                PostTime = p.PostTime ?? null,
                NumOfGoods = p.NumOfGoods,
                NumOfHits = p.NumOfHits,
                StatusName = p.Status != null ? p.Status.Name : "未知"
            })
            .ToListAsync();

        // 4. 包裝成分頁物件
        var pagedList = new StaticPagedList<PostsViewModel>(pagedPosts, page, pageSize, totalCount);

            // 5. 組合 ViewModel
            var viewModel = new QueryViewModel<PostsViewModel>
            {
                Keyword = keyword,
                StartDate = startDate,
                EndDate = endDate,
                Page = page,
                PageSize = pageSize,
                Items = pagedList // 注意這裡要是 IPagedList<PostsViewModel>
            };

            return View(viewModel);
    }


        //刪除文章功能
        [HttpPost]
        public async Task<IActionResult> DeletePost(int id, string? keyword, DateTime? startDate, DateTime? endDate, int page = 1, int pageSize = 5)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null) return NotFound();

            post.IsDeleted = true;
            post.StatusId = 2;
            await _context.SaveChangesAsync();

            // 導回原本的查詢條件與頁面
            return RedirectToAction("Posts", new
            {
                keyword = keyword,
                startDate = startDate?.ToString("yyyy-MM-dd"),
                endDate = endDate?.ToString("yyyy-MM-dd"),
                page,
                pageSize
            });
        }


        public IActionResult postHistory() //文章編輯歷史
        {
            return View();
        }
        
        public IActionResult ShowPost(int id)
        {
            var post = _context.Posts
                .Include(p => p.Member)
                .Include(p => p.Type)
                .Include(p => p.Status)
                .FirstOrDefault(p => p.Id == id );

            if (post == null)
            {
                return NotFound();
            }

            return View(post); // 對應 Views/Posts/ShowPost.cshtml
        }

        //顯示留言列表
        // 顯示留言列表（只取：未刪除留言 + 所屬文章為公開且未刪除）
        public async Task<IActionResult> Comments(
            string? keyword, DateTime? startDate, DateTime? endDate,
            int page = 1, int pageSize = 10)
        {
            // 讓 endDate 包含整天（若你要「<= 當日 23:59:59」語意）
            DateTime? endExclusive = endDate?.Date.AddDays(1);

            var query = _context.Comments
                .AsNoTracking()
                .Include(c => c.Member)
                .Include(c => c.Post)
                .Where(c => !c.IsDeleted
                            && c.Post != null
                            && !c.Post.IsDeleted
                            && c.Post.StatusId == 1);   // 1 = 公開（依你的系統調整）

            // 🔍 關鍵字搜尋（留言、作者、文章標題）
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = $"%{keyword.Trim()}%";
                query = query.Where(c =>
                    EF.Functions.Like(c.CommentText ?? "", kw) ||
                    EF.Functions.Like(c.Member.UserName ?? "", kw) ||
                    EF.Functions.Like(c.Post.Title ?? "", kw));
            }

            // 📆 時間區間（含當日）
            if (startDate.HasValue)
                query = query.Where(c => c.CommentTime >= startDate.Value);
            if (endExclusive.HasValue)
                query = query.Where(c => c.CommentTime < endExclusive.Value);

            var totalCount = await query.CountAsync();

            var pagedComments = await query
                .OrderByDescending(c => c.CommentTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CommentViewModel
                {
                    Id = c.Id,
                    CommentText = c.CommentText,
                    CommentTime = c.CommentTime,
                    MemberName = c.Member.UserName,
                    PostId = c.PostId,          // 用外鍵即可
                    PostTitle = c.Post.Title
                })
                .ToListAsync();

            var pagedList = new StaticPagedList<CommentViewModel>(pagedComments, page, pageSize, totalCount);

            var viewModel = new QueryViewModel<CommentViewModel>
            {
                Keyword = keyword,
                StartDate = startDate,
                EndDate = endDate,
                Page = page,
                PageSize = pageSize,
                Items = pagedList
            };

            return View(viewModel);
        }



        //刪除留言
        [HttpPost]
        public async Task<IActionResult> DeleteComment(int id, string? keyword, int page = 1, int pageSize = 10)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return NotFound();

            comment.IsDeleted = true;
            await _context.SaveChangesAsync();

            return RedirectToAction("Comments", new
            {
                keyword = keyword,
                page,
                pageSize
            });
        }


        //文章檢舉
        public async Task<IActionResult> PostReports(string? keyword, DateTime? startDate, DateTime? endDate, int page = 1, int pageSize = 10)
        {
            IQueryable<PostReport> query = _context.PostReports
                .Include(r => r.Post.Member)
                .ThenInclude(m => m.Punishments)
                .ThenInclude(p => p.Type)
                .Include(r => r.Reporter)
                .Include(r => r.Rule)
                .Include(r => r.Status)
                .Include(r => r.Result);

            // 🔍 關鍵字搜尋（文章標題 + 檢舉人）
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(r =>
                    r.Post.Title.Contains(keyword) ||
                    r.Reporter.UserName.Contains(keyword));
            }

            // 📆 時間區間搜尋（以 ReportTime 為主）
            if (startDate.HasValue)
            {
                query = query.Where(r => r.ReportTime >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(r => r.ReportTime <= endDate.Value);
            }

            var totalCount = await query.CountAsync();

            var reports = await query
                .OrderByDescending(r => r.ReportTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new PostReportViewModel
                {
                    Id = r.Id,
                    PostId = r.PostId,
                    PostTitle = r.Post.Title,
                    ReporterId = r.ReporterId,
                    ReporterName = r.Reporter.UserName,
                    PosterId = r.Post.MemberId,
                    PosterName = r.Post.Member.UserName,
                    RuleName = r.Rule.Name,
                    ReportTime = r.ReportTime,
                    StatusName = r.Status.Name,
                    ResultName = r.Result != null ? r.Result.Name : "尚未審查",
                }).ToListAsync();

            var pagedList = new StaticPagedList<PostReportViewModel>(reports, page, pageSize, totalCount);

            var viewModel = new QueryViewModel<PostReportViewModel>
            {
                Keyword = keyword,
                StartDate = startDate,
                EndDate = endDate,
                Page = page,
                PageSize = pageSize,
                Items = pagedList
            };

            return View(viewModel);
        }


        // 處理文章檢舉（後台）
        // 表單建議只傳 id(檢舉單Id), resultId(1違規/2無違規/3無效), deletePost(bool)
        // reporterId/postId 都可以從 report 導覽取得，避免傳錯
        [HttpPost]
        public async Task<IActionResult> HandlePostReport(int id, int resultId, bool deletePost = false)
        {
            // 抓檢舉單 + 導覽文章
            var report = await _context.PostReports
                .Include(r => r.Post)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (report == null) return NotFound("找不到檢舉單");

            var post = report.Post ?? await _context.Posts.FindAsync(report.PostId);
            if (post == null) return NotFound("找不到該文章");

            var offenderId = post.MemberId;     // 違規者：文章作者
            var reporterId = report.ReporterId; // 檢舉者（可能為 null）

            // 用名稱查通知型別 Id，避免魔法數字
            var typeIdReportProcessed = await _context.NotificationTypes
                .Where(t => t.Name == "檢舉處理結果")
                .Select(t => t.Id)
                .FirstOrDefaultAsync();
            var typeIdPunishment = await _context.NotificationTypes
                .Where(t => t.Name == "懲處通知")
                .Select(t => t.Id)
                .FirstOrDefaultAsync();

            if (typeIdReportProcessed == 0 || typeIdPunishment == 0)
            {
                // 你可以改成自動種資料；這裡用 500 提醒先初始化 NotificationTypes
                return StatusCode(500, "請先於 NotificationTypes 種『檢舉處理結果』與『懲處通知』兩筆資料");
            }

            // 轉成名稱，發通知比較友善
            var resultName = await _context.ReportResults
                .Where(x => x.Id == resultId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync() ?? "已處理";

            using var tx = await _context.Database.BeginTransactionAsync();

            // 1) 更新檢舉單
            report.ResultId = resultId;
            report.StatusId = 2; // 已處理
            await _context.SaveChangesAsync();

            var now = DateTime.Now;

            // 2) 若為違規（1），懲處文章作者 + 通知作者
            if (resultId == 1)
            {
                // 這裡假設 Punishments.TypeId: 1=警告, 2=禁言（請確認表/常數）
                var warningCount = await _context.Punishments
                    .CountAsync(p => p.MemberId == offenderId && p.TypeId == 1);

                var punishTypeId = warningCount >= 3 ? 2 : 1; // 第四次開始禁言
                DateTime? endTime = punishTypeId == 2 ? now.AddDays(3) : now.AddDays(7);

                _context.Punishments.Add(new Punishment
                {
                    MemberId = offenderId,
                    TypeId = punishTypeId,
                    Description = $"文章(ID:{post.Id})「{post.Title}」違規，處以{(punishTypeId == 2 ? "禁言" : "警告")}。",
                    StartTime = now,
                    EndTime = endTime,
                    IsActive = true
                });

                // 通知違規者（文章作者）
                _context.Notifications.Add(new Notification
                {
                    MemberId = offenderId,
                    TypeId = typeIdPunishment, // ← 不再寫死 1
                    NotificationText = $"您的文章「{post.Title}」違規，第 {warningCount + 1} 次，已處以{(punishTypeId == 2 ? "禁言 3 天" : "警告")}。",
                    IsRead = false,
                    Time = now
                });

                // 如需刪文
                if (deletePost)
                {
                    post.IsDeleted = true;
                    post.StatusId = 2;
                }
            }

            // 3) 無論結果為何，通知檢舉者處理結果
            if (reporterId.HasValue)
            {
                _context.Notifications.Add(new Notification
                {
                    MemberId = reporterId.Value,
                    TypeId = typeIdReportProcessed, // ← 「檢舉處理結果」
                    NotificationText = $"您對「{post.Title}」的檢舉處理結果：{resultName}",
                    IsRead = false,
                    Time = now
                });
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["SuccessMessage"] = "檢舉已成功處理。";
            return RedirectToAction("PostReports");
        }


        //留言檢舉
        public async Task<IActionResult> CommentReports(
    string? keyword,
    DateTime? startDate,
    DateTime? endDate,
    int page = 1,
    int pageSize = 10)
        {
            IQueryable < CommentReport >query = _context.CommentReports
                .Include(r => r.Comment)
                    .ThenInclude(c => c.Member)
                        .ThenInclude(m => m.Punishments)
                        .ThenInclude(p => p.Type)
                .Include(r => r.Comment.Post) // 拿文章標題
                .Include(r => r.Reporter)
                .Include(r => r.Rule)
                .Include(r => r.Status)
                .Include(r => r.Result);

            // 🔍 關鍵字搜尋：留言內容 / 檢舉人帳號 / 所屬文章
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(r =>
                    r.Comment.CommentText.Contains(keyword) ||
                    r.Reporter.UserName.Contains(keyword) ||
                    r.Comment.Post.Title.Contains(keyword));
            }

            // 📅 時間區間搜尋
            if (startDate.HasValue)
            {
                query = query.Where(r => r.ReportTime >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(r => r.ReportTime <= endDate.Value);
            }

            // 📊 總筆數
            var totalCount = await query.CountAsync();

            // 📄 分頁資料
            var reports = await query
                .OrderByDescending(r => r.ReportTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new CommentReportViewModel
                {
                    Id = r.Id,
                    CommentId = r.CommentId,
                    CommentText = r.Comment.CommentText,
                    PostTitle = r.Comment.Post.Title,
                    ReporterId = r.ReporterId,
                    ReporterName = r.Reporter.UserName,
                    CommenterId = r.Comment.MemberId,
                    CommenterName = r.Comment.Member.UserName,
                    RuleName = r.Rule.Name,
                    ReportTime = r.ReportTime,
                    StatusName = r.Status.Name,
                    ResultName = r.Result != null ? r.Result.Name : "尚未審查",
                    
                }).ToListAsync();

            var pagedList = new StaticPagedList<CommentReportViewModel>(reports, page, pageSize, totalCount);

            var viewModel = new QueryViewModel<CommentReportViewModel>
            {
                Keyword = keyword,
                StartDate = startDate,
                EndDate = endDate,
                Page = page,
                PageSize = pageSize,
                Items = pagedList
            };

            return View(viewModel);
        }

        //留言檢舉處理
        [HttpPost]
        public async Task<IActionResult> HandleCommentReport(int id, int? resultId, int commentId, int? reporterId)
        {
            var report = await _context.CommentReports.FindAsync(id);
            if (report == null) return NotFound();

            var comment = await _context.Comments
                .Include(c => c.Member)
                .Include(c => c.Post)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null) return NotFound("找不到該留言");

            // 更新檢舉審查狀態與結果
            report.ResultId = resultId;
            report.StatusId = 2; // 已處理
            await _context.SaveChangesAsync();

            // 若違規 → 懲罰 + 通知
            if (resultId == 1 && reporterId.HasValue)
            {
                var now = DateTime.Now;

                // 查詢過去警告次數
                var warningCount = await _context.Punishments
                    .CountAsync(p => p.MemberId == reporterId.Value && p.TypeId == 1);

                int punishTypeId = warningCount >= 3 ? 2 : 1;
                DateTime? endTime = punishTypeId == 2 ? now.AddDays(3) : now.AddDays(7);

                // 加入懲罰
                _context.Punishments.Add(new Punishment
                {
                    MemberId = reporterId.Value,
                    TypeId = punishTypeId,
                    Description = $"您因發表違規留言「{comment.CommentText}」，已被系統處以 {(punishTypeId == 2 ? "禁言" : "警告")}。",
                    StartTime = now,
                    EndTime = endTime,
                    IsActive = true
                });

                // 加入通知
                _context.Notifications.Add(new Notification
                {
                    MemberId = reporterId.Value,
                    NotificationText = $"您因違規留言（於文章「{comment.Post.Title}」）第 {(warningCount + 1)} 次違規，已被處以 {(punishTypeId == 2 ? "禁言 3 天" : "警告")}。",
                    IsRead = false,
                    Time = now,
                    TypeId = 1
                });

                // 是否刪除留言
                if (Request.Form["DeleteComment"] == "true")
                {
                    comment.IsDeleted = true;
                }

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "留言檢舉已成功處理。";
            return RedirectToAction("CommentReports");
        }


        //公告管理
        public async Task<IActionResult> Announcements(string? keyword, DateTime? startDate, DateTime? endDate, int page = 1, int pageSize = 5)
        {
            // 1. 建立查詢條件
            var query = _context.Announcements
                .Include(p => p.Supervisor)
                .Include(p => p.Status)
                .Include(p => p.LastEditByNavigation)
                .Where(p => !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(p =>
                    p.Title.Contains(keyword) ||
                    p.Supervisor.Account.Contains(keyword));
            }

            if (startDate.HasValue)
            {
                query = query.Where(p => p.PostTime >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(p => p.PostTime <= endDate.Value);
            }

            // 2. 計算總筆數（非同步）
            var totalCount = await query.CountAsync();

            // 3. 取出當頁資料（非同步）
            var pagedPosts = await query
                .OrderByDescending(p => p.PostTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new AnnouncementsViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Supervisor = p.Supervisor != null ? p.Supervisor.Account: "未知",
                    PostTime = p.PostTime,
                    LastEditor=p.LastEditBy!=null?p.LastEditByNavigation.Account : "-",
                    LastEditTime = p.LastEditTime,
                    StatusName = p.Status != null ? p.Status.Name : "未知"
                })
                .ToListAsync();

            // 4. 包裝成分頁物件
            var pagedList = new StaticPagedList<AnnouncementsViewModel>(pagedPosts, page, pageSize, totalCount);

            // 5. 組合 ViewModel
            var viewModel = new QueryViewModel<AnnouncementsViewModel>
            {
                Keyword = keyword,
                StartDate = startDate,
                EndDate = endDate,
                Page = page,
                PageSize = pageSize,
                Items = pagedList // 注意這裡要是 IPagedList<AnnouncementsViewModel>
            };

            return View(viewModel);
        }



        //懲罰
        public async Task<IActionResult> Punishments(string? keyword, DateTime? startDate, DateTime? endDate, bool? isActive, int page = 1, int pageSize = 10)
        {
            var now = DateTime.Now; // 若你全站用 UTC，改成 DateTime.UtcNow

            var query = _context.Punishments
                .Include(p => p.Member)
                .Include(p => p.Type) // 沒有類型表就拿掉
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(p => p.Member.UserName.Contains(keyword) || p.Description.Contains(keyword));

            if (startDate.HasValue) query = query.Where(p => p.StartTime >= startDate.Value);
            if (endDate.HasValue) query = query.Where(p => p.StartTime <= endDate.Value);
            if (isActive.HasValue) query = query.Where(p => p.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();

            var pagedItems = await query
                .OrderByDescending(p => p.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PunishmentsViewModel
                {
                    Id = p.Id,
                    Member = p.Member != null ? p.Member.UserName : "未知",
                    TypeName = p.Type != null ? p.Type.Name : p.TypeId.ToString(),
                    Description = p.Description,
                    StartTime = p.StartTime,
                    EndTime = p.EndTime,
                    IsActive = p.IsActive,
                    IsCurrentlyEffective = p.StartTime <= now && (p.EndTime == null || p.EndTime >= now)
                })
                .ToListAsync();

            var pagedList = new StaticPagedList<PunishmentsViewModel>(pagedItems, page, pageSize, totalCount);

            var viewModel = new QueryViewModel<PunishmentsViewModel>
            {
                Keyword = keyword,
                StartDate = startDate,
                EndDate = endDate,
                Page = page,
                PageSize = pageSize,
                Items = pagedList
            };

            ViewBag.IsActive = isActive;
            return View(viewModel);
        }

        //解除懲處
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(
    int id,
    string? keyword,
    DateTime? startDate,
    DateTime? endDate,
    bool? isActive,
    int page = 1,
    int pageSize = 10)
        {
            var item = await _context.Punishments.FindAsync(id);
            if (item == null) return NotFound();

            var now = DateTime.Now; // 若全站用 UTC，改用 DateTime.UtcNow
            item.IsActive = false;
            if (item.EndTime == null || item.EndTime > now)
                item.EndTime = now;

            await _context.SaveChangesAsync();

            return RedirectToAction("Punishments", new
            {
                keyword,
                startDate = startDate?.ToString("yyyy-MM-dd"),
                endDate = endDate?.ToString("yyyy-MM-dd"),
                isActive = isActive?.ToString().ToLower(),
                page,
                pageSize
            });
        }










        //顯示公告詳細頁面
        public IActionResult ShowAnnouncement(int id)
        {
            var ann = _context.Announcements
                .Include(p => p.Supervisor)
                .Include(p => p.Status)
                .Include(p => p.LastEditByNavigation)
                .Where(p => !p.IsDeleted)
                .FirstOrDefault(p => p.Id == id && !p.IsDeleted);

            if (ann == null)
            {
                return NotFound();
            }

            return View(ann); // 對應ShowAnnouncement.cshtml
        }

        //彈出新增公告編輯器
        public IActionResult CreateAnnouncement()
        {
            return View("announcementEditor");
        }

        //送出新公告
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAnnouncement(Announcement model)
        {
            if (!ModelState.IsValid)
            {
                // 若驗證失敗，重新載入編輯器（保留資料）
                return View("announcementEditor", model);
            }

            model.PostTime = DateTime.Now;
            model.SupervisorId = 1; // TODO: 改為從登入的管理員取得
            model.LastEditTime = null;
            model.LastEditBy = null;
            model.IsDeleted = false;

            _context.Announcements.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "true";
            return View("announcementEditor");


        }

        // 小工具：用名稱取 PunishmentType.Id，若沒種資料就丟清楚的錯
        private async Task<int> GetPunishTypeIdAsync(string name)
        {
            var id = await _context.PunishmentTypes
                .Where(t => t.Name == name)
                .Select(t => t.Id)
                .FirstOrDefaultAsync();
            if (id == 0) throw new InvalidOperationException($"PunishmentType '{name}' 不存在，請先於 PunishmentTypes 種資料。");
            return id;
        }


    }
}
