using System;

namespace MyDiary.Web.Features.Business.Models
{
    public class MonthlyGoldInspectionEntity
    {
     
            public int Id { get; set; }

            // ✅ Header Fields
            public DateTime? InspectionDate { get; set; }
            public string BranchCode { get; set; } = "";
            public string BranchName { get; set; } = "";

            public int PhysicalPacketsAvailable { get; set; }
            public int OutstandingAccounts { get; set; }
            public int ClosedButUndelivered { get; set; }

            public string? MismatchReason { get; set; }
            public string? OtherObservations { get; set; }

            public string? OfficerName { get; set; }

            public DateTime CreatedOn { get; set; }

            // ✅ Checklist (1 – 30)

            public bool? C1_Compliant { get; set; }
            public string? C1_Observation { get; set; }

            public bool? C2_Compliant { get; set; }
            public string? C2_Observation { get; set; }

            public bool? C3_Compliant { get; set; }
            public string? C3_Observation { get; set; }

            public bool? C4_Compliant { get; set; }
            public string? C4_Observation { get; set; }

            public bool? C5_Compliant { get; set; }
            public string? C5_Observation { get; set; }

            public bool? C6_Compliant { get; set; }
            public string? C6_Observation { get; set; }

            public bool? C7_Compliant { get; set; }
            public string? C7_Observation { get; set; }

            public bool? C8_Compliant { get; set; }
            public string? C8_Observation { get; set; }

            public bool? C9_Compliant { get; set; }
            public string? C9_Observation { get; set; }

            public bool? C10_Compliant { get; set; }
            public string? C10_Observation { get; set; }

            public bool? C11_Compliant { get; set; }
            public string? C11_Observation { get; set; }

            public bool? C12_Compliant { get; set; }
            public string? C12_Observation { get; set; }

            public bool? C13_Compliant { get; set; }
            public string? C13_Observation { get; set; }

            public bool? C14_Compliant { get; set; }
            public string? C14_Observation { get; set; }

            public bool? C15_Compliant { get; set; }
            public string? C15_Observation { get; set; }

            public bool? C16_Compliant { get; set; }
            public string? C16_Observation { get; set; }

            public bool? C17_Compliant { get; set; }
            public string? C17_Observation { get; set; }

            public bool? C18_Compliant { get; set; }
            public string? C18_Observation { get; set; }

            public bool? C19_Compliant { get; set; }
            public string? C19_Observation { get; set; }

            public bool? C20_Compliant { get; set; }
            public string? C20_Observation { get; set; }

            public bool? C21_Compliant { get; set; }
            public string? C21_Observation { get; set; }

            public bool? C22_Compliant { get; set; }
            public string? C22_Observation { get; set; }

            public bool? C23_Compliant { get; set; }
            public string? C23_Observation { get; set; }

            public bool? C24_Compliant { get; set; }
            public string? C24_Observation { get; set; }

            public bool? C25_Compliant { get; set; }
            public string? C25_Observation { get; set; }

            public bool? C26_Compliant { get; set; }
            public string? C26_Observation { get; set; }

            public bool? C27_Compliant { get; set; }
            public string? C27_Observation { get; set; }

            public bool? C28_Compliant { get; set; }
            public string? C28_Observation { get; set; }

            public bool? C29_Compliant { get; set; }
            public string? C29_Observation { get; set; }

            public bool? C30_Compliant { get; set; }
            public string? C30_Observation { get; set; }
        }
    }

