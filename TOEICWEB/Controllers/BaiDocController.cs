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
    public class BaiDocController : ControllerBase
    {
        private readonly SupabaseDbContext _context;

        public BaiDocController(SupabaseDbContext context)
        {
            _context = context;
        }

        // ✅ LẤY DANH SÁCH TẤT CẢ BÀI ĐỌC
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

        // ✅ LẤY CHI TIẾT BÀI ĐỌC (Bao gồm Nội Dung + Câu Hỏi + Đáp Án)
        [HttpGet("{maBaiDoc}")]
        public async Task<IActionResult> GetBaiDocDetail(string maBaiDoc)
        {
            try
            {
                var baiDoc = await _context.BaiDocs
                    .FirstOrDefaultAsync(b => b.MaBaiDoc == maBaiDoc);

                if (baiDoc == null)
                    return NotFound(new { message = "Bài đọc không tồn tại!" });

                // Lấy câu hỏi của bài đọc
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

                // Lấy đáp án cho mỗi câu hỏi
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

        // ✅ NỘP BÀI ĐỌC (Yêu cầu xác thực)
        [Authorize]
        [HttpPost("submit/{maBaiDoc}")]
        public async Task<IActionResult> SubmitBaiDoc(string maBaiDoc, [FromBody] SubmitBaiDocVM model)
        {
            try
            {
                var maNd = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(maNd))
                    return Unauthorized(new { message = "Không tìm thấy mã người dùng trong token!" });

                // 🔍 Kiểm tra bài đọc
                var baiDoc = await _context.BaiDocs
                    .FirstOrDefaultAsync(b => b.MaBaiDoc == maBaiDoc);

                if (baiDoc == null)
                    return NotFound(new { message = "Bài đọc không tồn tại!" });

                // 🔍 Lấy danh sách câu hỏi
                var cauHois = await _context.CauHoiDocs
                    .Where(c => c.MaBaiDoc == maBaiDoc)
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
                    var dapAnDung = await _context.DapAnDocs
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
                    var traLoiEntity = new TOEICWEB.Models.TraLoiHocVienDoc
                    {
                        MaNd = maNd,
                        MaCauHoi = tl.MaCauHoi,
                        MaDapAnChon = tl.MaDapAn,
                        DungSai = dungSai,
                        NgayTao = DateTime.Now
                    };
                    _context.TraLoiHocVienDocs.Add(traLoiEntity);
                }

                await _context.SaveChangesAsync();

                // ✅ Lưu kết quả tổng
                double phanTram = ((double)diem / (tongCauHoi * 1)) * 100.0;
                var ketQua = new TOEICWEB.Models.KetQuaBaiDoc
                {
                    MaBaiDoc = maBaiDoc,
                    MaNd = maNd,
                    Diem = diem,
                    DiemToiDa = tongCauHoi,
                    PhanTram = Convert.ToDecimal(phanTram),
                    ThoiGianLamGiay = model.ThoiGianLamGiay,
                    NgayNop = DateTime.Now
                };

                _context.KetQuaBaiDocs.Add(ketQua);
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
        [Authorize]
        [HttpGet("history/{maBaiDoc}")]
        public async Task<IActionResult> GetBaiDocHistory(string maBaiDoc)
        {
            try
            {
                var maNd = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                var ketQuas = await _context.KetQuaBaiDocs
                    .Where(k => k.MaBaiDoc == maBaiDoc && k.MaNd == maNd)
                    .Select(k => new
                    {
                        k.MaBaiDoc,
                        k.MaNd,
                        diem = k.Diem ?? 0, // tránh null
                        diemToiDa = k.DiemToiDa ?? 1, // tránh chia cho 0
                        phanTram = k.PhanTram,
                        thang = Math.Round(
                            (decimal)((k.Diem ?? 0) / (double)(k.DiemToiDa ?? 1) * 100),
                            2
                        ),
                        thoiGianLamGiay = k.ThoiGianLamGiay,
                        lanLamThu = k.LanLamThu,
                        ngayNop = k.NgayNop
                    })
                    .OrderByDescending(k => k.ngayNop)
                    .ToListAsync();

                return Ok(new
                {
                    message = "Lịch sử bài làm",
                    total = ketQuas.Count,
                    data = ketQuas
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy lịch sử!", error = ex.Message });
            }
        }

    }
}