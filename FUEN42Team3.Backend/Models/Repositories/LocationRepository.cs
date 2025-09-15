using FUEN42Team3.Backend.Models.Entities;

namespace FUEN42Team3.Backend.Models.Repositories
{
    /// <summary>
    /// 維護城市/鄉鎮市區 data
    /// </summary>
    public class LocationRepository
    {
        private static List<City> _cities;
        static LocationRepository()
        {
            // 模擬資料庫查詢
            var districts = new List<District>
            {
                new District { Id = 1, Name = "大安區", CityId = 1 },
                new District { Id = 2, Name = "中正區", CityId = 1 },
                new District { Id = 3, Name = "信義區", CityId = 1 },
                new District { Id = 4, Name = "竹北市", CityId = 2 },
                new District { Id = 4, Name = "寶山鄉", CityId = 2 },
                new District { Id = 5, Name = "員林市", CityId = 3 },
                new District { Id = 5, Name = "秀水鄉", CityId = 3 }
            };

            _cities = new List<City>
                {
                    new City { Id = 1, Name = "台北市" },
                    new City { Id = 2, Name = "新竹縣" },
                    new City { Id = 3, Name = "彰化縣" }
                };

            _cities.ForEach(city =>
            {
                city.Districts = districts.Where(d => d.CityId == city.Id).ToList();
            });
        }

        public List<District> GetDistrictsByCityId(int cityId)
        {
            return _cities
                .FirstOrDefault(c => c.Id == cityId)?
                .Districts ?? new List<District>();
        }

        public List<City> GetCities()
        {
            return _cities;
        }
    }
}

