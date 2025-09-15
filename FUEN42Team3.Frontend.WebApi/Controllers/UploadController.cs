using Microsoft.AspNetCore.Mvc;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _hostEnvironment;

        // 透過建構子注入 IWebHostEnvironment
        public UploadController(IWebHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        /// <summary>
        /// 上傳封面圖片 (文章封面)
        /// 會被存在 DB 中，存檔名或完整 URL
        /// </summary>
        [HttpPost("cover")]
        public async Task<IActionResult> UploadCover(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            // 確保 uploads/covers 存在
            var uploads = Path.Combine(_hostEnvironment.WebRootPath ?? "wwwroot", "uploads", "covers");
            if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

            // 產生唯一檔名
            var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploads, fileName);

            // 寫入檔案
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 給前端的公開 URL
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var fileUrl = $"{baseUrl}/uploads/covers/{fileName}";
            return Ok(new { url = fileUrl, fileName });
        }


        /// <summary>
        /// 上傳文章內容圖片 (Summernote 用)
        /// 只會存在檔案系統，不進 DB
        /// </summary>
        [HttpPost("content-image")]
        public async Task<IActionResult> UploadContentImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            var uploads = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "post-images");
            if (!Directory.Exists(uploads))
                Directory.CreateDirectory(uploads);

            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploads, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 👇 這裡改成完整 URL
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var url = $"{baseUrl}/uploads/post-images/{fileName}";

            return Ok(new { url });
        }


    }
}
