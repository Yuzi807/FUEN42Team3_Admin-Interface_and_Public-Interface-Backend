using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Frontend.WebApi.Models.DTOs;
using FUEN42Team3.Frontend.WebApi.Models.DTOs.Auth;
using FUEN42Team3.Frontend.WebApi.Models.Services;
using FUEN42Team3.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MembersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailSender _emailSender;
        private readonly FUEN42Team3.Frontend.WebApi.Models.Services.PointsEventsClient _pointsEvents;

        public MembersController(AppDbContext context, IConfiguration configuration, EmailSender emailSender, FUEN42Team3.Frontend.WebApi.Models.Services.PointsEventsClient pointsEvents)
        {
            _context = context;
            _configuration = configuration;
            _emailSender = emailSender;
            _pointsEvents = pointsEvents;
        }
        [HttpPost]
        [Route("Registration")] // 註冊
        public async Task<IActionResult> Registration([FromBody] RegisterDto Dto)
        {
            // 實作註冊邏輯
            // 例如：檢查使用者名稱是否已存在、email是否已被使用、密碼是否一致
            try
            {
                // 暱稱驗證
                if (string.IsNullOrWhiteSpace(Dto.Username))
                    return BadRequest("請輸入使用者暱稱");

                if (Dto.Username.Length < 2)
                    return BadRequest("暱稱至少需要2個字元");

                if (Dto.Username.Length > 25)
                    return BadRequest("暱稱不能超過25個字元");

                // Email 驗證
                if (string.IsNullOrWhiteSpace(Dto.Email))
                    return BadRequest("請輸入電子郵件");

                string emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
                if (!Regex.IsMatch(Dto.Email, emailPattern))
                    return BadRequest("請輸入有效的電子郵件格式");

                if (Dto.Email.Length > 100)
                    return BadRequest("電子郵件不能超過100個字元");

                // 密碼驗證
                if (string.IsNullOrWhiteSpace(Dto.Password))
                    return BadRequest("請輸入密碼");

                if (Dto.Password.Length < 8)
                    return BadRequest("密碼至少需要8個字元");

                if (Dto.Password.Length > 30)
                    return BadRequest("密碼不能超過30個字元");

                string passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$";
                if (!Regex.IsMatch(Dto.Password, passwordPattern))
                    return BadRequest("密碼必須包含至少一個小寫字母、一個大寫字母和一個數字");

                // 確認密碼驗證
                if (string.IsNullOrWhiteSpace(Dto.ConfirmPassword))
                    return BadRequest("請確認密碼");

                if (Dto.Password != Dto.ConfirmPassword)
                    return BadRequest("密碼不一致");

                var existingMember = _context.Members.FirstOrDefault(m => m.UserName == Dto.Username);
                if (existingMember != null)
                    return BadRequest("該暱稱已經有人使用過囉");

                var existingMemberByEmail = _context.Members.FirstOrDefault(m => m.Email == Dto.Email);
                if (existingMemberByEmail != null)
                    return BadRequest("該Email已註冊過，請直接登入");

                // 使用 HashUtility 雜湊密碼
                var hashedPassword = HashUtility.HashPassword(Dto.Password);

                // 建立會員資料,已啟用但未驗證通過
                var newMember = new Member
                {
                    UserName = Dto.Username,
                    Email = Dto.Email,
                    Password = hashedPassword, // 儲存雜湊後的密碼
                    IsActive = true,
                    IsConfirmed = false,
                    LoginProvider = "Local"
                };

                await _context.Members.AddAsync(newMember);
                await _context.SaveChangesAsync();

                // 建立對應的個人檔案
                var profile = new MemberProfile
                {
                    MemberId = newMember.Id
                };

                await _context.MemberProfiles.AddAsync(profile);
                await _context.SaveChangesAsync();

                // 生成驗證Token
                var code = Guid.NewGuid().ToString();
                // 儲存驗證碼至資料表
                var verification = new MemberVerification
                {
                    MemberId = newMember.Id,
                    Type = "EmailConfirm",
                    Code = code,
                    IsUsed = false,
                    ExpiredAt = DateTime.UtcNow.AddHours(24),
                    CreatedAt = DateTime.UtcNow
                };
                await _context.MemberVerifications.AddAsync(verification);
                await _context.SaveChangesAsync();

                // 建立驗證連結
                var confirmationUrl = $"http://localhost:5173/confirm-email?code={code}";

                // 呼叫寄信方法
                await _emailSender.SendConfirmRegisterEmail(confirmationUrl, Dto.Username, Dto.Email);

                return Ok(new { message = "註冊成功！請至信箱點擊驗證連結以開通帳號。" });

            }
            catch (Exception ex)
            {
                // 處理例外情況
                Console.WriteLine($"註冊失敗: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "註冊過程發生錯誤，請稍後再試", error = ex.Message });
            }
        }

        [HttpGet]
        [Route("ConfirmEmail")] // 驗證Email
        public async Task<IActionResult> ConfirmEmail(string code)
        {
            var verification = await _context.MemberVerifications
                .Include(v => v.Member)
                .FirstOrDefaultAsync(v => v.Code == code && v.Type == "EmailConfirm");

            if (verification == null || verification.IsUsed || verification.ExpiredAt < DateTime.UtcNow)
                return BadRequest("驗證連結無效或已過期");

            // 驗證成功後，建議清除 code 或刪除整筆驗證紀錄
            verification.IsUsed = true;
            verification.Code = null;
            verification.Member.IsConfirmed = true;

            await _context.SaveChangesAsync();

            // 發送點數規則事件：完成註冊驗證
            try
            {
                if (verification.Member != null)
                {
                    _ = _pointsEvents.SendRegistrationCompletedAsync(verification.Member.Id);
                }
            }
            catch { /* swallow to avoid blocking the flow */ }

            return Ok("Email驗證成功，您現在可以登入囉！");
        }

        [HttpPost]
        [Route("ResendVerification")] //重寄驗證碼
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Email))
                    return BadRequest("請提供電子郵件地址");

                // 檢查是否是有效的郵箱格式
                string emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
                if (!Regex.IsMatch(dto.Email, emailPattern))
                    return BadRequest("請輸入有效的電子郵件格式");

                // 不區分大小寫查詢會員
                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.Email.ToLower() == dto.Email.ToLower());

                if (member == null)
                    return NotFound("此 Email 尚未註冊");

                if (member.IsConfirmed)
                    return BadRequest("此帳號已完成驗證，無需重寄");

                // 檢查是否已有未過期的驗證碼
                var existingCode = await _context.MemberVerifications
                    .Where(v => v.MemberId == member.Id && v.Type == "EmailConfirm" && !v.IsUsed && v.ExpiredAt > DateTime.UtcNow)
                    .OrderByDescending(v => v.CreatedAt)
                    .FirstOrDefaultAsync();

                string code;

                if (existingCode != null)
                {
                    code = existingCode.Code;
                }
                else
                {
                    code = Guid.NewGuid().ToString();
                    var newVerification = new MemberVerification
                    {
                        MemberId = member.Id,
                        Type = "EmailConfirm",
                        Code = code,
                        IsUsed = false,
                        ExpiredAt = DateTime.UtcNow.AddHours(24),
                        CreatedAt = DateTime.UtcNow
                    };
                    await _context.MemberVerifications.AddAsync(newVerification);
                    await _context.SaveChangesAsync();
                }

                var confirmationUrl = $"http://localhost:5173/confirm-email?code={code}";

                await _emailSender.SendConfirmRegisterEmail(confirmationUrl, member.UserName, member.Email);

                return Ok(new { message = "驗證信已重新寄出，請至信箱點擊連結完成驗證" });
            }
            catch (Exception ex)
            {
                // 記錄詳細的錯誤訊息
                Console.WriteLine($"重發驗證信失敗: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "發送驗證郵件時發生錯誤，請稍後再試", error = ex.Message });
            }
        }

        [HttpPost]
        [Route("login")] // 登入
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
                {
                    return BadRequest(new { message = "Email 與密碼不可為空" });
                }

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.Email == loginDto.Email);

                if (member == null)
                {
                    return Unauthorized(new { message = "帳號或密碼錯誤" });
                }

                bool isPasswordValid = HashUtility.VerifyPassword(loginDto.Password, member.Password);
                if (!isPasswordValid)
                {
                    return Unauthorized(new { message = "帳號或密碼錯誤" });
                }

                if (!member.IsConfirmed)
                {
                    return Unauthorized(new
                    {
                        message = "此帳號尚未完成電子郵件驗證",
                        requireVerification = true,
                        email = member.Email
                    });
                }

                var claims = new[]
                {
            new Claim(JwtRegisteredClaimNames.Sub, _configuration["Jwt:Subject"] ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("MemberId", member.Id.ToString()),
            new Claim("Email", member.Email),
            new Claim("UserName", member.UserName)
        };
                // 使用對稱金鑰簽署 JWT
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? string.Empty));
                var signIn = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var token = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(60),
                    signingCredentials: signIn
                );
                // 生成 JWT 字串
                string tokenValue = new JwtSecurityTokenHandler().WriteToken(token);

                return Ok(new
                {
                    token = tokenValue,
                    memberId = member.Id,
                    username = member.UserName,
                    email = member.Email,
                    loginProvider = member.LoginProvider 
                });
            }
            catch (Exception ex)
            {
                // 可記錄 log 或回傳錯誤訊息
                Console.WriteLine($"登入失敗：{ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "登入過程發生錯誤，請稍後再試",
                    error = ex.Message
                });
            }
        }

        [HttpGet]
        [Route("GetMembers")]
        public IActionResult GetMember()
        {
            // 實作取得所有會員資料邏輯
            var members = _context.Members.ToList();
            if (members == null || !members.Any())
            {
                return NotFound();
            }
            return Ok(members);
        }

        [Authorize]
        [HttpGet]
        [Route("GetMember")]
        public IActionResult GetMember(int id)
        {
            // 實作取得會員資料邏輯
            var member = _context.Members.Find(id);
            if (member == null)
            {
                return NotFound();
            }
            return Ok(member);
        }

        [HttpPost]
        [Route("ForgotPassword")] // 使用者輸入Email => 寄送驗證碼
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            try
            {
                var member = _context.Members.FirstOrDefault(m => m.Email == dto.Email);

                if (member == null)
                    return BadRequest("查無此 Email，請確認是否輸入正確");

                if (member.LoginProvider == "Google")
                    return BadRequest("Google 帳戶無法使用忘記密碼功能");

                if (!member.IsActive) // 處理已停用帳號
                    return BadRequest("此帳號已被停用，無法重設密碼。請聯絡客服人員");

                if (!member.IsConfirmed)
                    return BadRequest("此帳號尚未完成驗證，請先完成註冊驗證流程");

                // 產生 6 碼驗證碼
                var code = new Random().Next(100000, 999999).ToString();
                // 寫入 Verifications 表
                var newVerification = new MemberVerification
                {
                    MemberId = member.Id,
                    Type = "ForgotPassword",
                    Code = code,
                    IsUsed = false,
                    ExpiredAt = DateTime.UtcNow.AddMinutes(15),
                    CreatedAt = DateTime.UtcNow
                };
                await _context.MemberVerifications.AddAsync(newVerification);
                await _context.SaveChangesAsync();

                // 寄送 Email
                await _emailSender.SendForgotPasswordEmail(code, member.UserName, member.Email);

                return Ok("驗證碼已寄送至您的信箱，請於15分鐘內完成驗證");
            }
            catch (Exception ex)
            {
                // 處理例外情況
                Console.WriteLine($"重設密碼失敗: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "重設密碼過程發生錯誤，請稍後再試", error = ex.Message });
            }
        }

        [HttpPost]
        [Route("VerifyResetCode")] // 使用者輸入驗證碼 => 驗證成功 => 產生一次性Token
        public async Task<IActionResult> VerifyResetCode([FromBody] VerifyCodeDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Code))
                {
                    return BadRequest("Email 與驗證碼不可為空");
                }

                var member = await _context.Members
                    .FirstOrDefaultAsync(m => m.Email == dto.Email);

                if (member == null)
                {
                    return BadRequest("查無此帳號");
                }

                var verification = await _context.MemberVerifications
                .Where(v => v.MemberId == member.Id && v.Type == "ForgotPassword" && !v.IsUsed)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

                if (verification == null)
                {
                    return BadRequest("尚未申請驗證碼或驗證碼已使用");
                }

                if (verification.Code != dto.Code)
                {
                    return BadRequest("驗證碼錯誤");
                }

                if (verification.ExpiredAt < DateTime.UtcNow)
                {
                    return BadRequest("驗證碼已過期，請重新申請");
                }
                // 驗證成功標記為已使用，且清除Code
                verification.IsUsed = true;
                verification.Code = null;
                await _context.SaveChangesAsync();
                // jwt token
                var claims = new[]
                {
                new Claim("MemberId", member.Id.ToString()),
                new Claim("Email", member.Email),
                new Claim("Purpose", "ResetPassword")
                };
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? string.Empty));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(15), // Token 有效期
                    signingCredentials: creds
                );

                string tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
                return Ok(new { token = tokenValue });
            }
            catch (Exception ex)
            {
                // 可記錄 log 或回傳錯誤訊息
                Console.WriteLine($"重設密碼失敗: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "重設密碼過程發生錯誤，請稍後再試", error = ex.Message });
            }
        }

        [HttpPost]
        [Route("ResetPassword")] // 重設密碼
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            try
            {
                // 確認密碼驗證
                if (dto.NewPassword != dto.ConfirmPassword)
                    return BadRequest("新密碼與確認密碼不一致");

                // 密碼長度驗證
                if (string.IsNullOrWhiteSpace(dto.NewPassword))
                    return BadRequest("請輸入密碼");

                if (dto.NewPassword.Length < 8)
                    return BadRequest("密碼至少需要8個字元");

                if (dto.NewPassword.Length > 30)
                    return BadRequest("密碼不能超過30個字元");

                // 密碼格式驗證
                string passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$";
                if (!Regex.IsMatch(dto.NewPassword, passwordPattern))
                    return BadRequest("密碼必須包含至少一個小寫字母、一個大寫字母和一個數字");

                // 驗證 JWT Token
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? string.Empty);
                tokenHandler.ValidateToken(dto.Token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuerSigningKey = true
                }, out SecurityToken validatedToken);

                // 取得 MemberId
                var jwtToken = (JwtSecurityToken)validatedToken;
                var memberId = int.Parse(jwtToken.Claims.First(x => x.Type == "MemberId").Value);

                var member = await _context.Members.FindAsync(memberId);
                if (member == null || !member.IsActive)
                    return BadRequest("帳號無效或已停用");

                member.Password = HashUtility.HashPassword(dto.NewPassword);
                await _context.SaveChangesAsync();

                return Ok(new { message = "密碼已成功重設，請重新登入" });
            }
            catch (SecurityTokenExpiredException)
            {
                return BadRequest("Token 已過期，請重新申請驗證碼");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "重設密碼失敗", error = ex.Message });
            }
        }


        [Authorize]
        [HttpPost]
        [Route("ChangePassword")] //會員中心內修改密碼
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            try
            {
                // 基本欄位驗證
                if (string.IsNullOrWhiteSpace(dto.OldPassword) ||
                    string.IsNullOrWhiteSpace(dto.NewPassword) ||
                    string.IsNullOrWhiteSpace(dto.ConfirmPassword))
                {
                    return BadRequest("請完整填寫所有欄位");
                }
                // 新密碼與確認密碼一致性驗證
                if (dto.NewPassword != dto.ConfirmPassword)
                {
                    return BadRequest("新密碼與確認密碼不一致");
                }

                // 新密碼的長度驗證
                if (dto.NewPassword.Length < 8)
                    return BadRequest("密碼至少需要8個字元");

                if (dto.NewPassword.Length > 30)
                    return BadRequest("密碼不能超過30個字元");

                // 新密碼格式驗證 (必須包含至少一個小寫字母、一個大寫字母和一個數字)
                string passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$";
                if (!Regex.IsMatch(dto.NewPassword, passwordPattern))
                    return BadRequest("密碼必須包含至少一個小寫字母、一個大寫字母和一個數字");

                // 用戶身分驗證
                var memberId = User.FindFirst("MemberId")?.Value;
                if (string.IsNullOrEmpty(memberId))
                {
                    return Unauthorized("無法取得使用者資訊");
                }

                var member = await _context.Members.FindAsync(int.Parse(memberId));
                if (member == null || !member.IsActive)
                {
                    return BadRequest("帳號不存在或已停用");
                }

                // 舊密碼驗證
                if (!HashUtility.VerifyPassword(dto.OldPassword, member.Password))
                {
                    return BadRequest("原密碼錯誤");
                }

                // 更新密碼
                member.Password = HashUtility.HashPassword(dto.NewPassword);
                await _context.SaveChangesAsync();

                // 返回需要重新登入的標記
                return Ok(new
                {
                    message = "密碼已成功更新，請重新登入",
                    requireRelogin = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"修改密碼失敗：{ex.Message}");
                return StatusCode(500, new { message = "修改密碼過程發生錯誤", error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost]
        [Route("UpdateProfile")] // 更新會員個人資料
        public async Task<IActionResult> UpdateProfile([FromBody] MemberProfileDto dto)
        {
            try
            {
                // 取得當前會員ID
                var memberId = User.FindFirst("MemberId")?.Value;
                if (string.IsNullOrEmpty(memberId))
                {
                    return Unauthorized("無法取得使用者資訊");
                }

                var memberIdInt = int.Parse(memberId);
                var member = await _context.Members.FindAsync(memberIdInt);
                if (member == null || !member.IsActive)
                {
                    return BadRequest("帳號不存在或已停用");
                }

                // 驗證使用者名稱 (暱稱)
                if (!string.IsNullOrWhiteSpace(dto.UserName) && dto.UserName != member.UserName)
                {
                    if (dto.UserName.Length < 2 || dto.UserName.Length > 25)
                    {
                        return BadRequest("暱稱長度必須在2到25個字元之間");
                    }

                    // 檢查使用者名稱是否已存在
                    var existingMember = await _context.Members.FirstOrDefaultAsync(m => m.UserName == dto.UserName && m.Id != memberIdInt);
                    if (existingMember != null)
                    {
                        return BadRequest("該暱稱已被使用");
                    }

                    member.UserName = dto.UserName;
                }

                // 檢查 MemberProfile 是否存在（在註冊時已創建）
                var memberProfile = await _context.MemberProfiles
                    .FirstOrDefaultAsync(mp => mp.MemberId == memberIdInt);
                if (memberProfile == null)
                {
                    memberProfile = new MemberProfile
                    {
                        MemberId = memberIdInt,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.MemberProfiles.Add(memberProfile);
                }

                // 更新現有的個人檔案，所有欄位都可以明確設為 null
                memberProfile.RealName = dto.RealName;     // 可以為 null，表示清空
                memberProfile.Gender = dto.Gender;         // 可以為 null，表示清空
                memberProfile.Phone = dto.Phone;           // 可以為 null，表示清空
                memberProfile.Photo = dto.PhotoUrl;        // 可以為 null，表示清空
                memberProfile.Birthdate = dto.Birthday;    // 可以為 null，表示清空
                memberProfile.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // 回傳結果，如果某些欄位為 null，則在回傳時轉換為空字串，避免前端處理問題
                return Ok(new
                {
                    message = "個人資料更新成功",
                    profile = new
                    {
                        username = member.UserName,
                        email = member.Email,
                        realName = memberProfile.RealName ?? string.Empty,
                        gender = memberProfile.Gender ?? string.Empty,
                        birthday = memberProfile.Birthdate,
                        phone = memberProfile.Phone ?? string.Empty,
                        photoUrl = memberProfile.Photo ?? string.Empty
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新個人資料失敗：{ex.Message}");
                return StatusCode(500, new { message = "更新個人資料過程發生錯誤", error = ex.Message });
            }
        }

        [Authorize]
        [HttpGet]
        [Route("GetProfile")]  // 獲取個人檔案
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                // 取得 JWT 中的 MemberId
                var memberId = User.FindFirst("MemberId")?.Value;
                if (string.IsNullOrEmpty(memberId))
                {
                    return Unauthorized("無法取得使用者資訊");
                }

                var memberIdInt = int.Parse(memberId);
                var member = await _context.Members.FindAsync(memberIdInt);
                if (member == null)
                {
                    return NotFound("會員不存在");
                }

                // 獲取個人檔案
                var memberProfile = await _context.MemberProfiles
                    .FirstOrDefaultAsync(mp => mp.MemberId == memberIdInt);

                if (memberProfile == null)
                {
                    return Ok(new
                    {
                        username = member.UserName,
                        email = member.Email,
                        realName = "",
                        gender = "",
                        birthday = (DateTime?)null,
                        phone = "",
                        photoUrl = ""
                    });
                }

                return Ok(new
                {
                    loginProvider = member.LoginProvider, // 判斷登入方式
                    username = member.UserName,
                    email = member.Email,
                    realName = memberProfile.RealName,
                    gender = memberProfile.Gender,
                    birthday = memberProfile.Birthdate,
                    phone = memberProfile.Phone,
                    photoUrl = memberProfile.Photo
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"獲取個人資料失敗：{ex.Message}");
                return StatusCode(500, new { message = "獲取個人資料過程發生錯誤", error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost]
        [Route("UploadProfileImage")] // 上傳個人檔案圖片
        public async Task<IActionResult> UploadProfileImage(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("未選擇檔案");

                // 檢查檔案類型
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                    return BadRequest("只允許上傳 JPG、PNG 或 GIF 格式的圖片");

                // 檢查檔案大小 (例如限制 5MB)
                if (file.Length > 5 * 1024 * 1024)
                    return BadRequest("圖片大小不能超過 5MB");

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var imageUrl = $"/uploads/profiles/{fileName}"; // 相對路徑
                return Ok(new { url = imageUrl });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"上傳圖片失敗：{ex.Message}");
                return StatusCode(500, new { message = "上傳圖片過程發生錯誤", error = ex.Message });
            }
        }

        [HttpGet("auth/google/callback")] // Google第三方註冊+登入
        public async Task<IActionResult> GoogleCallback([FromQuery] string code)
        {
            if (string.IsNullOrEmpty(code))
                return BadRequest(new { message = "缺少授權碼" });

            // 1. 向 Google 交換 token
            var tokenResponse = await ExchangeCodeForToken(code);
            if (string.IsNullOrEmpty(tokenResponse?.IdToken))
            {
                Console.WriteLine("Google 回傳的 id_token 為空");
                return Unauthorized(new { message = "Google 授權失敗，無法取得使用者資訊" });
            }

            // 2. 解析 id_token，取得使用者資訊
            var googleUser = ParseGoogleIdToken(tokenResponse.IdToken);
            if (googleUser == null || string.IsNullOrEmpty(googleUser.Email))
                return Unauthorized(new { message = "無法取得 Google 使用者資訊" });

            // 3. 查詢或建立本地帳號
            var member = await _context.Members.FirstOrDefaultAsync(m => m.Email == googleUser.Email);
            if(member != null && member.LoginProvider != "Google")
            {
                var errorMessage = Uri.EscapeDataString("此Email已被其他方式註冊，請使用原有方式登入");
                return Redirect($"http://localhost:5173/auth/callback?message={errorMessage}");
            }
            if (member == null)
            {
                var rawName = googleUser.Name ?? "GoogleUser"; // 預設名稱
                var uniqueName = await GenerateUniqueUserName(rawName);

                member = new Member
                {
                    Email = googleUser.Email,
                    UserName = uniqueName,
                    IsActive = true, // 啟用帳號
                    IsConfirmed = true, // Google 帳號已驗證
                    LoginProvider = "Google",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Members.Add(member);
                await _context.SaveChangesAsync();

                // 建立對應的個人檔案
                var profile = new MemberProfile
                {
                    MemberId = member.Id
                };

                await _context.MemberProfiles.AddAsync(profile);
                await _context.SaveChangesAsync();
            }

            // 4. 發行 JWT token
            var jwt = GenerateJwtToken(member);
            // 直接重導向到前端，帶上必要資訊
            // 對 token 進行 URL 編碼
            var encodedToken = Uri.EscapeDataString(jwt);
            var encodedUserName = Uri.EscapeDataString(member.UserName ?? "");
            var encodedEmail = Uri.EscapeDataString(member.Email ?? "");
            var redirectUrl = $"http://localhost:5173/auth/callback?token={encodedToken}&memberId={member.Id}&username={encodedUserName}&email={encodedEmail}&loginProvider=Google";

            Console.WriteLine($"準備重導向到: {redirectUrl}");

            return Redirect(redirectUrl);
        }

        // 交換 Google 授權碼為 token
        private async Task<GoogleTokenResponseDto> ExchangeCodeForToken(string code)
        {
            var client = new HttpClient();
            var values = new Dictionary<string, string>
        {
            { "code", code },
            { "client_id", _configuration["Google:ClientId"] },
            { "client_secret", _configuration["Google:ClientSecret"] },
            { "redirect_uri", _configuration["Google:RedirectUri"] },
            { "grant_type", "authorization_code" }
        };

            var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(values));
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Google Token Exchange Failed:");
                Console.WriteLine($"Status Code: {response.StatusCode}");
                Console.WriteLine($"Response Content: {content}");
                return null;
            }

            return JsonConvert.DeserializeObject<GoogleTokenResponseDto>(content);

        }

        // 解析 Google 回傳的 id_token
        private GoogleUserInfoDto ParseGoogleIdToken(string idToken)
        {
            if (string.IsNullOrEmpty(idToken))
            {
                Console.WriteLine("Google 回傳的 id_token 為空");
                return null;
            }

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(idToken);
            var email = token.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var name = token.Claims.FirstOrDefault(c => c.Type == "name")?.Value;

            return new GoogleUserInfoDto
            {
                Email = email,
                Name = name
            };
        }

        // 產生 JWT Token
        private string GenerateJwtToken(Member member)
        {
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, _configuration["Jwt:Subject"] ?? "JwtSubject"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("MemberId", member.Id.ToString()),
            new Claim("Email", member.Email ?? ""),
            new Claim("UserName", member.UserName ?? ""),
            new Claim("LoginProvider", member.LoginProvider)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(60),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // 產生不重複的使用者名稱
        private async Task<string> GenerateUniqueUserName(string baseName)
        {
            string finalName = baseName;
            int suffix = 1;

            while (await _context.Members.AnyAsync(m => m.UserName == finalName))
            {
                finalName = $"{baseName}_{suffix}";
                suffix++;
            }

            return finalName;
        }
    }

}
