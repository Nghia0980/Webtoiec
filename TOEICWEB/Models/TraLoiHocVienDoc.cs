using System;
using System.Collections.Generic;

namespace TOEICWEB.Models;

public partial class TraLoiHocVienDoc
{
    public int MaTraLoi { get; set; }

    public int? MaKetQua { get; set; }

    public string? MaCauHoi { get; set; }

    public int? MaDapAnChon { get; set; }

    public bool? DungSai { get; set; }

    public DateTime? NgayTao { get; set; }

    // 🔹 Thêm mã người dùng (khóa ngoại)
    public string? MaNd { get; set; }

    // 🔹 Điều hướng (navigation properties)
    public virtual CauHoiDoc? MaCauHoiNavigation { get; set; }

    public virtual DapAnDoc? MaDapAnChonNavigation { get; set; }

    public virtual KetQuaBaiDoc? MaKetQuaNavigation { get; set; }

    // 🔹 Liên kết với bảng người dùng
    public virtual NguoiDung? MaNdNavigation { get; set; }
}
