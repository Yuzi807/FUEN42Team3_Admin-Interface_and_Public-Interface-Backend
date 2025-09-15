using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    [Route("api/[controller]")]
    [ApiController]
    public class MembersApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MembersApiController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/MembersApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MembersViewModel>>> GetMembers()
        {
            // 琩高穦戈
            var members = await _context.Members
                .Select(m => new MembersViewModel
                {
                    Id = m.Id,
                    UserName = m.UserName ?? string.Empty,
                    Email = m.Email,
                    RoleName = "穦", // 穦à︹㏕﹚ "穦"
                    IsActive = m.IsActive,
                    CreatedAt = m.CreatedAt,
                    // 眔程祅丁眖祅癘魁い琩高
                    LastLoginAt = _context.MemberLoginLogs
                        .Where(log => log.MemberId == m.Id && log.IsSuccessful == true)
                        .OrderByDescending(log => log.LoginAt)
                        .Select(log => (DateTime?)log.LoginAt)
                        .FirstOrDefault()
                })
                .OrderBy(m => m.Id)
                .ToListAsync();

            return members;
        }

        // GET: api/MembersApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<MembersViewModel>> GetMember(int id)
        {
            var member = await _context.Members
                .Where(m => m.Id == id)
                .Select(m => new MembersViewModel
                {
                    Id = m.Id,
                    UserName = m.UserName ?? string.Empty,
                    Email = m.Email,
                    RoleName = "穦", // 穦à︹㏕﹚ "穦"
                    IsActive = m.IsActive,
                    CreatedAt = m.CreatedAt,
                    // 眔程祅丁眖祅癘魁い琩高
                    LastLoginAt = _context.MemberLoginLogs
                        .Where(log => log.MemberId == m.Id && log.IsSuccessful == true)
                        .OrderByDescending(log => log.LoginAt)
                        .Select(log => (DateTime?)log.LoginAt)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            if (member == null)
            {
                return NotFound();
            }

            return member;
        }

        // 眔穦计秖参璸戈
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetMemberStats()
        {
            var totalMembers = await _context.Members.CountAsync();
            var activeMembers = await _context.Members.Where(m => m.IsActive == true).CountAsync();
            var inactiveMembers = await _context.Members.Where(m => m.IsActive == false).CountAsync();
            var verifiedMembers = await _context.Members.Where(m => m.IsConfirmed == true).CountAsync();
            var unverifiedMembers = await _context.Members.Where(m => m.IsConfirmed == false).CountAsync();

            return new
            {
                TotalMembers = totalMembers,
                ActiveMembers = activeMembers,
                InactiveMembers = inactiveMembers,
                VerifiedMembers = verifiedMembers,
                UnverifiedMembers = unverifiedMembers
            };
        }

        // GET: api/MembersApi/search
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<MembersViewModel>>> SearchMembers(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return await GetMembers();
            }

            var members = await _context.Members
                .Where(m => m.UserName.Contains(keyword) || 
                            m.Email.Contains(keyword))
                .Select(m => new MembersViewModel
                {
                    Id = m.Id,
                    UserName = m.UserName ?? string.Empty,
                    Email = m.Email,
                    RoleName = "穦", // 穦à︹㏕﹚ "穦"
                    IsActive = m.IsActive,
                    CreatedAt = m.CreatedAt,
                    LastLoginAt = _context.MemberLoginLogs
                        .Where(log => log.MemberId == m.Id && log.IsSuccessful == true)
                        .OrderByDescending(log => log.LoginAt)
                        .Select(log => (DateTime?)log.LoginAt)
                        .FirstOrDefault()
                })
                .OrderBy(m => m.Id)
                .ToListAsync();

            return members;
        }
    }
}