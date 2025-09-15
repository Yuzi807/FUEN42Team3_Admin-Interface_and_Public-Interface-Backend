using System;
using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [ApiController]
    [Route("api/member/cvs-addresses")]
    [Authorize]
    public class MemberCvsAddressesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public MemberCvsAddressesController(AppDbContext db) => _db = db;

        private int? GetCurrentMemberId()
        {
            var val = User.FindFirst("MemberId")?.Value;
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? subtype)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();

            var q = _db.MemberCvsAddresses.AsQueryable().Where(a => a.MemberId == mid);
            if (!string.IsNullOrWhiteSpace(subtype)) q = q.Where(a => a.LogisticsSubType == subtype);

            var list = await q.OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.UpdatedAt)
                .Select(a => new MemberCvsAddressDto
                {
                    Id = a.Id,
                    LogisticsSubType = a.LogisticsSubType,
                    StoreId = a.StoreId,
                    StoreName = a.StoreName,
                    Address = a.Address,
                    Telephone = a.Telephone,
                    RecipientName = a.RecipientName,
                    RecipientPhone = a.RecipientPhone,
                    IsDefault = a.IsDefault,
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
            var a = await _db.MemberCvsAddresses.FirstOrDefaultAsync(x => x.Id == id && x.MemberId == mid);
            if (a == null) return NotFound();
            var dto = new MemberCvsAddressDto
            {
                Id = a.Id,
                LogisticsSubType = a.LogisticsSubType,
                StoreId = a.StoreId,
                StoreName = a.StoreName,
                Address = a.Address,
                Telephone = a.Telephone,
                RecipientName = a.RecipientName,
                RecipientPhone = a.RecipientPhone,
                IsDefault = a.IsDefault,
                UpdatedAt = a.UpdatedAt
            };
            return Ok(new { success = true, data = dto });
        }

        [HttpPost]
        public async Task<IActionResult> Create(MemberCvsAddressCreateDto req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var now = DateTime.Now;
            // Upsert：同會員、同品牌、同門市若已存在則更新內容，否則新增
            var exist = await _db.MemberCvsAddresses
                .FirstOrDefaultAsync(a => a.MemberId == mid && a.LogisticsSubType == req.LogisticsSubType && a.StoreId == req.StoreId);
            if (exist != null)
            {
                exist.StoreName = req.StoreName;
                exist.Address = req.Address;
                exist.Telephone = req.Telephone;
                exist.RecipientName = req.RecipientName;
                exist.RecipientPhone = req.RecipientPhone;
                exist.IsDefault = req.IsDefault;
                exist.UpdatedAt = now;
                if (req.IsDefault)
                {
                    await _db.MemberCvsAddresses
                        .Where(a => a.MemberId == mid && a.Id != exist.Id && a.LogisticsSubType == exist.LogisticsSubType && a.IsDefault)
                        .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, _ => false));
                }
                await _db.SaveChangesAsync();
                return Ok(new { success = true, id = exist.Id, upsert = "updated" });
            }
            else
            {
                var row = new MemberCvsAddress
                {
                    MemberId = mid.Value,
                    LogisticsSubType = req.LogisticsSubType,
                    StoreId = req.StoreId,
                    StoreName = req.StoreName,
                    Address = req.Address,
                    Telephone = req.Telephone,
                    RecipientName = req.RecipientName,
                    RecipientPhone = req.RecipientPhone,
                    IsDefault = req.IsDefault,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.MemberCvsAddresses.Add(row);
                if (req.IsDefault)
                {
                    await _db.MemberCvsAddresses
                        .Where(a => a.MemberId == mid && a.Id != row.Id && a.LogisticsSubType == row.LogisticsSubType && a.IsDefault)
                        .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, _ => false));
                }
                await _db.SaveChangesAsync();
                return Ok(new { success = true, id = row.Id, upsert = "created" });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, MemberCvsAddressUpdateDto req)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var row = await _db.MemberCvsAddresses.FirstOrDefaultAsync(x => x.Id == id && x.MemberId == mid);
            if (row == null) return NotFound();

            row.StoreName = req.StoreName;
            row.Address = req.Address;
            row.Telephone = req.Telephone;
            row.RecipientName = req.RecipientName;
            row.RecipientPhone = req.RecipientPhone;
            row.IsDefault = req.IsDefault;
            row.UpdatedAt = DateTime.Now;

            if (req.IsDefault)
            {
                await _db.MemberCvsAddresses
                    .Where(a => a.MemberId == mid && a.Id != row.Id && a.LogisticsSubType == row.LogisticsSubType && a.IsDefault)
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
            var row = await _db.MemberCvsAddresses.FirstOrDefaultAsync(x => x.Id == id && x.MemberId == mid);
            if (row == null) return NotFound();

            _db.MemberCvsAddresses.Remove(row);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost("{id:int}/default")]
        public async Task<IActionResult> SetDefault(int id)
        {
            var mid = GetCurrentMemberId();
            if (mid == null) return Unauthorized();
            var row = await _db.MemberCvsAddresses.FirstOrDefaultAsync(x => x.Id == id && x.MemberId == mid);
            if (row == null) return NotFound();

            await _db.MemberCvsAddresses
                .Where(a => a.MemberId == mid && a.LogisticsSubType == row.LogisticsSubType)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, _ => false));

            row.IsDefault = true;
            row.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
