using FUEN42Team3.Backend.Models.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FUEN42Team3.Backend.Controllers.APIs
{
    // URL: /api/locations
    [Route("api/[controller]")]
    [ApiController]
    public class LocationsApiController : ControllerBase
    {
        private LocationRepository _locationRepository;

        public LocationsApiController()
        {
            _locationRepository = new LocationRepository();
        }

        /// <summary>
        /// 根據 cityId 回傳鄉鎮市區清單
        /// </summary>
        /// <param name="cityId"></param>
        /// <returns></returns>
        // URL: /api/locations/{cityId}/Districts
        [Route("{cityId}/Districts")]
        [HttpGet]
        public IActionResult GetDistricts(int cityId)
        {
            var districts = _locationRepository.GetDistrictsByCityId(cityId);

            return Ok(districts);
        }
    }
}
