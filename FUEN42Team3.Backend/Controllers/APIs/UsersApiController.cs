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
        private const int SUPERADMIN_ROLE_ID = 1; // �W�ź޲z���� RoleId

        public UsersApiController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/UsersApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UsersViewModel>>> GetUsers()
        {
            // �d�߸�Ʈw�����ϥΪ̡A�����p������
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
                    RoleId = u.RoleId // �K�[ RoleId �H��K�e�ݧP�_
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
                    RoleId = u.RoleId // �K�[ RoleId �H��K�e�ݧP�_
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

            // �ˬd�b���O�_�w�s�b
            if (await _context.Users.AnyAsync(u => u.Account == model.Account))
            {
                return BadRequest("�b���w�s�b�A�ШϥΨ�L�b��");
            }

            // �ˬd����O�_�s�b
            var role = await _context.UserRoles.FindAsync(model.RoleId);
            if (role == null)
            {
                return BadRequest("��ܪ����⤣�s�b");
            }

            // �ˬd�O�_���իإ߶W�ź޲z��
            if (model.RoleId == SUPERADMIN_ROLE_ID)
            {
                // �ˬd�t�Τ��O�_�w�g�s�b�W�ź޲z��
                var existingSuperAdmin = await _context.Users
                    .AnyAsync(u => u.RoleId == SUPERADMIN_ROLE_ID);
                
                if (existingSuperAdmin)
                {
                    return StatusCode(403, "�t�Τ��w�s�b�W�ź޲z���A���i�إߦh�ӶW�ź޲z���C");
                }
                
                // �p�G���ճЫضW�ź޲z���A�j��]�m���ҥΪ��A
                model.IsActive = true;
            }

            // �إ߷s���ϥΪ�
            var user = new User
            {
                UserName = model.UserName,
                Account = model.Account,
                Password = HashUtility.HashPassword(model.Password), // �ϥ�����K�X
                RoleId = model.RoleId,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // �^�ǫإߦ��\���ϥΪ̸��
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

            // �����e�n�J�Τ᪺ ID
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Forbid();
            }

            // �ˬd�O�_���W�ź޲z��
            bool isSuperAdmin = (user.RoleId == SUPERADMIN_ROLE_ID);
            bool isSelfOperation = (currentUserId == id);
            
            // �p�G�O�W�ź޲z���B���O�ۤv�b�ާ@
            if (isSuperAdmin && !isSelfOperation)
            {
                return StatusCode(403, "�L�k�ק�W�ź޲z������ơA�u���W�ź޲z�����H�i�H�ק�ۤv����ơC");
            }

            // �p�G�O�W�ź޲z���A�S��B�z
            if (isSuperAdmin)
            {
                // �T��W�ź޲z�����Ŧۤv������
                if (model.RoleId != SUPERADMIN_ROLE_ID)
                {
                    // �ˬd�t�Τ��O�_�٦���L�W�ź޲z��
                    var superAdminCount = await _context.Users.CountAsync(u => u.RoleId == SUPERADMIN_ROLE_ID);
                    if (superAdminCount <= 1)
                    {
                        return StatusCode(403, "�t�Υ����O�d�ܤ֤@�W�W�ź޲z���A�L�k���Űߤ@���W�ź޲z���C");
                    }
                }
                
                // �T��W�ź޲z�����Φۤv
                if (!model.IsActive)
                {
                    return StatusCode(403, "�W�ź޲z������Q���ΡC");
                }
                
                // �W�ź޲z���u��ק�ۤv���Τ�W�M�K�X
                user.UserName = model.UserName;
                
                // �p�G���ѤF�s�K�X�A�~��s�K�X
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    user.Password = HashUtility.HashPassword(model.Password);
                }
                
                // ��L�ݩʫO������
                // ����s RoleId �M IsActive
            }
            else
            {
                // �D�W�ź޲z�������`��s�y�{
                
                // �p�G�n�N�Τᨤ��אּ�W�ź޲z���A�ݭn�S��B�z
                if (model.RoleId == SUPERADMIN_ROLE_ID && user.RoleId != SUPERADMIN_ROLE_ID)
                {
                    // �ˬd�t�Τ��O�_�w�g�s�b�W�ź޲z��
                    var existingSuperAdmin = await _context.Users
                        .AnyAsync(u => u.RoleId == SUPERADMIN_ROLE_ID);
                    
                    if (existingSuperAdmin)
                    {
                        return StatusCode(403, "�t�Τ��w�s�b�W�ź޲z���A�L�k�N��L�Τᴣ�ɬ��W�ź޲z���C");
                    }
                    
                    // �p�G�ɯŬ��W�ź޲z���A�j��]�m���ҥΪ��A
                    model.IsActive = true;
                }
                
                // ��s���
                user.UserName = model.UserName;
                
                // �p�G�n�ק�K�X�A�~��s�K�X
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

            // �����e�n�J�Τ᪺ ID
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Forbid();
            }

            // �ˬd�O�_���W�ź޲z��
            if (user.RoleId == SUPERADMIN_ROLE_ID)
            {
                // �W�ź޲z������Q����
                if (!isActive)
                {
                    return StatusCode(403, "�W�ź޲z������Q���ΡC");
                }
                
                // �u���W�ź޲z���ۤv�i�H�ק�ۤv�����A
                if (currentUserId != id)
                {
                    return StatusCode(403, "�L�k�ק�W�ź޲z�������A�A�u���W�ź޲z�����H�i�H�ק�ۤv�����A�C");
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

            // �ˬd�O�_���W�ź޲z��
            if (user.RoleId == SUPERADMIN_ROLE_ID)
            {
                // �W�ź޲z������Q�R��
                return StatusCode(403, "�W�ź޲z���b������Q�R���C");
            }

            // �����e�n�J�Τ᪺ ID
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Forbid();
            }

            // ����R���ۤv
            if (id == currentUserId)
            {
                return StatusCode(403, "����R���ۤv���b���C");
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/UsersApi/ChangePassword
        [HttpPost("ChangePassword")]
        public async Task<ActionResult<PasswordChangeResultViewModel>> ChangePassword([FromBody] PasswordChangeViewModel model)
        {
            // �����e�n�J�Τ᪺ ID
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Forbid();
            }
            
            // �T�{�ק諸�O�ۤv���K�X
            if (currentUserId != model.UserId)
            {
                return StatusCode(403, "�u��ק�ۤv���K�X");
            }
            
            // �ˬd�K�X�T�{
            if (model.NewPassword != model.ConfirmPassword)
            {
                return BadRequest(new PasswordChangeResultViewModel
                {
                    Success = false,
                    Message = "�s�K�X�P�T�{�K�X����"
                });
            }

            // ���o�Τ���
            var user = await _context.Users.FindAsync(currentUserId);
            if (user == null)
            {
                return NotFound(new PasswordChangeResultViewModel
                {
                    Success = false,
                    Message = "�䤣��Τ���"
                });
            }

            // ���ҥثe�K�X�O�_���T
            if (!HashUtility.VerifyPassword(model.CurrentPassword, user.Password))
            {
                return BadRequest(new PasswordChangeResultViewModel
                {
                    Success = false,
                    Message = "�ثe�K�X�����T"
                });
            }

            // ��s�K�X
            user.Password = HashUtility.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            // �^�Ǧ��\���G�A�ë��ܫe�ݻݭn�n�X
            return Ok(new PasswordChangeResultViewModel
            {
                Success = true,
                Message = "�K�X�w���\��s�A�Э��s�n�J",
                RequireLogout = true
            });
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}