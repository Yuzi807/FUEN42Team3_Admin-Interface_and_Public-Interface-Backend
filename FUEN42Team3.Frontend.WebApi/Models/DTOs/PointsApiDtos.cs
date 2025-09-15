using System.ComponentModel.DataAnnotations;

namespace FUEN42Team3.Frontend.WebApi.Models.DTOs
{
    public class PointRedeemRequest
    {
        [Range(1, int.MaxValue)]
        public int UsePoints { get; set; }
    }

    public class PointRedeemItemDto
    {
        public int LotId { get; set; }
        public int UsedPoints { get; set; }
    }

    public class PointRedeemResponse
    {
        public int UsedPoints { get; set; }
        public int Balance { get; set; }
        public List<PointRedeemItemDto> Items { get; set; } = new();
    }

    public class ExpiringLotDto
    {
        public int LotId { get; set; }
        public int RemainingPoints { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? Reason { get; set; }
    }
}
