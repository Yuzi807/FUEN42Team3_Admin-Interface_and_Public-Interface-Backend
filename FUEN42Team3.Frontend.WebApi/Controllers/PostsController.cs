using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Helpers;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Ganss.Xss;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly HtmlSanitizer _sanitizer;


        public PostsController(AppDbContext context, IMemoryCache cache,HtmlSanitizer sanitizer)
        {
            _context = context;
            _cache = cache;
            _sanitizer = sanitizer;


        }

        // GET: api/Posts
        // 文章列表 搜尋+分頁
        [HttpGet]
        public async Task<ActionResult<PageResult<PostDto>>> GetPosts(
            string? keyword, int? postTypeId, string? tag, int page = 1, int pageSize = 2)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 2;

            var query = _context.Posts
                .Include(p => p.Member) // 包含會員資訊
                .Include(p => p.Type)   // 包含文章類型資訊
                .Include(p => p.Tags)
                .Where(p => !p.IsDeleted && p.StatusId == 1); // 軟刪除過濾

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(p => p.Title.Contains(keyword) ||
                                         p.Member.UserName.Contains(keyword));
            }

            if (postTypeId.HasValue && postTypeId > 0)
            {
                query = query.Where(p => p.TypeId == postTypeId.Value);
            }

            // 依標籤（只允許啟用的標籤；禁用或不存在 → 404 或改回空集合）
            if (!string.IsNullOrWhiteSpace(tag))
            {
                var tagEntity = await _context.Tags
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Name == tag.Trim());

                if (tagEntity == null || !tagEntity.IsActive)
                {
                    return NotFound("標籤不存在或已被停用");
                }
                query = query.Where(p => p.Tags.Any(t => t.Id == tagEntity.Id));
            }

            var totalCount = await query.CountAsync();

            var items = await query
    .OrderByDescending(p => p.PostTime ?? DateTime.MinValue)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(p => new PostDto
    {
        PostType = p.Type.Name,
        Id = p.Id,
        Title = p.Title,
        Author = p.Member.UserName,
        PostTime = p.PostTime ?? DateTime.MinValue,
        NumOfHits = p.NumOfHits,
        NumOfGoods = p.NumOfGoods,
        LastEditTime = p.LastEditTime,
        CoverImage = p.ImageUrl,
        Summary = CreateSummary(p.PostContent, 60)
    })
    .ToListAsync();

            return new PageResult<PostDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        // 摘要方法
        private static string CreateSummary(string? html, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            var plainText = Regex.Replace(html, "<.*?>", ""); // 去除 HTML 標籤
            return plainText.Length > maxLength
                ? plainText.Substring(0, maxLength) + "..."
                : plainText;
        }


        // GET: api/Posts/{id}
        // 文章詳情
        [HttpGet("{id}")]
        public async Task<ActionResult<PostDetailDto>> GetPostDetail(int id)
        {
            // 嘗試取得登入者 Id（未登入就會是 null）
            int? currentMemberId = null;
            var memberIdClaim = User.Claims.FirstOrDefault(c => c.Type == "MemberId");
            if (memberIdClaim != null)
                currentMemberId = int.Parse(memberIdClaim.Value);

            var post = await _context.Posts
                .Include(p => p.Member)
                .ThenInclude(m => m.MemberProfiles)
                .Include(p => p.Type)
                .Include(p => p.Status)
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (post == null) return NotFound();

            // ✅ 不是公開文章 → 只有作者本人能看
            if (post.StatusId != 1)
            {
                if (!currentMemberId.HasValue || post.MemberId != currentMemberId.Value)
                    return Forbid();
            }
            // ====== 點閱數防刷機制 ======
            // key 用文章ID + 使用者ID 或 IP 來區分
            var cacheKey = $"PostHit_{id}_{currentMemberId ?? 0}_{HttpContext.Connection.RemoteIpAddress}";

            if (!_cache.TryGetValue(cacheKey, out _))
            {
                post.NumOfHits++;
                await _context.SaveChangesAsync();

                // 設定快取有效時間，例如 5分鐘內重複點同一篇文章，不會再加次數
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                _cache.Set(cacheKey, true, cacheOptions);
            }
            // 判斷是否已按讚/收藏（要登入才判斷）
            bool isLiked = false, isFavorited = false, hasOpenReport = false;
            if (currentMemberId.HasValue)
            {
                isLiked = await _context.PostGoods
                    .AnyAsync(pg => pg.PostId == id && pg.MemberId == currentMemberId.Value);

                isFavorited = await _context.PostFavorites
                    .AnyAsync(f => f.PostId == id && f.MemberId == currentMemberId.Value);

                hasOpenReport = await _context.PostReports
                    .AnyAsync(r => r.PostId == id && r.ReporterId == currentMemberId.Value && r.StatusId == 1);
            }

            var dto = new PostDetailDto
            {
                Id = post.Id,
                Title = post.Title,
                PostContent = post.PostContent,
                CoverImage = post.ImageUrl,
                PostTypeId = post.TypeId,
                PostType = post.Type?.Name,
                StatusId = post.StatusId,
                StatusName = post.Status?.Name,
                MemberId = post.MemberId,
                AuthorUserName = post.Member?.UserName,
                NumOfHits = post.NumOfHits,
                NumOfGoods = post.NumOfGoods,
                PostTime = post.PostTime ?? DateTime.MinValue,
                // ★ 只帶出啟用標籤
                Tags = post.Tags
            .Where(t => t.IsActive)                
            .Select(t => t.Name)
            .ToList(),
                LastEditTime = post.LastEditTime,
                HasOpenReport = hasOpenReport,
                IsLiked = isLiked,
                IsFavorited = isFavorited,
                // ✅ 這裡就能拿得到，因為前面 Include 了 MemberProfiles
                AuthorPhoto = post.Member?.MemberProfiles
        ?.OrderByDescending(mp => mp.UpdatedAt)
        .ThenByDescending(mp => mp.Id)
        .Select(mp => mp.Photo)
        .FirstOrDefault()


            };

            return Ok(dto);
        }



        // POST: api/Posts  （發文 / 草稿）
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] PostCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // 嚴格用 Claims 身分
            var memberId = User.GetCurrentMemberId();
            if (!memberId.HasValue) return Unauthorized("請先登入");

            if (dto.StatusId != 1 && dto.StatusId != 2)
                return BadRequest("無效的狀態：只能為 1（公開）或 2（草稿）");

            if (dto.StatusId == 1)
            {
                if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest("標題不可空白");
                if (string.IsNullOrWhiteSpace(StripHtml(dto.PostContent))) return BadRequest("內容不可空白");
            }

            // ✅ 在寫入前做白名單消毒
            var safeHtml = string.IsNullOrWhiteSpace(dto.PostContent)
                ? ""
                : _sanitizer.Sanitize(dto.PostContent);

            var post = new Post
            {
                Title = dto.Title?.Trim() ?? "",
                PostContent = safeHtml, // ✅ 存入消毒後的版本
                ImageUrl = dto.CoverImage,
                TypeId = dto.PostTypeId,
                StatusId = dto.StatusId,
                PostTime = dto.StatusId == 1 ? DateTime.Now : (DateTime?)null,
                LastEditTime = DateTime.Now,
                NumOfHits = 0,
                NumOfGoods = 0,
                MemberId = memberId.Value, // <<<<<< 這裡用 claims
                IsDeleted = false
            };

            foreach (var name in dto.Tags.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == name);
                if (tag == null)
                {
                    tag = new Tag { Name = name, IsActive = true };
                    _context.Tags.Add(tag);
                    await _context.SaveChangesAsync();
                }
                post.Tags.Add(tag);
            }

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            await AddHistoryAsync(post, dto.Tags, "create");

            return Ok(new { id = post.Id, status = post.StatusId });
        }



        // PUT: api/Posts/{id}  （編輯 / 草稿轉公開）
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePost(int id, [FromBody] PostUpdateDto dto)
        {
            if (id != dto.Id) return BadRequest("Id 不一致");

            var currentMemberId = User.GetCurrentMemberId();
            if (!currentMemberId.HasValue) return Unauthorized("請先登入");

            var post = await _context.Posts
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (post == null) return NotFound();

            // 只有作者本人能編輯（可依需求調整）
            if (post.MemberId != currentMemberId.Value) return Forbid();

            // 編輯前 → 先把「舊版本」寫進歷史
            await AddHistoryAsync(post, post.Tags.Select(t => t.Name).ToList(), "update-before");

            // 若將要公開，做最基本驗證
            if (dto.StatusId == 1)
            {
                if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest("標題不可空白");
                if (string.IsNullOrWhiteSpace(StripHtml(dto.PostContent))) return BadRequest("內容不可空白");
            }

            // ✅ 在寫入前做白名單消毒
            var safeHtml = string.IsNullOrWhiteSpace(dto.PostContent)
                ? ""
                : _sanitizer.Sanitize(dto.PostContent);

            // 更新本文
            post.Title = dto.Title?.Trim() ?? "";
            post.PostContent = safeHtml ?? "";
            post.ImageUrl = dto.CoverImage;
            post.TypeId = dto.PostTypeId;

            // 狀態：草稿→公開時，若 PostTime 尚未給值，補上發佈時間
            var wasDraft = post.StatusId != 1;
            post.StatusId = dto.StatusId;
            if (wasDraft && dto.StatusId == 1 && post.PostTime == null)
            {
                post.PostTime = DateTime.Now;
            }

            post.LastEditTime = DateTime.Now;

            // Tags：先清 → 再建關聯（保證同步）
            post.Tags.Clear();
            foreach (var name in dto.Tags.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == name);
                if (tag == null)
                {
                    tag = new Tag { Name = name, IsActive = true };
                    _context.Tags.Add(tag);
                    await _context.SaveChangesAsync();
                }
                post.Tags.Add(tag);
            }

            await _context.SaveChangesAsync();

            // 也可在「更新後」再記一筆（若想保留每一次「結果版」）
            // await AddHistoryAsync(post, dto.Tags, "update-after");

            return Ok(new { message = "文章已更新", editTime = post.LastEditTime });
        }





        // DELETE: api/Postss/5
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            var currentMemberId = User.GetCurrentMemberId();
            if (!currentMemberId.HasValue) return Unauthorized("請先登入");

            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (post == null) return NotFound("文章不存在或已刪除");

            // 只能刪自己的文章
            if (post.MemberId != currentMemberId.Value) return Forbid();

            post.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "文章已刪除" });
        }


        // GET: api/Posts/{id}/history  （查某篇文章的歷史）
        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetPostHistory(int id)
        {
            var list = await _context.PostHistories
                .Where(h => h.PostId == id)
                .OrderByDescending(h => h.EditTime)
                .Select(h => new
                {
                    h.Id,
                    h.PostId,
                    h.EditTime,
                    h.Snapshot
                })
                .ToListAsync();

            return Ok(list);
        }


        //文章按讚
        // POST: api/Posts/{id}/like
        [Authorize]
        [HttpPost("{id}/like")]
        public async Task<IActionResult> ToggleLike(int id)
        {
            var memberId = User.GetCurrentMemberId();
            if (!memberId.HasValue) return Unauthorized("請先登入");

            var post = await _context.Posts.FindAsync(id);
            if (post == null || post.IsDeleted) return NotFound();

            var existing = await _context.PostGoods
                .FirstOrDefaultAsync(pg => pg.PostId == id && pg.MemberId == memberId.Value);

            bool isLiked;
            if (existing == null)
            {
                _context.PostGoods.Add(new PostGood { PostId = id, MemberId = memberId.Value, CreatedTime = DateTime.Now });
                post.NumOfGoods++;
                isLiked = true;
            }
            else
            {
                _context.PostGoods.Remove(existing);
                post.NumOfGoods--;
                isLiked = false;
            }

            await _context.SaveChangesAsync();

            return Ok(new { post.Id, post.NumOfGoods, isLiked });
        }
        [Authorize]
        //文章收藏
        [HttpPost("favorite/{id:int}")]
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            var memberId = User.GetCurrentMemberId();
            if (!memberId.HasValue) return Unauthorized("請先登入");

            var post = await _context.Posts.FindAsync(id);
            if (post == null || post.IsDeleted) return NotFound();

            var existing = await _context.PostFavorites
                .FirstOrDefaultAsync(f => f.PostId == id && f.MemberId == memberId.Value);

            bool isFavorited;
            if (existing == null)
            {
                _context.PostFavorites.Add(new PostFavorite { PostId = id, MemberId = memberId.Value, CreatedTime = DateTime.Now });
                isFavorited = true;
            }
            else
            {
                _context.PostFavorites.Remove(existing);
                isFavorited = false;
            }

            await _context.SaveChangesAsync();

            return Ok(new { post.Id, isFavorited });
        }

        // POST: api/Posts/{id}/report  送出檢舉（需登入）
        [Authorize]
        [HttpPost("{id}/report")]
        public async Task<IActionResult> ReportPost(int id, [FromBody] PostReportCreateDto dto)
        {
            var memberId = User.GetCurrentMemberId();
            if (!memberId.HasValue) return Unauthorized("請先登入");

            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (post == null) return NotFound("文章不存在");

            var ruleExists = await _context.Rules.AnyAsync(r => r.Id == dto.RuleId);
            if (!ruleExists) return BadRequest("無效的檢舉規則");

            // 防重複：同一使用者對同一篇文章，若有「未處理」的檢舉就不再新增
            bool alreadyOpen = await _context.PostReports.AnyAsync(r =>
                r.PostId == id && r.ReporterId == memberId.Value && r.StatusId == 1);
            if (alreadyOpen) return Conflict("您已檢舉過此文章，仍在處理中");

            var report = new PostReport
            {
                PostId = id,
                ReporterId = memberId.Value,
                RuleId = dto.RuleId,
                ReportTime = DateTime.Now,
                StatusId = 1,     // 1=未處理
                ResultId = null   // 尚無結果
            };

            _context.PostReports.Add(report);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPostDetail), new { id = post.Id }, new
            {
                reportId = report.Id,
                statusId = report.StatusId
            });
        }

        //for Member頁面
        //取使用者的公開發文
        [Authorize]
        [HttpGet("my")]
        public async Task<ActionResult<PageResult<MyPostRowDto>>> GetMyPosts(
    int? statusId = 1,
    bool? isDeleted = false,
    int page = 1,
    int pageSize = 10,
    int? postTypeId = null,
    string? keyword = null,
    [FromQuery(Name = "q")] string? q = null,
    [FromQuery(Name = "orderBy")] string? orderBy = "newest",
    [FromQuery(Name = "sort")] string? sort = null
)
        {
            try
            {
                var memberIdClaim = User.FindFirst("MemberId")?.Value;
                if (string.IsNullOrEmpty(memberIdClaim))
                    return Unauthorized("請先登入");

                int memberId = int.Parse(memberIdClaim);

                if (page <= 0) page = 1;
                if (pageSize <= 0 || pageSize > 100) pageSize = 10;

                var key = keyword ?? q;
                var ord = (sort ?? orderBy ?? "newest").ToLowerInvariant();

                var query = _context.Posts
                    .Include(p => p.Type)
                    .Include(p => p.Comments)
                    .Where(p => p.MemberId == memberId);

                if (isDeleted.HasValue)
                    query = query.Where(p => p.IsDeleted == isDeleted.Value);

                if (statusId.HasValue)
                    query = query.Where(p => p.StatusId == statusId.Value);

                if (postTypeId.HasValue && postTypeId > 0)
                    query = query.Where(p => p.TypeId == postTypeId.Value);

                if (!string.IsNullOrWhiteSpace(key))
                    query = query.Where(p => p.Title.Contains(key) || p.PostContent.Contains(key));

                // ✅ 排序：直接用 coalesce，但保留 null，避免 DateTime.MinValue
                query = ord switch
                {
                    "oldest" => query.OrderBy(p => p.PostTime ?? p.LastEditTime),
                    "mostviews" => query.OrderByDescending(p => p.NumOfHits)
                                        .ThenByDescending(p => p.PostTime ?? p.LastEditTime),
                    "mostcomments" => query.OrderByDescending(p => p.Comments.Count())
                                           .ThenByDescending(p => p.PostTime ?? p.LastEditTime),
                    _ => query.OrderByDescending(p => p.LastEditTime ?? p.PostTime),
                };

                var totalCount = await query.CountAsync();

                var items = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new MyPostRowDto
                    {
                        Id = p.Id,
                        Title = p.Title,
                        PostType = p.Type != null ? p.Type.Name : "未分類",
                        CoverImage = p.ImageUrl,
                        NumOfHits = p.NumOfHits,
                        NumOfGoods = p.NumOfGoods,
                        PostTime = p.PostTime,               // ❌ 不轉換，直接回傳原始值（可 null）
                        LastEditTime = p.LastEditTime,       // ❌ 不轉換，直接回傳原始值（可 null）
                        CommentCount = p.Comments.Count(),
                        Summary = !string.IsNullOrEmpty(p.PostContent) ? CreateSummary(p.PostContent, 110) : "",
                        MemberId = p.MemberId
                    })
                    .ToListAsync();

                return Ok(new PageResult<MyPostRowDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ GetMyPosts 發生錯誤：" + ex);
                return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("favorites")]
        public async Task<ActionResult<PageResult<MyPostRowDto>>> GetMyFavorites(
    int page = 1,
    int pageSize = 10
)
        {
            var memberIdClaim = User.FindFirst("MemberId")?.Value;
            if (string.IsNullOrEmpty(memberIdClaim))
                return Unauthorized("請先登入");

            int memberId = int.Parse(memberIdClaim);

            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            var query = _context.PostFavorites
                .Include(f => f.Post)
                .ThenInclude(p => p.Type)
                .Where(f => f.MemberId == memberId && f.Post != null && !f.Post.IsDeleted);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(f => f.CreatedTime)   // ✅ 收藏時間新到舊
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new MyPostRowDto
                {
                    Id = f.Post.Id,
                    Title = f.Post.Title,
                    PostType = f.Post.Type != null ? f.Post.Type.Name : "未分類",
                    CoverImage = f.Post.ImageUrl,
                    NumOfHits = f.Post.NumOfHits,
                    NumOfGoods = f.Post.NumOfGoods,
                    PostTime = f.Post.PostTime,
                    LastEditTime = f.Post.LastEditTime,
                    CommentCount = f.Post.Comments.Count(),
                    Summary = CreateSummary(f.Post.PostContent, 110),
                    MemberId = f.Post.MemberId
                })
                .ToListAsync();

            return Ok(new PageResult<MyPostRowDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }



        // ====== 私有工具 ======

        /// <summary>
        /// 將目前 Post 的狀態寫入歷史表（Snapshot 以 JSON 儲存）
        /// </summary>
        private async Task AddHistoryAsync(Post post, IEnumerable<string> tagNames, string action)
        {
            var snapshotObj = new
            {
                Action = action,               // create / update-before / update-after（可自行定義）
                PostId = post.Id,
                Title = post.Title,
                PostContent = post.PostContent,
                ImageUrl = post.ImageUrl,
                TypeId = post.TypeId,
                StatusId = post.StatusId,
                PostTime = post.PostTime,      // 可能為 null（草稿）
                LastEditTime = post.LastEditTime,
                Tags = tagNames?.ToList() ?? new List<string>()
            };

            var json = JsonSerializer.Serialize(snapshotObj, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            });

            _context.PostHistories.Add(new PostHistory
            {
                PostId = post.Id,
                EditTime = DateTime.Now,
                Snapshot = json
            });

            await _context.SaveChangesAsync();
        }

        private static string StripHtml(string? html)
            => string.IsNullOrWhiteSpace(html)
               ? ""
               : System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", "")
                   .Replace("&nbsp;", " ")
                   .Trim();
    }
}

