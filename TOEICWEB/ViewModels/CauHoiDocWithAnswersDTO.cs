public class CauHoiDocWithAnswersDTO
{
    public string MaCauHoi { get; set; }
    public string NoiDungCauHoi { get; set; }
    public string GiaiThich { get; set; }
    public int Diem { get; set; }
    public int? ThuTuHienThi { get; set; }
    public List<DapAnDocDTO> DapAns { get; set; } = new List<DapAnDocDTO>();
}