namespace FUEN42Team3.Backend.Models.Entities
{
    /// <summary>
	/// 鄉鎮市區
	/// </summary>
    public class District
    {
        public int Id { get; set; }
        public string Name { get; set; } // e.g., 大安區、竹北市、員林市
        public int CityId { get; set; }
    }

    /// <summary>
    /// 城市
    /// </summary>
    public class City
    {
        public int Id { get; set; }
        public string Name { get; set; } // e.g., 台北市、嘉義縣
        public List<District> Districts { get; set; } = new List<District>();
    }
}
