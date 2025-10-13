using System;
using System.Collections.Generic;

namespace TOEICWEB.Models;

public partial class LichHocTap
{
    public string MaLich { get; set; } = null!;

    public string? MaNd { get; set; }

    public string? MaLoTrinh { get; set; }

    public DateOnly NgayHoc { get; set; }

    public int? ThuTuNgay { get; set; }

    public int? TuanHoc { get; set; }

    public string TieuDe { get; set; } = null!;

    public string? MoTa { get; set; }

    public string? LoaiNoiDung { get; set; }

    public string? MaBai { get; set; }

    public string? TrangThai { get; set; }

    public bool? DaHoanThanh { get; set; }

    public DateTime? NgayHoanThanh { get; set; }

    public virtual BaiHoc? MaBaiNavigation { get; set; }

    public virtual LoTrinhCoSan? MaLoTrinhNavigation { get; set; }

    public virtual NguoiDung? MaNdNavigation { get; set; }
}
