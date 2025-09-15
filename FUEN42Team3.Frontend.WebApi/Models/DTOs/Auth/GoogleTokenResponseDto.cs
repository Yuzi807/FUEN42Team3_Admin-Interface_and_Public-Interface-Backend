using Newtonsoft.Json;

namespace FUEN42Team3.Frontend.WebApi.Models.DTOs.Auth
{
    public class GoogleTokenResponseDto
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("id_token")]
        public string IdToken { get; set; }


    }
}
