using System;
using System.Collections.Generic;

namespace TOEICWEB.Models;

public partial class TraLoiHocVienNghe
{
    public int MaTraLoi { get; set; }

    public int? MaKetQua { get; set; }

    public string? MaCauHoi { get; set; }

    public int? MaDapAnChon { get; set; }

    public bool? DungSai { get; set; }

    public DateTime? NgayTao { get; set; }

    // 🔹 Thêm mã người dùng (Foreign Key)
    public string? MaNd { get; set; }

    // 🔹 Navigation Properties (liên kết với các bảng khác)
    public virtual CauHoiNghe? MaCauHoiNavigation { get; set; }

    public virtual DapAnNghe? MaDapAnChonNavigation { get; set; }

    public virtual KetQuaBaiNghe? MaKetQuaNavigation { get; set; }

    // 🔹 Liên kết với bảng NguoiDung
    public virtual NguoiDung? MaNdNavigation { get; set; }
}
