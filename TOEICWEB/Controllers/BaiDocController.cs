using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TOEICWEB.Data;
using TOEICWEB.Models;
using TOEICWEB.ViewModels;
using System.Security.Claims;

namespace ToeicWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BaiDocController : ControllerBase
    {
        private readonly SupabaseDbContext _context;

        public BaiDocController(SupabaseDbContext context)
        {
            _context = context;
        }

        // LẤY DANH SÁCH TẤT CẢ BÀI ĐỌC
        [HttpGet]
        public async Task<IActionResult> GetAllBaiDoc()
        {
            try
            {
                var baiDocs = await _context.BaiDocs
                    .Select(b => new BaiDocDTO
                    {
                        MaBaiDoc = b.MaBaiDoc,
                        MaBai = b.MaBai,
                        TieuDe = b.TieuDe,
                        DoKho = b.DoKho,
                        NgayTao = b.NgayTao,
                        DuongDanFileTxt = b.DuongDanFileTxt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    message = "Danh sách bài đọc",
                    total = baiDocs.Count,
                    data = baiDocs
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách!", error = ex.Message });
            }
        }

        // LẤY CHI TIẾT BÀI ĐỌC (Nội dung + Câu hỏi + Đáp án)
        [HttpGet("{maBaiDoc}")]
        public async Task<IActionResult> GetBaiDocDetail(string maBaiDoc)
        {
            try
            {
                var baiDoc = await _context.BaiDocs
                    .FirstOrDefaultAsync(b => b.MaBaiDoc == maBaiDoc);

                if (baiDoc == null)
                    return NotFound(new { message = "Bài đọc không tồn tại!" });

                var cauHois = await _context.CauHoiDocs
                    .Where(c => c.MaBaiDoc == maBaiDoc)
                    .Select(c => new CauHoiDocDTO
                    {
                        MaCauHoi = c.MaCauHoi,
                        NoiDungCauHoi = c.NoiDungCauHoi,
                        GiaiThich = c.GiaiThich,
                        Diem = c.Diem ?? 1,
                        ThuTuHienThi = c.ThuTuHienThi
                    })
                    .OrderBy(c => c.ThuTuHienThi)
                    .ToListAsync();

                var cauHoisWithAnswers = new List<CauHoiDocWithAnswersDTO>();
                foreach (var cauHoi in cauHois)
                {
                    var dapAns = await _context.DapAnDocs
                        .Where(d => d.MaCauHoi == cauHoi.MaCauHoi)
                        .Select(d => new DapAnDocDTO
                        {
                            MaDapAn = d.MaDapAn,
                            MaCauHoi = d.MaCauHoi,
                            NhanDapAn = d.NhanDapAn.ToString(),
                            NoiDungDapAn = d.NoiDungDapAn,
                            ThuTuHienThi = d.ThuTuHienThi,
                            LaDapAnDung = d.LaDapAnDung ?? false
                        })
                        .OrderBy(d => d.ThuTuHienThi)
                        .ToListAsync();

                    cauHoisWithAnswers.Add(new CauHoiDocWithAnswersDTO
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
                    maBaiDoc = baiDoc.MaBaiDoc,
                    maBai = baiDoc.MaBai,
                    tieuDe = baiDoc.TieuDe,
                    doKho = baiDoc.DoKho,
                    noiDung = baiDoc.NoiDung,
                    duongDanFileTxt = baiDoc.DuongDanFileTxt,
                    ngayTao = baiDoc.NgayTao,
                    tongCauHoi = cauHoisWithAnswers.Count,
                    cauHois = cauHoisWithAnswers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy chi tiết!", error = ex.Message });
            }
        }

        // NỘP BÀI ĐỌC - ĐÃ FIX + TỐI ƯU + TRANSACTION
        [Authorize]
        [HttpPost("submit/{maBaiDoc}")]
        public async Task<IActionResult> SubmitBaiDoc(string maBaiDoc, [FromBody] SubmitBaiDocVM model)
        {
            if (model?.TraLois == null || !model.TraLois.Any())
                return BadRequest(new { message = "Dữ liệu trả lời không hợp lệ!" });

            var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(maNd))
                return Unauthorized(new { message = "Không tìm thấy thông tin người dùng!" });

            try
            {
                var baiDoc = await _context.BaiDocs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.MaBaiDoc == maBaiDoc);

                if (baiDoc == null)
                    return NotFound(new { message = "Bài đọc không tồn tại!" });

                var cauHoiDapAnDung = await _context.CauHoiDocs
                    .Where(c => c.MaBaiDoc == maBaiDoc)
                    .Select(c => new
                    {
                        c.MaCauHoi,
                        Diem = c.Diem ?? 1,
                        DapAnDung = _context.DapAnDocs
                            .Where(d => d.MaCauHoi == c.MaCauHoi && d.LaDapAnDung == true)
                            .Select(d => d.MaDapAn)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                if (!cauHoiDapAnDung.Any())
                    return BadRequest(new { message = "Bài đọc không có câu hỏi!" });

                var traLoiEntities = new List<TraLoiHocVienDoc>();
                var chiTietKetQua = new List<object>();
                int diem = 0, diemToiDa = 0;

                foreach (var ch in cauHoiDapAnDung)
                {
                    diemToiDa += ch.Diem;
                    var traLoi = model.TraLois.FirstOrDefault(t => t.MaCauHoi == ch.MaCauHoi);
                    var maDapAnChon = traLoi?.MaDapAn;
                    var dungSai = maDapAnChon.HasValue && maDapAnChon == ch.DapAnDung;

                    if (dungSai) diem += ch.Diem;

                    chiTietKetQua.Add(new
                    {
                        maCauHoi = ch.MaCauHoi,
                        maDapAnChon,
                        dapAnDung = ch.DapAnDung,
                        dungSai,
                        diemCauHoi = ch.Diem
                    });

                    traLoiEntities.Add(new TraLoiHocVienDoc
                    {
                        MaNd = maNd,
                        MaCauHoi = ch.MaCauHoi,
                        MaDapAnChon = maDapAnChon,
                        DungSai = dungSai,
                        NgayTao = DateTime.Now
                    });
                }

                var phanTram = diemToiDa > 0 ? Math.Round((double)diem / diemToiDa * 100, 2) : 0;

                var lanLamThu = await _context.KetQuaBaiDocs
                    .CountAsync(k => k.MaBaiDoc == maBaiDoc && k.MaNd == maNd) + 1;

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    await _context.TraLoiHocVienDocs.AddRangeAsync(traLoiEntities);

                    var ketQua = new KetQuaBaiDoc
                    {
                        MaBaiDoc = maBaiDoc,
                        MaNd = maNd,
                        Diem = diem,
                        DiemToiDa = diemToiDa,
                        PhanTram = Convert.ToDecimal(phanTram),
                        ThoiGianLamGiay = model.ThoiGianLamGiay,
                        LanLamThu = lanLamThu,
                        NgayNop = DateTime.Now
                    };
                    _context.KetQuaBaiDocs.Add(ketQua);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new Exception("Lỗi khi lưu vào DB: " + ex.Message);
                }

                return Ok(new
                {
                    message = "Nộp bài thành công!",
                    maBaiDoc,
                    diem,
                    diemToiDa,
                    phanTram,
                    thoiGianLamGiay = model.ThoiGianLamGiay,
                    lanLamThu,
                    tongCauHoi = cauHoiDapAnDung.Count,
                    soCauDung = chiTietKetQua.Count(x => (bool)x.GetType().GetProperty("dungSai").GetValue(x)),
                    chiTiet = chiTietKetQua
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Lỗi khi nộp bài!",
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }
        }

        // LẤY TẤT CẢ LỊCH SỬ BÀI ĐỌC CỦA NGƯỜI DÙNG
        [Authorize]
        [HttpGet("history")]
        public async Task<IActionResult> GetAllBaiDocHistory()
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd))
                    return BadRequest(new { message = "Không tìm thấy thông tin người dùng" });

                var ketQuas = await _context.KetQuaBaiDocs
                    .Where(k => k.MaNd == maNd)
                    .Include(k => k.MaBaiDocNavigation)
                    .Select(k => new
                    {
                        maBaiDoc = k.MaBaiDoc,
                        tieuDe = k.MaBaiDocNavigation.TieuDe,
                        doKho = k.MaBaiDocNavigation.DoKho,
                        diem = k.Diem ?? 0,
                        diemToiDa = k.DiemToiDa ?? 1,
                        phanTram = k.PhanTram,
                        thoiGianLamGiay = k.ThoiGianLamGiay ?? 0,
                        thoiGianLamPhut = (k.ThoiGianLamGiay ?? 0) / 60,
                        lanLamThu = k.LanLamThu ?? 1,
                        ngayNop = k.NgayNop,
                        ngayNopFormatted = k.NgayNop.HasValue
                            ? k.NgayNop.Value.ToString("dd/MM/yyyy HH:mm")
                            : ""
                    })
                    .OrderByDescending(k => k.ngayNop)
                    .ToListAsync();

                if (!ketQuas.Any())
                {
                    return Ok(new
                    {
                        message = "Bạn chưa làm bài đọc nào",
                        total = 0,
                        data = new List<object>()
                    });
                }

                return Ok(new
                {
                    message = "Lịch sử bài đọc",
                    total = ketQuas.Count,
                    data = ketQuas
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi!", error = ex.Message });
            }
        }

        // LẤY LỊCH SỬ BÀI ĐỌC CỤ THỂ
        [Authorize]
        [HttpGet("history/{maBaiDoc}")]
        public async Task<IActionResult> GetBaiDocHistory(string maBaiDoc)
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd))
                    return BadRequest(new { message = "Không tìm thấy người dùng" });

                var baiDoc = await _context.BaiDocs.FirstOrDefaultAsync(b => b.MaBaiDoc == maBaiDoc);
                if (baiDoc == null)
                    return NotFound(new { message = "Bài đọc không tồn tại" });

                var ketQuas = await _context.KetQuaBaiDocs
                    .Where(k => k.MaBaiDoc == maBaiDoc && k.MaNd == maNd)
                    .Select(k => new
                    {
                        tieuDe = baiDoc.TieuDe,
                        doKho = baiDoc.DoKho,
                        diem = k.Diem ?? 0,
                        diemToiDa = k.DiemToiDa ?? 1,
                        phanTram = k.PhanTram,
                        thoiGianLamGiay = k.ThoiGianLamGiay ?? 0,
                        thoiGianLamPhut = (k.ThoiGianLamGiay ?? 0) / 60,
                        lanLamThu = k.LanLamThu ?? 1,
                        ngayNop = k.NgayNop,
                        ngayNopFormatted = k.NgayNop.HasValue
                            ? k.NgayNop.Value.ToString("dd/MM/yyyy HH:mm")
                            : ""
                    })
                    .OrderByDescending(k => k.ngayNop)
                    .ToListAsync();

                if (!ketQuas.Any())
                {
                    return Ok(new
                    {
                        message = $"Bạn chưa làm bài đọc '{baiDoc.TieuDe}'",
                        total = 0,
                        data = new List<object>()
                    });
                }

                return Ok(new
                {
                    message = $"Lịch sử bài đọc '{baiDoc.TieuDe}'",
                    total = ketQuas.Count,
                    data = ketQuas
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi!", error = ex.Message });
            }
        }

        // THỐNG KÊ TỔNG HỢP
        [Authorize]
        [HttpGet("history/stats/summary")]
        public async Task<IActionResult> GetBaiDocHistoryStats()
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd))
                    return BadRequest(new { message = "Không tìm thấy người dùng" });

                var ketQuas = await _context.KetQuaBaiDocs
                    .Where(k => k.MaNd == maNd)
                    .Include(k => k.MaBaiDocNavigation)
                    .ToListAsync();

                if (!ketQuas.Any())
                {
                    return Ok(new
                    {
                        message = "Bạn chưa làm bài đọc nào",
                        tongBaiDaLam = 0,
                        diemTrungBinh = 0,
                        thoiGianHocTongCong = 0,
                        data = new List<object>()
                    });
                }

                var diemTrungBinh = Math.Round(
                    ketQuas.Average(k => (double)(k.Diem ?? 0) / (double)(k.DiemToiDa ?? 1) * 100), 2);

                var thoiGianTongCong = ketQuas.Sum(k => k.ThoiGianLamGiay ?? 0);

                var detail = ketQuas.Select(k => new
                {
                    maBaiDoc = k.MaBaiDoc,
                    tieuDe = k.MaBaiDocNavigation.TieuDe,
                    doKho = k.MaBaiDocNavigation.DoKho,
                    diem = k.Diem ?? 0,
                    diemToiDa = k.DiemToiDa ?? 1,
                    phanTram = k.PhanTram,
                    thoiGianLamPhut = (k.ThoiGianLamGiay ?? 0) / 60,
                    lanLamThu = k.LanLamThu ?? 1,
                    ngayNopFormatted = k.NgayNop.HasValue
                        ? k.NgayNop.Value.ToString("dd/MM/yyyy HH:mm")
                        : ""
                })
                .OrderByDescending(k => k.ngayNopFormatted)
                .ToList();

                return Ok(new
                {
                    message = "Thống kê lịch sử bài đọc",
                    tongBaiDaLam = ketQuas.Count,
                    diemTrungBinh,
                    thoiGianHocPhut = thoiGianTongCong / 60,
                    data = detail
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi!", error = ex.Message });
            }
        }
    }
}