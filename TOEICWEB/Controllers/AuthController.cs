using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using TOEICWEB.Data;
using TOEICWEB.Models;
using TOEICWEB.ViewModels;
using System.Security.Claims;

namespace ToeicWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly SupabaseDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(SupabaseDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }



        // 🔹 Tạo JWT Token
        private string GenerateJwtToken(NguoiDung user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"] ?? "your-secret-key-here-minimum-32-characters-long"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.MaNd),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.HoTen),
                new Claim("VaiTro", user.VaiTro)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "toeic-app",
                audience: _configuration["Jwt:Audience"] ?? "toeic-users",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ✅ ĐĂNG KÝ NGƯỜI DÙNG
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterVM model)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(model.Email) ||
                    string.IsNullOrWhiteSpace(model.MatKhau) ||
                    string.IsNullOrWhiteSpace(model.HoTen))
                    return BadRequest(new { message = "Email, mật khẩu và họ tên là bắt buộc!" });

                // Kiểm tra định dạng email
                if (!model.Email.Contains("@"))
                    return BadRequest(new { message = "Email không hợp lệ!" });

                // Kiểm tra độ dài mật khẩu
                if (model.MatKhau.Length < 6)
                    return BadRequest(new { message = "Mật khẩu phải ít nhất 6 ký tự!" });

                // Kiểm tra email đã tồn tại
                if (await _context.NguoiDungs.AnyAsync(u => u.Email == model.Email))
                    return BadRequest(new { message = "Email đã tồn tại trong hệ thống!" });

                // Tạo mã ND mới
                var lastUser = await _context.NguoiDungs
                    .Where(u => u.MaNd.StartsWith("ND"))
                    .OrderByDescending(u => u.MaNd)
                    .FirstOrDefaultAsync();

                string newMaNd;
                if (lastUser == null)
                    newMaNd = "ND001";
                else
                {
                    int soCu = int.Parse(lastUser.MaNd.Substring(2));
                    newMaNd = "ND" + (soCu + 1).ToString("D3");
                }

                // Tạo người dùng mới (Không hash, trigger sẽ hash bằng MD5)
                var nguoiDung = new NguoiDung
                {
                    MaNd = newMaNd,
                    Email = model.Email,
                    MatKhau = model.MatKhau, // Gửi plain text, trigger sẽ hash
                    HoTen = model.HoTen,
                    VaiTro = "User", // Mặc định là User
                    NgayDangKy = DateTime.Now,
                    AnhDaiDien = null,
                    LanDangNhapCuoi = null
                };

                _context.NguoiDungs.Add(nguoiDung);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Đăng ký thành công!",
                    ma_nd = newMaNd,
                    email = model.Email,
                    ho_ten = model.HoTen
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi đăng ký!", error = ex.Message });
            }
        }

        // ✅ ĐĂNG NHẬP
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginVM model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.MatKhau))
                    return BadRequest(new { message = "Email và mật khẩu là bắt buộc!" });

                var user = await _context.NguoiDungs
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (user == null)
                    return Unauthorized(new { message = "Sai email hoặc mật khẩu!" });

                // Tính MD5 của mật khẩu nhập vào
                string inputPasswordHash = GetMD5Hash(model.MatKhau);

                // Debug: In ra để kiểm tra
                Console.WriteLine($"Email: {model.Email}");
                Console.WriteLine($"Input MD5: {inputPasswordHash}");
                Console.WriteLine($"DB MD5: {user.MatKhau}");
                Console.WriteLine($"Match: {user.MatKhau == inputPasswordHash}");

                if (user.MatKhau != inputPasswordHash)
                    return Unauthorized(new { message = "Sai email hoặc mật khẩu!" });

                var token = GenerateJwtToken(user);

                return Ok(new
                {
                    message = "Đăng nhập thành công!",
                    token = token,
                    user = new
                    {
                        ma_nd = user.MaNd,
                        ho_ten = user.HoTen,
                        email = user.Email,
                        vai_tro = user.VaiTro,
                        ngay_dang_ky = user.NgayDangKy,
                        anh_dai_dien = user.AnhDaiDien,
                        lan_dang_nhap_cuoi = user.LanDangNhapCuoi
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi đăng nhập!", error = ex.Message });
            }
        }

        // 🔹 Tính MD5 Hash (khớp với trigger database)
        private string GetMD5Hash(string password)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        // ✅ ADMIN: ĐĂNG KÝ ADMIN MỚI (Chỉ admin hiện tại có quyền)
        [Authorize]
        [HttpPost("admin/register")]
        public async Task<IActionResult> RegisterAdmin([FromBody] RegisterVM model)
        {
            try
            {
                // Kiểm tra quyền admin
                var currentUserRole = User.FindFirst("VaiTro")?.Value;
                if (currentUserRole != "Admin")
                    return Forbid();

                // Validate input
                if (string.IsNullOrWhiteSpace(model.Email) ||
                    string.IsNullOrWhiteSpace(model.MatKhau) ||
                    string.IsNullOrWhiteSpace(model.HoTen))
                    return BadRequest(new { message = "Email, mật khẩu và họ tên là bắt buộc!" });

                if (model.MatKhau.Length < 6)
                    return BadRequest(new { message = "Mật khẩu phải ít nhất 6 ký tự!" });

                if (await _context.NguoiDungs.AnyAsync(u => u.Email == model.Email))
                    return BadRequest(new { message = "Email đã tồn tại!" });

                // Tạo mã ADM mới
                var lastAdmin = await _context.NguoiDungs
                    .Where(u => u.MaNd.StartsWith("ADM"))
                    .OrderByDescending(u => u.MaNd)
                    .FirstOrDefaultAsync();

                string newMaAdm;
                if (lastAdmin == null)
                    newMaAdm = "ADM001";
                else
                {
                    int soCu = int.Parse(lastAdmin.MaNd.Substring(3));
                    newMaAdm = "ADM" + (soCu + 1).ToString("D3");
                }

                var admin = new NguoiDung
                {
                    MaNd = newMaAdm,
                    Email = model.Email,
                    MatKhau = model.MatKhau, // Gửi plain text, trigger sẽ hash
                    HoTen = model.HoTen,
                    VaiTro = "Admin",
                    NgayDangKy = DateTime.Now,
                    AnhDaiDien = null,
                    LanDangNhapCuoi = null
                };

                _context.NguoiDungs.Add(admin);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Tạo admin mới thành công!",
                    ma_adm = newMaAdm,
                    email = model.Email,
                    ho_ten = model.HoTen
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tạo admin!", error = ex.Message });
            }
        }

        // ✅ ADMIN: KHÓA/MỞ KHÓA TÀI KHOẢN
       
        [Authorize]
        [HttpGet("admin/users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var currentUserRole = User.FindFirst("VaiTro")?.Value;
                if (currentUserRole != "Admin")
                    return Forbid();

                var users = await _context.NguoiDungs
                    .Select(u => new
                    {
                        u.MaNd,
                        u.Email,
                        u.HoTen,
                        u.VaiTro,
                        u.NgayDangKy,
                        u.AnhDaiDien,
                        u.LanDangNhapCuoi
                    })
                    .ToListAsync();

                return Ok(new
                {
                    message = "Danh sách người dùng",
                    total = users.Count,
                    data = users
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách!", error = ex.Message });
            }
        }

        // ✅ LẤY THÔNG TIN CÁ NHÂN
        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.MaNd == maNd);

                if (user == null)
                    return NotFound(new { message = "Người dùng không tồn tại!" });

                return Ok(new
                {
                    ma_nd = user.MaNd,
                    email = user.Email,
                    ho_ten = user.HoTen,
                    vai_tro = user.VaiTro,
                    ngay_dang_ky = user.NgayDangKy,
                    anh_dai_dien = user.AnhDaiDien,
                    lan_dang_nhap_cuoi = user.LanDangNhapCuoi
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy profile!", error = ex.Message });
            }
        }

        // ✅ CẬP NHẬT THÔNG TIN CÁ NHÂN
        [Authorize]
        [HttpPut("profile/update")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileVM model)
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.MaNd == maNd);

                if (user == null)
                    return NotFound(new { message = "Người dùng không tồn tại!" });

                if (!string.IsNullOrWhiteSpace(model.HoTen))
                    user.HoTen = model.HoTen;

                if (!string.IsNullOrWhiteSpace(model.AnhDaiDien))
                    user.AnhDaiDien = model.AnhDaiDien;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Cập nhật thông tin thành công!",
                    user = new
                    {
                        ma_nd = user.MaNd,
                        email = user.Email,
                        ho_ten = user.HoTen,
                        vai_tro = user.VaiTro,
                        anh_dai_dien = user.AnhDaiDien
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi cập nhật!", error = ex.Message });
            }
        }

        // ✅ ĐỔI MẬT KHẨU
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordVM model)
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.MaNd == maNd);

                if (user == null)
                    return NotFound(new { message = "Người dùng không tồn tại!" });

                // So sánh mật khẩu cũ (hash MD5 để so sánh)
                string oldPasswordHash = GetMD5Hash(model.MatKhauCu);
                if (user.MatKhau != oldPasswordHash)
                    return BadRequest(new { message = "Mật khẩu cũ không chính xác!" });

                if (model.MatKhauMoi.Length < 6)
                    return BadRequest(new { message = "Mật khẩu mới phải ít nhất 6 ký tự!" });

                // Cập nhật mật khẩu (gửi plain text, trigger sẽ hash)
                user.MatKhau = model.MatKhauMoi;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Đổi mật khẩu thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi đổi mật khẩu!", error = ex.Message });
            }
        }

        // ✅ TEST KẾT NỐI DATABASE
        [HttpGet("testdb")]
        public async Task<IActionResult> TestDB()
        {
            try
            {
                var count = await _context.NguoiDungs.CountAsync();
                return Ok(new
                {
                    message = "✅ Kết nối thành công!",
                    total_users = count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "❌ Kết nối thất bại!",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }
    }
}