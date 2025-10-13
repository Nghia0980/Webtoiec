using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TOEICWEB.Data;
using TOEICWEB.Models;
using TOEICWEB.ViewModels;

namespace ToeicWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BaiNgheController : ControllerBase
    {
        private readonly SupabaseDbContext _context;

        public BaiNgheController(SupabaseDbContext context)
        {
            _context = context;
        }

        // ✅ LẤY DANH SÁCH TẤT CẢ BÀI NGHE
        [HttpGet]
        public async Task<IActionResult> GetAllBaiNghe()
        {
            try
            {
                var baiNghes = await _context.BaiNghes
                    .Select(b => new BaiNgheDTO
                    {
                        MaBaiNghe = b.MaBaiNghe,
                        MaBai = b.MaBai,
                        TieuDe = b.TieuDe,
                        DoKho = b.DoKho,
                        NgayTao = b.NgayTao,
                        DuongDanAudio = b.DuongDanAudio,
                        BanGhiAm = b.BanGhiAm
                    })
                    .ToListAsync();

                return Ok(new
                {
                    message = "Danh sách bài nghe",
                    total = baiNghes.Count,
                    data = baiNghes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách!", error = ex.Message });
            }
        }

        // ✅ LẤY CHI TIẾT BÀI NGHE (Bao gồm Nội Dung + Câu Hỏi + Đáp Án)
        [HttpGet("{maBaiNghe}")]
        public async Task<IActionResult> GetBaiNgheDetail(string maBaiNghe)
        {
            try
            {
                var baiNghe = await _context.BaiNghes
                    .FirstOrDefaultAsync(b => b.MaBaiNghe == maBaiNghe);

                if (baiNghe == null)
                    return NotFound(new { message = "Bài nghe không tồn tại!" });

                // Lấy câu hỏi của bài nghe
                var cauHois = await _context.CauHoiNghes
                    .Where(c => c.MaBaiNghe == maBaiNghe)
                    .Select(c => new CauHoiNgheDTO
                    {
                        MaCauHoi = c.MaCauHoi,
                        NoiDungCauHoi = c.NoiDungCauHoi,
                        GiaiThich = c.GiaiThich,
                        Diem = c.Diem ?? 1,
                        ThuTuHienThi = (int)c.ThuTuHienThi
                    })
                    .OrderBy(c => c.ThuTuHienThi)
                    .ToListAsync();

                // Lấy đáp án cho mỗi câu hỏi
                var cauHoisWithAnswers = new List<CauHoiNgheWithAnswersDTO>();
                foreach (var cauHoi in cauHois)
                {
                    var dapAns = await _context.DapAnNghes
                        .Where(d => d.MaCauHoi == cauHoi.MaCauHoi)
                        .Select(d => new DapAnNgheDTO
                        {
                            MaDapAn = d.MaDapAn,
                            MaCauHoi = d.MaCauHoi,
                            NhanDapAn = d.NhanDapAn.ToString(),
                            NoiDungDapAn = d.NoiDungDapAn,
                            ThuTuHienThi = (int)d.ThuTuHienThi,
                            LaDapAnDung = d.LaDapAnDung ?? false
                        })
                        .OrderBy(d => d.ThuTuHienThi)
                        .ToListAsync();

                    cauHoisWithAnswers.Add(new CauHoiNgheWithAnswersDTO
                    {
                        MaCauHoi = cauHoi.MaCauHoi,
                        NoiDungCauHoi = cauHoi.NoiDungCauHoi,
                        GiaiThich = cauHoi.GiaiThich,
                        Diem = cauHoi.Diem,
                        ThuTuHienThi = cauHoi.ThuTuHienThi,
                        DapAns = dapAns
                    });
                }

                return Ok(new
                {
                    maBaiNghe = baiNghe.MaBaiNghe,
                    maBai = baiNghe.MaBai,
                    tieuDe = baiNghe.TieuDe,
                    doKho = baiNghe.DoKho,
                    duongDanAudio = baiNghe.DuongDanAudio,
                    banGhiAm = baiNghe.BanGhiAm,
                    ngayTao = baiNghe.NgayTao,
                    tongCauHoi = cauHoisWithAnswers.Count,
                    cauHois = cauHoisWithAnswers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy chi tiết!", error = ex.Message });
            }
        }

        // ✅ NỘP BÀI NGHE (Yêu cầu xác thực)
        [Authorize]
        [HttpPost("submit/{maBaiNghe}")]
        public async Task<IActionResult> SubmitBaiNghe(string maBaiNghe, [FromBody] SubmitBaiNgheVM model)
        {
            try
            {
                var maNd = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(maNd))
                    return Unauthorized(new { message = "Không tìm thấy mã người dùng trong token!" });

                // 🔍 Kiểm tra bài nghe
                var baiNghe = await _context.BaiNghes
                    .FirstOrDefaultAsync(b => b.MaBaiNghe == maBaiNghe);

                if (baiNghe == null)
                    return NotFound(new { message = "Bài nghe không tồn tại!" });

                // 🔍 Lấy danh sách câu hỏi
                var cauHois = await _context.CauHoiNghes
                    .Where(c => c.MaBaiNghe == maBaiNghe)
                    .ToListAsync();

                if (cauHois.Count == 0)
                    return BadRequest(new { message = "Bài này không có câu hỏi!" });

                int diem = 0;
                int tongCauHoi = cauHois.Count;
                var traLoiKetQua = new List<object>();

                // ✅ Duyệt qua danh sách trả lời học viên gửi lên
                foreach (var tl in model.TraLois)
                {
                    // Lấy câu hỏi tương ứng
                    var cauHoi = cauHois.FirstOrDefault(c => c.MaCauHoi == tl.MaCauHoi);
                    if (cauHoi == null)
                        continue;

                    // Lấy đáp án đúng
                    var dapAnDung = await _context.DapAnNghes
                        .FirstOrDefaultAsync(d => d.MaCauHoi == tl.MaCauHoi && d.LaDapAnDung == true);

                    bool dungSai = dapAnDung?.MaDapAn == tl.MaDapAn;
                    if (dungSai)
                        diem += cauHoi.Diem ?? 1;

                    traLoiKetQua.Add(new
                    {
                        maCauHoi = tl.MaCauHoi,
                        maDapAnChon = tl.MaDapAn,
                        dungSai = dungSai,
                        ngayTao = DateTime.Now
                    });

                    // ✅ Lưu từng câu trả lời vào DB
                    var traLoiEntity = new TOEICWEB.Models.TraLoiHocVienNghe
                    {
                        MaNd = maNd,
                        MaCauHoi = tl.MaCauHoi,
                        MaDapAnChon = tl.MaDapAn,
                        DungSai = dungSai,
                        NgayTao = DateTime.Now
                    };
                    _context.TraLoiHocVienNghes.Add(traLoiEntity);
                }

                await _context.SaveChangesAsync();

                // ✅ Lưu kết quả tổng
                double phanTram = ((double)diem / (tongCauHoi * 1)) * 100.0;
                var ketQua = new TOEICWEB.Models.KetQuaBaiNghe
                {
                    MaBaiNghe = maBaiNghe,
                    MaNd = maNd,
                    Diem = diem,
                    DiemToiDa = tongCauHoi,
                    PhanTram = Convert.ToDecimal(phanTram),
                    ThoiGianLamGiay = model.ThoiGianLamGiay,
                    NgayNop = DateTime.Now
                };

                _context.KetQuaBaiNghes.Add(ketQua);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Nộp bài thành công!",
                    diem = diem,
                    diemToiDa = tongCauHoi,
                    phanTram = Math.Round(phanTram, 2),
                    thanhCong = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi nộp bài!", error = ex.Message });
            }
        }

        // ✅ LẤY LỊCH SỬ BÀI LÀM (Yêu cầu xác thực)
        // API 1: Lấy tất cả lịch sử bài nghe của người dùng hiện tại
        [Authorize]
        [HttpGet("history")]
        public async Task<IActionResult> GetAllBaiNgheHistory()
        {
            try
            {
                var maNd = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(maNd))
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin người dùng" });
                }

                var ketQuas = await _context.KetQuaBaiNghes
                    .Where(k => k.MaNd == maNd)
                    .Select(k => new
                    {
                        maBaiNghe = k.MaBaiNghe,
                        diem = k.Diem ?? 0,
                        diemToiDa = k.DiemToiDa ?? 1,
                        phanTram = k.PhanTram,
                        thang = Math.Round(
                            (decimal)((k.Diem ?? 0) / (double)(k.DiemToiDa ?? 1) * 100),
                            2
                        ),
                        thoiGianLamGiay = k.ThoiGianLamGiay ?? 0,
                        thoiGianLamPhut = (k.ThoiGianLamGiay ?? 0) / 60,
                        lanLamThu = k.LanLamThu ?? 1,
                        ngayNop = k.NgayNop,
                        ngayNopFormatted = k.NgayNop.HasValue ? k.NgayNop.Value.ToString("dd/MM/yyyy HH:mm") : ""
                    })
                    .OrderByDescending(k => k.ngayNop)
                    .ToListAsync();

                if (!ketQuas.Any())
                {
                    return Ok(new
                    {
                        message = "Bạn chưa làm bài nghe nào",
                        total = 0,
                        data = new List<object>()
                    });
                }

                return Ok(new
                {
                    message = "Lịch sử bài nghe",
                    total = ketQuas.Count,
                    data = ketQuas
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Lỗi khi lấy lịch sử bài nghe!",
                    error = ex.Message
                });
            }
        }

        // API 2: Lấy lịch sử bài nghe cụ thể theo mã bài của người dùng hiện tại
        [Authorize]
        [HttpGet("history/{maBaiNghe}")]
        public async Task<IActionResult> GetBaiNgheHistory(string maBaiNghe)
        {
            try
            {
                var maNd = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(maNd))
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin người dùng" });
                }

                // Kiểm tra bài nghe có tồn tại không
                var baiNghe = await _context.BaiNghes.FirstOrDefaultAsync(b => b.MaBaiNghe == maBaiNghe);
                if (baiNghe == null)
                {
                    return NotFound(new { message = "Bài nghe không tồn tại" });
                }

                var ketQuas = await _context.KetQuaBaiNghes
                    .Where(k => k.MaBaiNghe == maBaiNghe && k.MaNd == maNd)
                    .Select(k => new
                    {
                        tieuDe = baiNghe.TieuDe,
                        doKho = baiNghe.DoKho,
                        diem = k.Diem ?? 0,
                        diemToiDa = k.DiemToiDa ?? 1,
                        phanTram = k.PhanTram,
                        thang = Math.Round(
                            (decimal)((k.Diem ?? 0) / (double)(k.DiemToiDa ?? 1) * 100),
                            2
                        ),
                        thoiGianLamGiay = k.ThoiGianLamGiay ?? 0,
                        thoiGianLamPhut = (k.ThoiGianLamGiay ?? 0) / 60,
                        lanLamThu = k.LanLamThu ?? 1,
                        ngayNop = k.NgayNop,
                        ngayNopFormatted = k.NgayNop.HasValue ? k.NgayNop.Value.ToString("dd/MM/yyyy HH:mm") : ""
                    })
                    .OrderByDescending(k => k.ngayNop)
                    .ToListAsync();

                if (!ketQuas.Any())
                {
                    return Ok(new
                    {
                        message = $"Bạn chưa làm bài nghe '{baiNghe.TieuDe}'",
                        total = 0,
                        data = new List<object>()
                    });
                }

                return Ok(new
                {
                    message = $"Lịch sử bài nghe '{baiNghe.TieuDe}'",
                    total = ketQuas.Count,
                    data = ketQuas
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Lỗi khi lấy lịch sử bài nghe!",
                    error = ex.Message
                });
            }
        }

        // API 3: Lấy lịch sử bài nghe với thống kê tổng hợp
        [Authorize]
        [HttpGet("history/stats/summary")]
        public async Task<IActionResult> GetBaiNgheHistoryStats()
        {
            try
            {
                var maNd = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(maNd))
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin người dùng" });
                }

                var ketQuas = await _context.KetQuaBaiNghes
                    .Where(k => k.MaNd == maNd)
                    .ToListAsync();

                if (!ketQuas.Any())
                {
                    return Ok(new
                    {
                        message = "Bạn chưa làm bài nghe nào",
                        tongBaiDaLam = 0,
                        diemTrungBinh = 0,
                        thoiGianHocTongCong = 0,
                        baiHoacTotNhat = new { },
                        data = new List<object>()
                    });
                }

                var diemTrungBinh = Math.Round(
                    ketQuas.Average(k => (double)(k.Diem ?? 0) / (double)(k.DiemToiDa ?? 1) * 100),
                    2
                );

                var thoiGianTongCong = ketQuas.Sum(k => k.ThoiGianLamGiay ?? 0);

                var baiHoacTotNhat = ketQuas
                    .GroupBy(k => k.MaBaiNghe)
                    .Select(g => new
                    {
                        maBaiNghe = g.Key,
                        diemCaoNhat = g.Max(k => k.Diem),
                        soLanLam = g.Count(),
                        diemTrungBinh = Math.Round(
                            g.Average(k => (double)(k.Diem ?? 0)),
                            2
                        )
                    })
                    .OrderByDescending(x => x.diemCaoNhat)
                    .FirstOrDefault();

                var detail = await _context.KetQuaBaiNghes
                    .Where(k => k.MaNd == maNd)
                    .Select(k => new
                    {
                        maBaiNghe = k.MaBaiNghe,
                        diem = k.Diem ?? 0,
                        diemToiDa = k.DiemToiDa ?? 1,
                        thang = Math.Round(
                            (decimal)((k.Diem ?? 0) / (double)(k.DiemToiDa ?? 1) * 100),
                            2
                        ),
                        thoiGianLamPhut = (k.ThoiGianLamGiay ?? 0) / 60,
                        lanLamThu = k.LanLamThu ?? 1,
                        ngayNopFormatted = k.NgayNop.HasValue ? k.NgayNop.Value.ToString("dd/MM/yyyy HH:mm") : ""
                    })
                    .OrderByDescending(k => k.ngayNopFormatted)
                    .ToListAsync();

                return Ok(new
                {
                    message = "Thống kê lịch sử bài nghe",
                    tongBaiDaLam = ketQuas.Count,
                    diemTrungBinh = diemTrungBinh,
                    thoiGianHocTongCong = thoiGianTongCong,
                    thoiGianHocPhut = thoiGianTongCong / 60,
                    baiHoacTotNhat = baiHoacTotNhat,
                    data = detail
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Lỗi khi lấy thống kê!",
                    error = ex.Message
                });
            }
        }
    }
}