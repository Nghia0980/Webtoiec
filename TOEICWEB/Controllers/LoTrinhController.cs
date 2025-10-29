using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TOEICWEB.Data;
using TOEICWEB.Models;
using TOEICWEB.ViewModels.LoTrinh;
using TOEICWEB.ViewModels.LoTrinh.Request;
using YourApp.ViewModels.LoTrinh;

namespace TOEICWEB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoTrinhController : ControllerBase
    {
        private readonly SupabaseDbContext _context;

        public LoTrinhController(SupabaseDbContext context)
        {
            _context = context;
        }

        #region 1. Lấy danh sách lộ trình có sẵn
        [HttpGet("co-san")]
        public async Task<IActionResult> GetLoTrinhCoSan()
        {
            try
            {
                var loTrinhs = await _context.LoTrinhCoSans
                    .Select(lt => new LoTrinhCoSanDto
                    {
                        MaLoTrinh = lt.MaLoTrinh,
                        TenLoTrinh = lt.TenLoTrinh,
                        MoTa = lt.MoTa,
                        ThoiGianDuKien = lt.ThoiGianDuKien,
                        CapDo = lt.CapDo,
                        LoaiLoTrinh = lt.LoaiLoTrinh,
                        MucTieuDiem = lt.MucTieuDiem ?? 0,
                        TongSoBai = (int)lt.TongSoBai,
                        NgayTao = lt.NgayTao ?? DateTime.MinValue
                    })
                    .OrderBy(lt => lt.CapDo)
                    .ThenBy(lt => lt.NgayTao)
                    .ToListAsync();

                return Ok(new { message = "Danh sách lộ trình có sẵn", total = loTrinhs.Count, data = loTrinhs });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }
        #endregion

        #region 2. Chi tiết lộ trình
        [HttpGet("co-san/{maLoTrinh}")]
        public async Task<IActionResult> GetLoTrinhCoSanDetail(string maLoTrinh)
        {
            try
            {
                var loTrinh = await _context.LoTrinhCoSans
                    .Where(lt => lt.MaLoTrinh == maLoTrinh)
                    .Select(lt => new LoTrinhCoSanDto
                    {
                        MaLoTrinh = lt.MaLoTrinh,
                        TenLoTrinh = lt.TenLoTrinh,
                        MoTa = lt.MoTa,
                        ThoiGianDuKien = lt.ThoiGianDuKien,
                        CapDo = lt.CapDo,
                        LoaiLoTrinh = lt.LoaiLoTrinh,
                        MucTieuDiem = lt.MucTieuDiem ?? 0,
                        TongSoBai = (int)lt.TongSoBai,
                        NgayTao = lt.NgayTao ?? DateTime.MinValue
                    })
                    .FirstOrDefaultAsync();

                if (loTrinh == null) return NotFound(new { message = "Không tìm thấy lộ trình" });
                return Ok(new { message = "Chi tiết lộ trình", data = loTrinh });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }
        #endregion

        #region 3. Đăng ký lộ trình → Tạo lịch từ bài học thực tế
        [Authorize]
        [HttpPost("dang-ky/{maLoTrinh}")]
        public async Task<IActionResult> DangKyLoTrinh(string maLoTrinh)
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd))
                    return BadRequest(new { message = "Không tìm thấy người dùng" });

                var loTrinh = await _context.LoTrinhCoSans.FirstOrDefaultAsync(lt => lt.MaLoTrinh == maLoTrinh);
                if (loTrinh == null)
                    return NotFound(new { message = "Lộ trình không tồn tại" });

                if (await _context.DangKyLoTrinhs.AnyAsync(dk => dk.MaLoTrinh == maLoTrinh && dk.MaNd == maNd))
                    return BadRequest(new { message = "Đã đăng ký lộ trình này rồi" });

                var dangKy = new DangKyLoTrinh
                {
                    MaLoTrinh = maLoTrinh,
                    MaNd = maNd,
                    NgayDangKy = DateTime.Now,
                    TrangThai = "Đang học"
                };
                _context.DangKyLoTrinhs.Add(dangKy);

                // Tạo lịch học từ bài học thực tế
                await TaoLichHocTheoLoTrinh(maNd, maLoTrinh);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Đăng ký thành công",
                    data = new { maDangKy = dangKy.MaDangKy, tenLoTrinh = loTrinh.TenLoTrinh }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi đăng ký", error = ex.Message });
            }
        }

        private async Task TaoLichHocTheoLoTrinh(string maNd, string maLoTrinh)
        {
            // Lấy tất cả bài học trong lộ trình (đã sắp xếp theo so_thu_tu)
            var danhSachBaiHoc = await _context.BaiHocs
                .Where(b => b.MaLoTrinh == maLoTrinh)
                .OrderBy(b => b.SoThuTu)
                .ToListAsync();

            if (!danhSachBaiHoc.Any()) return;

            var ngayBatDau = DateTime.Today;
            var lichHocs = new List<LichHocTap>();
            int tuanHoc = 1;
            int ngayHocIndex = 0;

            foreach (var baiHoc in danhSachBaiHoc)
            {
                var ngayHoc = ngayBatDau.AddDays(ngayHocIndex);

                // 1. Thêm bài học lý thuyết/video
                lichHocs.Add(new LichHocTap
                {
                    MaLich = $"LICH_{maNd}_{baiHoc.MaBai}_LT",
                    MaNd = maNd,
                    MaLoTrinh = maLoTrinh,
                    MaBai = baiHoc.MaBai,
                    TieuDe = $"Lý thuyết: {baiHoc.TenBai}",
                    MoTa = baiHoc.MoTa,
                    LoaiNoiDung = "Lý thuyết",
                    NgayHoc = DateOnly.FromDateTime(ngayHoc),
                    TrangThai = (ngayHocIndex == 0) ? "Đã mở khóa" : "Chưa mở khóa",
                    DaHoanThanh = false,
                    ThuTuNgay = ((int)ngayHoc.DayOfWeek == 0) ? 7 : (int)ngayHoc.DayOfWeek,
                    TuanHoc = tuanHoc
                });

                // 2. Thêm bài nghe (nếu có)
                var baiNghe = await _context.BaiNghes.FirstOrDefaultAsync(bn => bn.MaBai == baiHoc.MaBai);
                if (baiNghe != null)
                {
                    lichHocs.Add(new LichHocTap
                    {
                        MaLich = $"LICH_{maNd}_{baiHoc.MaBai}_NGHE",
                        MaNd = maNd,
                        MaLoTrinh = maLoTrinh,
                        MaBai = baiHoc.MaBai,
                        TieuDe = $"Bài nghe: {baiNghe.TieuDe}",
                        MoTa = $"Luyện nghe - {baiNghe.DoKho}",
                        LoaiNoiDung = "Nghe",
                        NgayHoc = DateOnly.FromDateTime(ngayHoc),
                        TrangThai = "Chưa mở khóa",
                        DaHoanThanh = false,
                        ThuTuNgay = ((int)ngayHoc.DayOfWeek == 0) ? 7 : (int)ngayHoc.DayOfWeek,
                        TuanHoc = tuanHoc
                    });
                }

                // 3. Thêm bài đọc (nếu có)
                var baiDoc = await _context.BaiDocs.FirstOrDefaultAsync(bd => bd.MaBai == baiHoc.MaBai);
                if (baiDoc != null)
                {
                    lichHocs.Add(new LichHocTap
                    {
                        MaLich = $"LICH_{maNd}_{baiHoc.MaBai}_DOC",
                        MaNd = maNd,
                        MaLoTrinh = maLoTrinh,
                        MaBai = baiHoc.MaBai,
                        TieuDe = $"Bài đọc: {baiDoc.TieuDe}",
                        MoTa = $"Luyện đọc hiểu - {baiDoc.DoKho}",
                        LoaiNoiDung = "Đọc",
                        NgayHoc = DateOnly.FromDateTime(ngayHoc),
                        TrangThai = "Chưa mở khóa",
                        DaHoanThanh = false,
                        ThuTuNgay = ((int)ngayHoc.DayOfWeek == 0) ? 7 : (int)ngayHoc.DayOfWeek,
                        TuanHoc = tuanHoc
                    });
                }

                // Tăng ngày học (mỗi bài học = 1 ngày)
                ngayHocIndex++;

                // Cập nhật tuần học (sau thứ 7)
                if (ngayHoc.DayOfWeek == DayOfWeek.Saturday)
                {
                    tuanHoc++;
                }
            }

            _context.LichHocTaps.AddRange(lichHocs);
        }
        #endregion

        #region 4. Lộ trình của tôi
        [Authorize]
        [HttpGet("cua-toi")]
        public async Task<IActionResult> GetLoTrinhCuaToi()
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd)) return BadRequest(new { message = "Không tìm thấy người dùng" });

                var loTrinhs = await _context.DangKyLoTrinhs
                    .Where(dk => dk.MaNd == maNd)
                    .Join(_context.LoTrinhCoSans,
                        dk => dk.MaLoTrinh,
                        lt => lt.MaLoTrinh,
                        (dk, lt) => new DangKyLoTrinhDto
                        {
                            MaDangKy = dk.MaDangKy,
                            MaLoTrinh = dk.MaLoTrinh,
                            TenLoTrinh = lt.TenLoTrinh,
                            MoTa = lt.MoTa,
                            CapDo = lt.CapDo,
                            TongSoBai = (int)lt.TongSoBai,
                            NgayDangKy = dk.NgayDangKy ?? DateTime.MinValue,
                            TrangThai = dk.TrangThai
                        })
                    .OrderByDescending(x => x.NgayDangKy)
                    .ToListAsync();

                return Ok(new
                {
                    message = loTrinhs.Count == 0 ? "Chưa có lộ trình" : "Lộ trình của bạn",
                    total = loTrinhs.Count,
                    data = loTrinhs
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi", error = ex.Message });
            }
        }
        #endregion

        #region 5. Lịch học của tôi (tất cả lộ trình đã đăng ký)
        [Authorize]
        [HttpGet("lich-hoc")]
        public async Task<IActionResult> GetLichHocCuaToi(
            [FromQuery] string? maLoTrinh = null,
            [FromQuery] int page = 1,
            [FromQuery] int size = 20)
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd))
                    return BadRequest(new { message = "Không tìm thấy người dùng" });

                // Lấy danh sách lộ trình đã đăng ký - lấy dữ liệu thô trước
                var danhSachDangKyRaw = await _context.DangKyLoTrinhs
                    .Where(dk => dk.MaNd == maNd)
                    .Join(_context.LoTrinhCoSans,
                        dk => dk.MaLoTrinh,
                        lt => lt.MaLoTrinh,
                        (dk, lt) => new
                        {
                            dk.MaLoTrinh,
                            lt.TenLoTrinh,
                            dk.NgayDangKy,
                            lt.ThoiGianDuKien // Giữ nguyên kiểu gốc
                        })
                    .ToListAsync();

                // Xử lý chuyển đổi sau khi lấy dữ liệu
                var danhSachDangKy = danhSachDangKyRaw.Select(d => new
                {
                    d.MaLoTrinh,
                    d.TenLoTrinh,
                    NgayDangKy = d.NgayDangKy ?? DateTime.Now,
                    ThoiGianDuKien = ConvertToInt(d.ThoiGianDuKien, 30)
                }).ToList();

                if (!danhSachDangKy.Any())
                    return NotFound(new { message = "Bạn chưa đăng ký lộ trình nào" });

                // Nếu có filter theo mã lộ trình
                if (!string.IsNullOrEmpty(maLoTrinh))
                {
                    danhSachDangKy = danhSachDangKy.Where(d => d.MaLoTrinh == maLoTrinh).ToList();
                    if (!danhSachDangKy.Any())
                        return NotFound(new { message = "Không tìm thấy lộ trình đã đăng ký" });
                }

                // Tính khoảng thời gian tổng hợp
                var ngayBatDau = danhSachDangKy.Min(d => d.NgayDangKy).Date;
                var ngayKetThuc = danhSachDangKy.Max(d => d.NgayDangKy.AddDays(d.ThoiGianDuKien)).Date;

                // Lấy danh sách mã lộ trình để filter
                var maLoTrinhs = danhSachDangKy.Select(d => d.MaLoTrinh).ToList();

                var query = _context.LichHocTaps
                    .Where(l => l.MaNd == maNd && maLoTrinhs.Contains(l.MaLoTrinh));

                var total = await query.CountAsync();

                var lich = await query
                    .OrderBy(l => l.NgayHoc)
                    .ThenBy(l => l.ThuTuNgay)
                    .Skip((page - 1) * size)
                    .Take(size)
                    .Select(l => new LichHocTapDto
                    {
                        MaLich = l.MaLich,
                        MaLoTrinh = l.MaLoTrinh,
                        MaBai = l.MaBai,
                        TieuDe = l.TieuDe,
                        MoTa = l.MoTa,
                        LoaiNoiDung = l.LoaiNoiDung,
                        NgayHoc = l.NgayHoc.ToDateTime(TimeOnly.MinValue),
                        TrangThai = l.TrangThai,
                        DaHoanThanh = l.DaHoanThanh ?? false,
                        ThuTuNgay = l.ThuTuNgay,
                        TuanHoc = l.TuanHoc
                    })
                    .ToListAsync();

                return Ok(new
                {
                    message = "Lịch học của bạn",
                    ngayBatDau = ngayBatDau.ToString("yyyy-MM-dd"),
                    ngayKetThuc = ngayKetThuc.ToString("yyyy-MM-dd"),
                    soLoTrinh = danhSachDangKy.Count,
                    danhSachLoTrinh = danhSachDangKy.Select(d => new
                    {
                        d.MaLoTrinh,
                        d.TenLoTrinh,
                        ngayBatDau = d.NgayDangKy.ToString("yyyy-MM-dd"),
                        ngayKetThuc = d.NgayDangKy.AddDays(d.ThoiGianDuKien).ToString("yyyy-MM-dd")
                    }),
                    total,
                    page,
                    size,
                    totalPages = (int)Math.Ceiling(total / (double)size),
                    data = lich
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy lịch", error = ex.Message });
            }
        }

        // Thêm helper method để chuyển đổi
        private int ConvertToInt(object? value, int defaultValue)
        {
            if (value == null)
                return defaultValue;

            // Nếu đã là int
            if (value is int intValue)
                return intValue;

            // Nếu là string, parse
            if (value is string strValue && int.TryParse(strValue, out var result))
                return result;

            return defaultValue;
        }
        #endregion

        #region 6. Tiến độ lộ trình
        [Authorize]
        [HttpGet("tien-do/{maLoTrinh}")]
        public async Task<IActionResult> GetTienDoLoTrinh(string maLoTrinh)
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd)) return BadRequest(new { message = "Không tìm thấy người dùng" });

                var dangKy = await _context.DangKyLoTrinhs
                    .FirstOrDefaultAsync(dk => dk.MaLoTrinh == maLoTrinh && dk.MaNd == maNd);
                if (dangKy == null) return NotFound(new { message = "Chưa đăng ký" });

                var loTrinh = await _context.LoTrinhCoSans.FirstOrDefaultAsync(lt => lt.MaLoTrinh == maLoTrinh);
                var tongSoBai = loTrinh?.TongSoBai ?? 0;

                var tienDos = await _context.TienDoHocTaps
                    .Where(td => td.MaNd == maNd && _context.LichHocTaps.Any(l => l.MaBai == td.MaBai && l.MaLoTrinh == maLoTrinh))
                    .Select(td => new TienDoHocTapDto
                    {
                        MaTienDo = td.MaTienDo,
                        MaBai = td.MaBai,
                        TrangThai = td.TrangThai,
                        PhanTramHoanThanh = (int)td.PhanTramHoanThanh,
                        ThoiGianHocPhut = (int)td.ThoiGianHocPhut,
                        NgayHoanThanh = td.NgayHoanThanh,
                        NgayCapNhat = td.NgayCapNhat ?? DateTime.MinValue
                    })
                    .ToListAsync();

                var hoanThanh = tienDos.Count(t => t.TrangThai == "Hoàn thành");
                var phanTram = tongSoBai > 0 ? Math.Round((double)hoanThanh / tongSoBai * 100, 2) : 0;

                return Ok(new
                {
                    message = "Tiến độ",
                    data = new
                    {
                        tenLoTrinh = loTrinh?.TenLoTrinh,
                        trangThai = dangKy.TrangThai,
                        tongSoBai,
                        hoanThanh,
                        phanTramHoanThanh = phanTram,
                        danhSach = tienDos
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi", error = ex.Message });
            }
        }
        #endregion

        #region 7. Cập nhật trạng thái lộ trình
        [Authorize]
        [HttpPut("cap-nhat-trang-thai/{maLoTrinh}")]
        public async Task<IActionResult> CapNhatTrangThai(string maLoTrinh, [FromBody] TrangThaiRequest req)
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd)) return BadRequest(new { message = "Không tìm thấy người dùng" });

                var dk = await _context.DangKyLoTrinhs
                    .FirstOrDefaultAsync(d => d.MaLoTrinh == maLoTrinh && d.MaNd == maNd);
                if (dk == null) return NotFound(new { message = "Không tìm thấy" });

                dk.TrangThai = req.TrangThai;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Cập nhật thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi", error = ex.Message });
            }
        }
        #endregion

        #region 8. Hủy đăng ký
        [Authorize]
        [HttpDelete("huy-dang-ky/{maLoTrinh}")]
        public async Task<IActionResult> HuyDangKy(string maLoTrinh)
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd)) return BadRequest(new { message = "Không tìm thấy người dùng" });

                var dk = await _context.DangKyLoTrinhs
                    .FirstOrDefaultAsync(d => d.MaLoTrinh == maLoTrinh && d.MaNd == maNd);
                if (dk == null) return NotFound(new { message = "Không tìm thấy" });

                var lichs = await _context.LichHocTaps.Where(l => l.MaLoTrinh == maLoTrinh && l.MaNd == maNd).ToListAsync();
                var maBais = lichs.Select(l => l.MaBai).ToList();
                var tienDos = await _context.TienDoHocTaps.Where(t => maBais.Contains(t.MaBai) && t.MaNd == maNd).ToListAsync();

                _context.LichHocTaps.RemoveRange(lichs);
                _context.TienDoHocTaps.RemoveRange(tienDos);
                _context.DangKyLoTrinhs.Remove(dk);

                await _context.SaveChangesAsync();
                return Ok(new { message = "Hủy thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi", error = ex.Message });
            }
        }
        #endregion

        #region 9. Cập nhật tiến độ + Mở khóa bài tiếp theo
        [Authorize]
        [HttpPut("cap-nhat-tien-do/{maBai}")]
        public async Task<IActionResult> CapNhatTienDo(string maBai, [FromBody] CapNhatTienDoRequest req)
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd)) return BadRequest(new { message = "Không tìm thấy người dùng" });

                // 1. Cập nhật tiến độ
                var td = await _context.TienDoHocTaps
                    .FirstOrDefaultAsync(t => t.MaBai == maBai && t.MaNd == maNd);

                if (td == null)
                {
                    td = new TienDoHocTap { MaBai = maBai, MaNd = maNd };
                    _context.TienDoHocTaps.Add(td);
                }

                td.PhanTramHoanThanh = Math.Clamp(req.PhanTramHoanThanh, 0, 100);
                td.ThoiGianHocPhut += req.ThoiGianHocPhut;
                td.TrangThai = td.PhanTramHoanThanh >= 100 ? "Hoàn thành" : "Đang học";

                if (td.PhanTramHoanThanh >= 100)
                {
                    td.NgayHoanThanh = DateTime.Now;
                }

                // 2. Cập nhật trạng thái lịch học
                var lichHienTai = await _context.LichHocTaps
                    .FirstOrDefaultAsync(l => l.MaBai == maBai && l.MaNd == maNd);

                if (lichHienTai != null && td.PhanTramHoanThanh >= 100)
                {
                    lichHienTai.DaHoanThanh = true;
                    lichHienTai.NgayHoanThanh = DateTime.Now;

                    // 3. Mở khóa bài tiếp theo trong cùng lộ trình
                    var lichTiepTheo = await _context.LichHocTaps
                        .Where(l => l.MaNd == maNd
                            && l.MaLoTrinh == lichHienTai.MaLoTrinh
                            && l.NgayHoc >= lichHienTai.NgayHoc
                            && l.TrangThai == "Chưa mở khóa")
                        .OrderBy(l => l.NgayHoc)
                        .ThenBy(l => l.ThuTuNgay)
                        .FirstOrDefaultAsync();

                    if (lichTiepTheo != null)
                    {
                        lichTiepTheo.TrangThai = "Đã mở khóa";
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Cập nhật thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi", error = ex.Message });
            }
        }
        #endregion

        #region 10. Tổng quan
        [Authorize]
        [HttpGet("tong-quan")]
        public async Task<IActionResult> GetTongQuan()
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd)) return BadRequest(new { message = "Không tìm thấy người dùng" });

                var result = await _context.Database
                    .SqlQueryRaw<TongQuanTienDoDto>(
                        "SELECT * FROM fn_thong_ke_hoc_tap(@p0)",
                        maNd
                    )
                    .FirstOrDefaultAsync();

                return Ok(new { message = "Tổng quan", data = result ?? new TongQuanTienDoDto() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi", error = ex.Message });
            }
        }
        #endregion

        #region 11. Log hoạt động
        [Authorize]
        [HttpGet("log-hoat-dong")]
        public async Task<IActionResult> GetLog([FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            try
            {
                var maNd = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(maNd)) return BadRequest(new { message = "Không tìm thấy người dùng" });

                var logs = await _context.LogHoatDongs
                    .Where(l => l.MaNd == maNd)
                    .OrderByDescending(l => l.ThoiGian)
                    .Skip((page - 1) * size)
                    .Take(size)
                    .Select(l => new LogHoatDongDto
                    {
                        MaLog = l.MaLog,
                        LoaiHoatDong = l.LoaiHoatDong,
                        MoTa = l.MoTa,
                        ThoiGian = (DateTime)l.ThoiGian,
                        DuLieuCu = l.DuLieuCu,
                        DuLieuMoi = l.DuLieuMoi
                    })
                    .ToListAsync();

                var total = await _context.LogHoatDongs.CountAsync(l => l.MaNd == maNd);
                return Ok(new { message = "Log", total, page, size, data = logs });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi", error = ex.Message });
            }
        }
        #endregion
    }
}