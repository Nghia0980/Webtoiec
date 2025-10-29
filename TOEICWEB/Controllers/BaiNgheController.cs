﻿using Microsoft.AspNetCore.Authorization;
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
    public class BaiNgheController : ControllerBase
    {
        private readonly SupabaseDbContext _context;

        public BaiNgheController(SupabaseDbContext context)
        {
            _context = context;
        }

        // LẤY DANH SÁCH TẤT CẢ BÀI NGHE
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

        // LẤY CHI TIẾT BÀI NGHE
        [HttpGet("{maBaiNghe}")]
        public async Task<IActionResult> GetBaiNgheDetail(string maBaiNghe)
        {
            try
            {
                var baiNghe = await _context.BaiNghes
                    .FirstOrDefaultAsync(b => b.MaBaiNghe == maBaiNghe);

                if (baiNghe == null)
                    return NotFound(new { message = "Bài nghe không tồn tại!" });

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

        // NỘP BÀI NGHE - ĐÃ FIX 100%: GÁN MaKetQua, TRANSACTION, LẦN LÀM
        [Authorize]
        [HttpPost("submit/{maBaiNghe}")]
        public async Task<IActionResult> SubmitBaiNghe(string maBaiNghe, [FromBody] SubmitBaiNgheVM model)
        {
            if (model?.TraLois == null || !model.TraLois.Any())
                return BadRequest(new { message = "Dữ liệu trả lời không hợp lệ!" });

            var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(maNd))
                return Unauthorized(new { message = "Không tìm thấy thông tin người dùng!" });

            try
            {
                var baiNghe = await _context.BaiNghes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.MaBaiNghe == maBaiNghe);

                if (baiNghe == null)
                    return NotFound(new { message = "Bài nghe không tồn tại!" });

                var cauHoiDapAnDung = await _context.CauHoiNghes
                    .Where(c => c.MaBaiNghe == maBaiNghe)
                    .Select(c => new
                    {
                        c.MaCauHoi,
                        Diem = c.Diem ?? 1,
                        DapAnDung = _context.DapAnNghes
                            .Where(d => d.MaCauHoi == c.MaCauHoi && d.LaDapAnDung == true)
                            .Select(d => d.MaDapAn)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                if (!cauHoiDapAnDung.Any())
                    return BadRequest(new { message = "Bài nghe không có câu hỏi!" });

                var traLoiEntities = new List<TraLoiHocVienNghe>();
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

                    traLoiEntities.Add(new TraLoiHocVienNghe
                    {
                        MaNd = maNd,
                        MaCauHoi = ch.MaCauHoi,
                        MaDapAnChon = maDapAnChon,
                        DungSai = dungSai,
                        NgayTao = DateTime.Now
                        // MaKetQua sẽ được gán sau
                    });
                }

                var phanTram = diemToiDa > 0 ? Math.Round((double)diem / diemToiDa * 100, 2) : 0;

                var lanLamThu = await _context.KetQuaBaiNghes
                    .CountAsync(k => k.MaBaiNghe == maBaiNghe && k.MaNd == maNd) + 1;

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 1. Lưu KetQuaBaiNghe trước để lấy MaKetQua
                    var ketQua = new KetQuaBaiNghe
                    {
                        MaBaiNghe = maBaiNghe,
                        MaNd = maNd,
                        Diem = diem,
                        DiemToiDa = diemToiDa,
                        PhanTram = Convert.ToDecimal(phanTram),
                        ThoiGianLamGiay = model.ThoiGianLamGiay,
                        LanLamThu = lanLamThu,
                        NgayNop = DateTime.Now
                    };
                    _context.KetQuaBaiNghes.Add(ketQua);
                    await _context.SaveChangesAsync(); // Lưu để sinh MaKetQua

                    // 2. Gán MaKetQua cho từng TraLoi
                    foreach (var traLoi in traLoiEntities)
                    {
                        traLoi.MaKetQua = ketQua.MaKetQua;
                    }

                    // 3. Lưu TraLoiHocVienNghe
                    await _context.TraLoiHocVienNghes.AddRangeAsync(traLoiEntities);
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
                    maBaiNghe,
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

        // LẤY TẤT CẢ LỊCH SỬ BÀI NGHE
        [Authorize]
        [HttpGet("history")]
        public async Task<IActionResult> GetAllBaiNgheHistory()
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd))
                    return BadRequest(new { message = "Không tìm thấy thông tin người dùng" });

                var ketQuas = await _context.KetQuaBaiNghes
                    .Where(k => k.MaNd == maNd)
                    .Include(k => k.MaBaiNgheNavigation)
                    .Select(k => new
                    {
                        maBaiNghe = k.MaBaiNghe,
                        tieuDe = k.MaBaiNgheNavigation.TieuDe,
                        doKho = k.MaBaiNgheNavigation.DoKho,
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
                return StatusCode(500, new { message = "Lỗi!", error = ex.Message });
            }
        }

        // LẤY LỊCH SỬ BÀI NGHE CỤ THỂ
        [Authorize]
        [HttpGet("history/{maBaiNghe}")]
        public async Task<IActionResult> GetBaiNgheHistory(string maBaiNghe)
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd))
                    return BadRequest(new { message = "Không tìm thấy người dùng" });

                var baiNghe = await _context.BaiNghes.FirstOrDefaultAsync(b => b.MaBaiNghe == maBaiNghe);
                if (baiNghe == null)
                    return NotFound(new { message = "Bài nghe không tồn tại" });

                var ketQuas = await _context.KetQuaBaiNghes
                    .Where(k => k.MaBaiNghe == maBaiNghe && k.MaNd == maNd)
                    .Select(k => new
                    {
                        tieuDe = baiNghe.TieuDe,
                        doKho = baiNghe.DoKho,
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
                return StatusCode(500, new { message = "Lỗi!", error = ex.Message });
            }
        }

        // THỐNG KÊ TỔNG HỢP
        [Authorize]
        [HttpGet("history/stats/summary")]
        public async Task<IActionResult> GetBaiNgheHistoryStats()
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd))
                    return BadRequest(new { message = "Không tìm thấy người dùng" });

                var ketQuas = await _context.KetQuaBaiNghes
                    .Where(k => k.MaNd == maNd)
                    .Include(k => k.MaBaiNgheNavigation)
                    .ToListAsync();

                if (!ketQuas.Any())
                {
                    return Ok(new
                    {
                        message = "Bạn chưa làm bài nghe nào",
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
                    maBaiNghe = k.MaBaiNghe,
                    tieuDe = k.MaBaiNgheNavigation.TieuDe,
                    doKho = k.MaBaiNgheNavigation.DoKho,
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
                    message = "Thống kê lịch sử bài nghe",
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