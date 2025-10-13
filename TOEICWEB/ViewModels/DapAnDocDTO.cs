﻿public class DapAnDocDTO
{
    public int MaDapAn { get; set; }                 // ma_dap_an
    public string MaCauHoi { get; set; }             // ma_cau_hoi
    public string NhanDapAn { get; set; }            // nhan_dap_an (A, B, C, D)
    public string NoiDungDapAn { get; set; }         // noi_dung_dap_an
    public int? ThuTuHienThi { get; set; }           // thu_tu_hien_thi
    public bool LaDapAnDung { get; set; }            // la_dap_an_dung
}