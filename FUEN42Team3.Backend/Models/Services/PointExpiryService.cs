using FUEN42Team3.Backend.Models.EfModels;
using Microsoft.EntityFrameworkCore;

namespace FUEN42Team3.Backend.Models.Services
{
    public class PointExpiryService
    {
        private readonly AppDbContext _db;
        public PointExpiryService(AppDbContext db) { _db = db; }

        public async Task<int> CleanupExpiredAsync()
        {
            var now = DateTime.UtcNow;
            var lots = await _db.PointLots.Where(l => l.RemainingPoints > 0 && l.ExpiresAt <= now).ToListAsync();
            foreach (var lot in lots)
            {
                lot.RemainingPoints = 0;
            }
            await _db.SaveChangesAsync();
            return lots.Count;
        }
    }
}
