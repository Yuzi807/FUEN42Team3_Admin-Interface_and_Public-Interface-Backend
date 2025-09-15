using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Helpers;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CommentsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 取得某篇文章的留言 (樹狀結構, 最多 3 層)
        /// </summary>
        [HttpGet("post/{postId}")]
        public async Task<IActionResult> GetComments(int postId, int maxDepth = 3)
        {
            var currentMemberId = User.GetCurrentMemberId();

            // 撈出留言
            var comments = await _context.Comments
                .Where(c => c.PostId == postId && !c.IsDeleted)
                .Include(c => c.Member)
                .ThenInclude(m => m.MemberProfiles)
                .OrderBy(c => c.CommentTime)
                .ToListAsync();

            // 撈出目前使用者按過讚的留言 Id，避免 N+1
            var likedIds = currentMemberId.HasValue
                ? await _context.CommentGoods
                    .Where(g => g.MemberId == currentMemberId.Value && g.Comment.PostId == postId)
                    .Select(g => g.CommentId)
                    .ToListAsync()
                : new List<int>();

            // 建立查詢用字典
            var lookup = comments.ToDictionary(c => c.Id, c => new CommentDto
            {
                Id = c.Id,
                CommentText = c.CommentText,
                CommentTime = c.CommentTime,
                MemberId = c.MemberId,
                MemberName = c.Member.UserName,
                ReplyToCommentId = c.ReplyToCommentId,
                NumOfGoods = c.NumOfGoods,
                IsLiked = likedIds.Contains(c.Id),
                Replies = new List<CommentDto>(),
                // ✅ 取留言者頭像
                MemberPhoto = c.Member.MemberProfiles
                    .OrderByDescending(mp => mp.UpdatedAt)
                    .ThenByDescending(mp => mp.Id)
                    .Select(mp => mp.Photo)
                    .FirstOrDefault(),
            });

            // 組成樹狀
            List<CommentDto> roots = new();
            foreach (var c in comments)
            {
                if (c.ReplyToCommentId == null)
                {
                    roots.Add(lookup[c.Id]);
                }
                else if (lookup.ContainsKey(c.ReplyToCommentId.Value))
                {
                    lookup[c.ReplyToCommentId.Value].Replies.Add(lookup[c.Id]);
                }
            }

            // 限制層數 (避免無限巢狀)
            void TrimDepth(List<CommentDto> list, int depth)
            {
                if (depth >= maxDepth)
                {
                    foreach (var item in list)
                        item.Replies = new(); // 清掉更深層
                    return;
                }

                foreach (var item in list)
                    TrimDepth(item.Replies, depth + 1);
            }

            TrimDepth(roots, 1);

            return Ok(roots);
        }

        /// <summary>
        /// 新增留言或回覆
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateComment([FromBody] CreateCommentDto dto)
        {
            var memberId = User.GetCurrentMemberId();
            if (memberId is null) return Unauthorized();

            try
            {
                // 建立留言
                var entity = new Comment
                {
                    CommentText = dto.CommentText?.Trim() ?? "",
                    CommentTime = DateTime.Now,             // 需要 +8 可改 Now
                    PostId = dto.PostId,
                    MemberId = memberId.Value,
                    ReplyToCommentId = dto.ReplyToCommentId
                };
                _context.Comments.Add(entity);
                await _context.SaveChangesAsync();

                // 取會員 + 最新一筆 Profile 照片（用 Id 排序，避免 UpdatedAt 引發錯誤）
                var member = await _context.Members
                    .AsNoTracking()
                    .Where(m => m.Id == memberId.Value)
                    .Select(m => new
                    {
                        m.UserName, // 或 Account
                        Photo = m.MemberProfiles
                            .OrderByDescending(p => p.Id)     // ✅ 改用 Id，最穩
                            .Select(p => p.Photo)
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync();

                // 組絕對網址（若 DB 存檔名）
                string? photoUrl = string.IsNullOrWhiteSpace(member?.Photo)
                    ? null
                    : (member!.Photo.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? member.Photo
                        : $"{Request.Scheme}://{Request.Host}/uploads/profiles/{member.Photo}");

                // 回傳 201（或用 Ok 也行）
                return StatusCode(StatusCodes.Status201Created, new
                {
                    id = entity.Id,
                    memberName = member?.UserName ?? "",
                    commentText = entity.CommentText,
                    commentTime = entity.CommentTime,
                    memberPhoto = photoUrl,      // ✅ 關鍵
                    isLiked = false,
                    numOfGoods = 0,
                    replies = Array.Empty<object>(),
                    forceExpand = 0
                });
            }
            catch (Exception ex)
            {
                // 建議在控制器注入 ILogger<CommentsController> _logger 後打 Log
                // _logger.LogError(ex, "CreateComment failed, memberId={MemberId}", memberId);

                // 先把真正錯誤傳回給前端（開發期）
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }





        /// <summary>
        /// 留言按讚/取消
        /// </summary>
        [Authorize]
        [HttpPost("{id}/like")]
        public async Task<IActionResult> ToggleCommentLike(int id)
        {
            var memberId = User.GetCurrentMemberId();
            if (!memberId.HasValue) return Unauthorized("請先登入");

            var comment = await _context.Comments.FindAsync(id);
            if (comment == null || comment.IsDeleted) return NotFound("留言不存在");

            var existing = await _context.CommentGoods
                .FirstOrDefaultAsync(cg => cg.CommentId == id && cg.MemberId == memberId.Value);

            bool isLiked;
            if (existing == null)
            {
                _context.CommentGoods.Add(new CommentGood
                {
                    CommentId = id,
                    MemberId = memberId.Value,
                    CreatedTime = DateTime.Now
                });
                comment.NumOfGoods++;
                isLiked = true;
            }
            else
            {
                _context.CommentGoods.Remove(existing);
                if (comment.NumOfGoods > 0) comment.NumOfGoods--;
                isLiked = false;
            }

            await _context.SaveChangesAsync();

            return Ok(new { comment.Id, comment.NumOfGoods, isLiked });
        }
    }
}
