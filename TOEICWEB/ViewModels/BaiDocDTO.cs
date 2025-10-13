public class BaiDocDTO
{
    public string MaBaiDoc { get; set; }              // ma_bai_doc
    public string MaBai { get; set; }                // ma_bai
    public string TieuDe { get; set; }               // tieu_de
    public string DoKho { get; set; }                // do_kho (Easy, Medium, Hard)
    public DateTime? NgayTao { get; set; }           // ngay_tao
    public string DuongDanFileTxt { get; set; }      // duong_dan_file_txt
}