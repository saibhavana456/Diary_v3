namespace MyDiary.Web.Features.Reports.Models
{
    // Maps to the output of usp_States_List
    public class StateMasterModel
    {
        public string StateNameEng { get; set; }
        public string StateCodeFinacle { get; set; }
    }

    // Maps to the output columns of usp_Dealer_List
    public class DealerModel
    {
        public string Name { get; set; }
        public string Address { get; set; } // Maps to addr3
        public string City { get; set; }
        public string State { get; set; }
        public string Pincode { get; set; } // Maps to pin
        public string MobileNo { get; set; } // Maps to mobno
        public string ContactPerson { get; set; } // Maps to contper
        public string ContactPersonMobile { get; set; } // Maps to contpermob
        public string Email { get; set; }
        public DateTime? LastUpdateDate { get; set; }
    }

    // Wrapper to hold the server-side paginated data and the OUTPUT parameters
    public class PagedDealerResult
    {
        public List<DealerModel> Dealers { get; set; } = new List<DealerModel>();
        public int TotalCount { get; set; }
        public DateTime? AsOnDate { get; set; }
    }
}