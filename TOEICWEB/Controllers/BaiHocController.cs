using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TOEICWEB.Data;
using TOEICWEB.ViewModels;

namespace ToeicWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BaiHocController : ControllerBase
    {
        private readonly SupabaseDbContext _context;

        public BaiHocController(SupabaseDbContext context)
        {
            _context = context;
        }

        // ✅ LẤY DANH SÁCH TẤT CẢ BÀI HỌC
        [HttpGet]
        public async Task<IActionResult> GetAllBaiHoc()
        {
            try
            {
                var baiHocs = await _context.BaiHocs
                    .Select(b => new BaiHocDTO
                    {
                        MaBai = b.MaBai,
                        MaLoTrinh = b.MaLoTrinh,
                        TenBai = b.TenBai,
                        MoTa = b.MoTa,
                        ThoiLuongPhut = b.ThoiLuongPhut ?? 0,
                        SoThuTu = b.SoThuTu,
                        NgayTao = b.NgayTao
                    })
                    .OrderBy(b => b.SoThuTu)
                    .ToListAsync();

                return Ok(new
                {
                    message = "Danh sách bài học",
                    total = baiHocs.Count,
                    data = baiHocs
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách bài học!", error = ex.Message });
            }
        }

        // ✅ LẤY CHI TIẾT BÀI HỌC
        [HttpGet("{maBai}")]
        public async Task<IActionResult> GetBaiHocDetail(string maBai)
        {
            try
            {
                var baiHoc = await _context.BaiHocs
                    .FirstOrDefaultAsync(b => b.MaBai == maBai);

                if (baiHoc == null)
                    return NotFound(new { message = "Bài học không tồn tại!" });

                return Ok(new
                {
                    maBai = baiHoc.MaBai,
                    maLoTrinh = baiHoc.MaLoTrinh,
                    tenBai = baiHoc.TenBai,
                    moTa = baiHoc.MoTa,
                    thoiLuongPhut = baiHoc.ThoiLuongPhut ?? 0,
                    soThuTu = baiHoc.SoThuTu,
                    ngayTao = baiHoc.NgayTao
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy chi tiết bài học!", error = ex.Message });
            }
        }
    }
}