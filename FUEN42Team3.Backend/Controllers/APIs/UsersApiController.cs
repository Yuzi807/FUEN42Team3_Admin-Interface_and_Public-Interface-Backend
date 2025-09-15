using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.ViewModels;
using FUEN42Team3.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private const int SUPERADMIN_ROLE_ID = 1; // 超級管理員的 RoleId

        public UsersApiController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/UsersApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UsersViewModel>>> GetUsers()
        {
            // 查詢資料庫中的使用者，並關聯角色資料
            var users = await _context.Users
                .Include(u => u.Role)
                .Select(u => new UsersViewModel
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Account = u.Account,
                    RoleName = u.Role.RoleName,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    RoleId = u.RoleId // 添加 RoleId 以方便前端判斷
                })
                .OrderBy(u => u.Id)
                .ToListAsync();

            return users;
        }

        // GET: api/UsersApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UsersViewModel>> GetUser(int id)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Id == id)
                .Select(u => new UsersViewModel
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Account = u.Account,
                    RoleName = u.Role.RoleName,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    RoleId = u.RoleId // 添加 RoleId 以方便前端判斷
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }

            return user;
        }

        // POST: api/UsersApi
        [HttpPost]
        public async Task<ActionResult<UsersViewModel>> CreateUser([FromBody] UserCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // 檢查帳號是否已存在
            if (await _context.Users.AnyAsync(u => u.Account == model.Account))
            {
                return BadRequest("帳號已存在，請使用其他帳號");
            }

            // 檢查角色是否存在
            var role = await _context.UserRoles.FindAsync(model.RoleId);
            if (role == null)
            {
                return BadRequest("選擇的角色不存在");
            }

            // 檢查是否嘗試建立超級管理員
            if (model.RoleId == SUPERADMIN_ROLE_ID)
            {
                // 檢查系統中是否已經存在超級管理員
                var existingSuperAdmin = await _context.Users
                    .AnyAsync(u => u.RoleId == SUPERADMIN_ROLE_ID);
                
                if (existingSuperAdmin)
                {
                    return StatusCode(403, "系統中已存在超級管理員，不可建立多個超級管理員。");
                }
                
                // 如果嘗試創建超級管理員，強制設置為啟用狀態
                model.IsActive = true;
            }

            // 建立新的使用者
            var user = new User
            {
                UserName = model.UserName,
                Account = model.Account,
                Password = HashUtility.HashPassword(model.Password), // 使用雜湊密碼
                RoleId = model.RoleId,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 回傳建立成功的使用者資料
            var result = new UsersViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Account = user.Account,
                RoleName = role.RoleName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                RoleId = user.RoleId
            };

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, result);
        }

        // PUT: api/UsersApi/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserUpdateViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest("Id mismatch");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // 獲取當前登入用戶的 ID
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Forbid();
            }

            // 檢查是否為超級管理員
            bool isSuperAdmin = (user.RoleId == SUPERADMIN_ROLE_ID);
            bool isSelfOperation = (currentUserId == id);
            
            // 如果是超級管理員且不是自己在操作
            if (isSuperAdmin && !isSelfOperation)
            {
                return StatusCode(403, "無法修改超級管理員的資料，只有超級管理員本人可以修改自己的資料。");
            }

            // 如果是超級管理員，特殊處理
            if (isSuperAdmin)
            {
                // 禁止超級管理員降級自己的角色
                if (model.RoleId != SUPERADMIN_ROLE_ID)
                {
                    // 檢查系統中是否還有其他超級管理員
                    var superAdminCount = await _context.Users.CountAsync(u => u.RoleId == SUPERADMIN_ROLE_ID);
                    if (superAdminCount <= 1)
                    {
                        return StatusCode(403, "系統必須保留至少一名超級管理員，無法降級唯一的超級管理員。");
                    }
                }
                
                // 禁止超級管理員停用自己
                if (!model.IsActive)
                {
                    return StatusCode(403, "超級管理員不能被停用。");
                }
                
                // 超級管理員只能修改自己的用戶名和密碼
                user.UserName = model.UserName;
                
                // 如果提供了新密碼，才更新密碼
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    user.Password = HashUtility.HashPassword(model.Password);
                }
                
                // 其他屬性保持不變
                // 不更新 RoleId 和 IsActive
            }
            else
            {
                // 非超級管理員的正常更新流程
                
                // 如果要將用戶角色改為超級管理員，需要特殊處理
                if (model.RoleId == SUPERADMIN_ROLE_ID && user.RoleId != SUPERADMIN_ROLE_ID)
                {
                    // 檢查系統中是否已經存在超級管理員
                    var existingSuperAdmin = await _context.Users
                        .AnyAsync(u => u.RoleId == SUPERADMIN_ROLE_ID);
                    
                    if (existingSuperAdmin)
                    {
                        return StatusCode(403, "系統中已存在超級管理員，無法將其他用戶提升為超級管理員。");
                    }
                    
                    // 如果升級為超級管理員，強制設置為啟用狀態
                    model.IsActive = true;
                }
                
                // 更新資料
                user.UserName = model.UserName;
                
                // 如果要修改密碼，才更新密碼
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    user.Password = HashUtility.HashPassword(model.Password);
                }
                
                user.RoleId = model.RoleId;
                user.IsActive = model.IsActive;
            }

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // PUT: api/UsersApi/5/status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] bool isActive)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // 獲取當前登入用戶的 ID
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Forbid();
            }

            // 檢查是否為超級管理員
            if (user.RoleId == SUPERADMIN_ROLE_ID)
            {
                // 超級管理員不能被停用
                if (!isActive)
                {
                    return StatusCode(403, "超級管理員不能被停用。");
                }
                
                // 只有超級管理員自己可以修改自己的狀態
                if (currentUserId != id)
                {
                    return StatusCode(403, "無法修改超級管理員的狀態，只有超級管理員本人可以修改自己的狀態。");
                }
            }

            user.IsActive = isActive;
            await _context.SaveChangesAsync();

            return NoContent();
        }
        
        // DELETE: api/UsersApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // 檢查是否為超級管理員
            if (user.RoleId == SUPERADMIN_ROLE_ID)
            {
                // 超級管理員不能被刪除
                return StatusCode(403, "超級管理員帳號不能被刪除。");
            }

            // 獲取當前登入用戶的 ID
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Forbid();
            }

            // 不能刪除自己
            if (id == currentUserId)
            {
                return StatusCode(403, "不能刪除自己的帳號。");
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/UsersApi/ChangePassword
        [HttpPost("ChangePassword")]
        public async Task<ActionResult<PasswordChangeResultViewModel>> ChangePassword([FromBody] PasswordChangeViewModel model)
        {
            // 獲取當前登入用戶的 ID
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Forbid();
            }
            
            // 確認修改的是自己的密碼
            if (currentUserId != model.UserId)
            {
                return StatusCode(403, "只能修改自己的密碼");
            }
            
            // 檢查密碼確認
            if (model.NewPassword != model.ConfirmPassword)
            {
                return BadRequest(new PasswordChangeResultViewModel
                {
                    Success = false,
                    Message = "新密碼與確認密碼不符"
                });
            }

            // 取得用戶資料
            var user = await _context.Users.FindAsync(currentUserId);
            if (user == null)
            {
                return NotFound(new PasswordChangeResultViewModel
                {
                    Success = false,
                    Message = "找不到用戶資料"
                });
            }

            // 驗證目前密碼是否正確
            if (!HashUtility.VerifyPassword(model.CurrentPassword, user.Password))
            {
                return BadRequest(new PasswordChangeResultViewModel
                {
                    Success = false,
                    Message = "目前密碼不正確"
                });
            }

            // 更新密碼
            user.Password = HashUtility.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            // 回傳成功結果，並指示前端需要登出
            return Ok(new PasswordChangeResultViewModel
            {
                Success = true,
                Message = "密碼已成功更新，請重新登入",
                RequireLogout = true
            });
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}