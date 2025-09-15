using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [ApiController]
    [Route("api/member/addresses")]
    [Authorize]
    public class MemberAddressesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public MemberAddressesController(AppDbContext db) => _db = db;

        private int? GetCurrentMemberId()
        {
            var val = User.FindFirst("MemberId")?.Value;
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();

            var list = await _db.MemberAddresses
                .Where(a => a.MemberId == mid)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.UpdatedAt)
                .Select(a => new MemberAddressDto
                {
                    Id = a.Id,
                    RecipientName = a.RecipientName ?? string.Empty,
                    RecipientPhone = a.RecipientPhone ?? string.Empty,
                    PostalCode = a.PostalCode ?? string.Empty,
                    City = a.City ?? string.Empty,
                    District = a.District ?? string.Empty,
                    Street = a.Street ?? string.Empty,
                    Label = a.Label,
                    IsDefault = a.IsDefault ?? false,
                    UpdatedAt = a.UpdatedAt
                })
                .ToListAsync();

            return Ok(new { success = true, data = list });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOne(int id)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();
            var a = await _db.MemberAddresses.FirstOrDefaultAsync(x => x.Id == id && x.MemberId == mid);
            if (a == null) return NotFound();
            var dto = new MemberAddressDto
            {
                Id = a.Id,
                RecipientName = a.RecipientName ?? string.Empty,
                RecipientPhone = a.RecipientPhone ?? string.Empty,
                PostalCode = a.PostalCode ?? string.Empty,
                City = a.City ?? string.Empty,
                District = a.District ?? string.Empty,
                Street = a.Street ?? string.Empty,
                Label = a.Label,
                IsDefault = a.IsDefault ?? false,
                UpdatedAt = a.UpdatedAt
            };
            return Ok(new { success = true, data = dto });
        }

        [HttpPost]
        public async Task<IActionResult> Create(MemberAddressCreateDto req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var now = DateTime.Now;
            var row = new MemberAddress
            {
                MemberId = mid,
                RecipientName = req.RecipientName,
                RecipientPhone = req.RecipientPhone,
                PostalCode = req.PostalCode,
                City = req.City,
                District = req.District,
                Street = req.Street,
                Label = req.Label,
                IsDefault = req.IsDefault,
                UpdatedAt = now
            };
            _db.MemberAddresses.Add(row);

            if (req.IsDefault)
            {
                await _db.MemberAddresses
                    .Where(a => a.MemberId == mid && a.Id != row.Id && a.IsDefault == true)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, _ => false));
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, id = row.Id });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, MemberAddressUpdateDto req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var row = await _db.MemberAddresses.FirstOrDefaultAsync(x => x.Id == id && x.MemberId == mid);
            if (row == null) return NotFound();

            row.RecipientName = req.RecipientName;
            row.RecipientPhone = req.RecipientPhone;
            row.PostalCode = req.PostalCode;
            row.City = req.City;
            row.District = req.District;
            row.Street = req.Street;
            row.Label = req.Label;
            row.IsDefault = req.IsDefault;
            row.UpdatedAt = DateTime.Now;

            if (req.IsDefault)
            {
                await _db.MemberAddresses
                    .Where(a => a.MemberId == mid && a.Id != row.Id && a.IsDefault == true)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, _ => false));
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();
            var row = await _db.MemberAddresses.FirstOrDefaultAsync(x => x.Id == id && x.MemberId == mid);
            if (row == null) return NotFound();

            _db.MemberAddresses.Remove(row);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost("{id:int}/default")]
        public async Task<IActionResult> SetDefault(int id)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();
            var row = await _db.MemberAddresses.FirstOrDefaultAsync(x => x.Id == id && x.MemberId == mid);
            if (row == null) return NotFound();

            await _db.MemberAddresses
                .Where(a => a.MemberId == mid)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, _ => false));

            row.IsDefault = true;
            row.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
