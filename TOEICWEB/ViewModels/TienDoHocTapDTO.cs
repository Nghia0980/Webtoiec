public class TienDoHocTapDTO
{
    public int MaTienDo { get; set; }
    public string MaBai { get; set; }
    public string TrangThai { get; set; }
    public DateTime? NgayHoanThanh { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public int ThoiGianHocPhut { get; set; }
    public int PhanTramHoanThanh { get; set; }
    public string TenBai { get; internal set; }
    public string? MaLoTrinh { get; internal set; }
}