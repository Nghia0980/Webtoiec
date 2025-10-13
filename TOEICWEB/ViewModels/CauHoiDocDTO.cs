public class CauHoiDocDTO
{
    public string MaCauHoi { get; set; }             // ma_cau_hoi
    public string NoiDungCauHoi { get; set; }        // noi_dung_cau_hoi
    public string GiaiThich { get; set; }            // giai_thich
    public int Diem { get; set; }                    // diem (default 1)
    public int? ThuTuHienThi { get; set; }           // thu_tu_hien_thi
}