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
                // 1. LẤY THÔNG TIN BÀI HỌC CHÍNH
                var baiHoc = await _context.BaiHocs
                    .Where(b => b.MaBai == maBai)
                    .Select(b => new
                    {
                        b.MaBai,
                        b.MaLoTrinh,
                        b.TenBai,
                        b.MoTa,
                        ThoiLuongPhut = b.ThoiLuongPhut ?? 0,
                        b.SoThuTu,
                        b.NgayTao
                    })
                    .FirstOrDefaultAsync();

                if (baiHoc == null)
                    return NotFound(new { message = "Bài học không tồn tại!" });

                // 2. LẤY TẤT CẢ VIDEO LIÊN QUAN
                var videos = await _context.VideoBaiHocs
                    .Where(v => v.MaBai == maBai)
                    .Select(v => new VideoBaiHocDTO
                    {
                        MaVideo = v.MaVideo,
                        TieuDeVideo = v.TieuDeVideo,
                        DuongDanVideo = v.DuongDanVideo,
                        ThoiLuongGiay = v.ThoiLuongGiay,
                        NgayTao = v.NgayTao
                    })
                    .OrderBy(v => v.TieuDeVideo)
                    .ToListAsync();

                // 3. LẤY TẤT CẢ BÀI NGHE LIÊN QUAN
                var baiNghes = await _context.BaiNghes
                    .Where(bn => bn.MaBai == maBai)
                    .Select(bn => new BaiNgheDTO
                    {
                        MaBaiNghe = bn.MaBaiNghe,
                        MaBai = bn.MaBai,
                        TieuDe = bn.TieuDe,
                        DoKho = bn.DoKho,
                        NgayTao = bn.NgayTao,
                        DuongDanAudio = bn.DuongDanAudio,
                        BanGhiAm = bn.BanGhiAm
                    })
                    .OrderBy(bn => bn.TieuDe)
                    .ToListAsync();

                // 4. LẤY TẤT CẢ BÀI ĐỌC LIÊN QUAN
                var baiDocs = await _context.BaiDocs
                    .Where(bd => bd.MaBai == maBai)
                    .Select(bd => new BaiDocDTO
                    {
                        MaBaiDoc = bd.MaBaiDoc,
                        MaBai = bd.MaBai,
                        TieuDe = bd.TieuDe,
                        DoKho = bd.DoKho,
                        NgayTao = bd.NgayTao,
                        DuongDanFileTxt = bd.DuongDanFileTxt
                    })
                    .OrderBy(bd => bd.TieuDe)
                    .ToListAsync();

                // 5. TRẢ VỀ KẾT QUẢ ĐẦY ĐỦ
                return Ok(new
                {
                    message = "Chi tiết bài học",
                    data = new
                    {
                        baiHoc.MaBai,
                        baiHoc.MaLoTrinh,
                        baiHoc.TenBai,
                        baiHoc.MoTa,
                        baiHoc.ThoiLuongPhut,
                        baiHoc.SoThuTu,
                        baiHoc.NgayTao,
                        Videos = videos,
                        BaiNghes = baiNghes,
                        BaiDocs = baiDocs
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy chi tiết bài học!", error = ex.Message });
            }
        }

    }
}