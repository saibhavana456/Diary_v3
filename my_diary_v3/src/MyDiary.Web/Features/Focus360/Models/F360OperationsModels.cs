namespace MyDiary.Web.Features.Focus360.Models;

/// <summary>Combined Suspense + Sundry panel model for Focus360.</summary>
public class F360SuspenseSundryModel
{
    public string SuspPrevMonthDt { get; set; } = "—";
    public string SuspPrevDayDt { get; set; } = "—";
    public string SuspTodayDtVal { get; set; } = "—";
    public string SundPrevMonthDt { get; set; } = "—";
    public string SundPrevDayDt { get; set; } = "—";
    public string SundTodayDtVal { get; set; } = "—";

    // Suspense
    public int SuspPrevMonthEntries { get; set; }
    public decimal SuspPrevMonthTotal { get; set; }
    public int SuspPrevDayEntries { get; set; }
    public decimal SuspPrevDayTotal { get; set; }
    public int SuspTodayEntries { get; set; }
    public decimal SuspTodayTotal { get; set; }
    public int SuspPrevDay90Entries { get; set; }
    public decimal SuspToday90Total { get; set; }
    public int SuspPrevDay180Entries { get; set; }
    public decimal SuspToday180Total { get; set; }
    public decimal SuspVariation { get; set; }
    public decimal SuspVariationPct { get; set; }

    // Sundry
    public int SundPrevMonthEntries { get; set; }
    public decimal SundPrevMonthTotal { get; set; }
    public int SundPrevDayEntries { get; set; }
    public decimal SundPrevDayTotal { get; set; }
    public int SundTodayEntries { get; set; }
    public decimal SundTodayTotal { get; set; }
    public int SundPrevDay90Entries { get; set; }
    public decimal SundToday90Total { get; set; }
    public int SundPrevDay180Entries { get; set; }
    public decimal SundToday180Total { get; set; }
    public decimal SundVariation { get; set; }
    public decimal SundVariationPct { get; set; }

    public string PrevMonthDt => SuspPrevMonthDt != "—" ? SuspPrevMonthDt : SundPrevMonthDt;
    public string PrevDayDt => SuspPrevDayDt != "—" ? SuspPrevDayDt : SundPrevDayDt;
    public string TodayDt => SuspTodayDtVal != "—" ? SuspTodayDtVal : SundTodayDtVal;

    // Cash Holding (rendered under left panel per design)
    public decimal CashLimitPrevMonth { get; set; }
    public decimal CashLimitPrevDay { get; set; }
    public decimal CashLimitToday { get; set; }
    public decimal CashActualPrevMonth { get; set; }
    public decimal CashActualPrevDay { get; set; }
    public decimal CashActualToday { get; set; }

    // Variations: PrevDay column = PrevDay vs PrevMonth; Today column = Today vs PrevDay
    public decimal SuspVarToday { get; set; }
    public decimal SuspVarTodayPct { get; set; }
    public decimal SundVarToday { get; set; }
    public decimal SundVarTodayPct { get; set; }

    public void Calculate()
    {
        // Suspense: PrevDay variation (PrevDay - PrevMonth)
        SuspVariation = SuspPrevDayTotal - SuspPrevMonthTotal;
        SuspVariationPct = SuspPrevMonthTotal != 0 ? (SuspVariation / SuspPrevMonthTotal) * 100m : 0m;
        // Suspense: Today variation (Today - PrevDay)
        SuspVarToday = SuspTodayTotal - SuspPrevDayTotal;
        SuspVarTodayPct = SuspPrevDayTotal != 0 ? (SuspVarToday / SuspPrevDayTotal) * 100m : 0m;

        // Sundry: PrevDay variation (PrevDay - PrevMonth)
        SundVariation = SundPrevDayTotal - SundPrevMonthTotal;
        SundVariationPct = SundPrevMonthTotal != 0 ? (SundVariation / SundPrevMonthTotal) * 100m : 0m;
        // Sundry: Today variation (Today - PrevDay)
        SundVarToday = SundTodayTotal - SundPrevDayTotal;
        SundVarTodayPct = SundPrevDayTotal != 0 ? (SundVarToday / SundPrevDayTotal) * 100m : 0m;
    }
}

/// <summary>Defaulting Accounts + UCIC Pendency + Customer Complaints panel model for Focus360.</summary>
public class F360DefaultingPanelModel
{
    public string PrevMonthDt { get; set; } = "—";
    public string PrevDayDt { get; set; } = "—";
    public string TodayDt { get; set; } = "—";

    // Defaulting Accounts
    public decimal DefDormantPrevMonth { get; set; }
    public decimal DefDormantPrevDay { get; set; }
    public decimal DefDormantToday { get; set; }
    public decimal DefInactivePrevMonth { get; set; }
    public decimal DefInactivePrevDay { get; set; }
    public decimal DefInactiveToday { get; set; }
    public decimal DefNoNomPrevMonth { get; set; }
    public decimal DefNoNomPrevDay { get; set; }
    public decimal DefNoNomToday { get; set; }

    // Lien Marked
    public decimal LienCountPrevMonth { get; set; }
    public decimal LienCountPrevDay { get; set; }
    public decimal LienCountToday { get; set; }
    public decimal LienAmtPrevMonth { get; set; }
    public decimal LienAmtPrevDay { get; set; }
    public decimal LienAmtToday { get; set; }

    // DEAF
    public decimal DeafCountPrevMonth { get; set; }
    public decimal DeafCountPrevDay { get; set; }
    public decimal DeafCountToday { get; set; }
    public decimal DeafAmtPrevMonth { get; set; }
    public decimal DeafAmtPrevDay { get; set; }
    public decimal DeafAmtToday { get; set; }

    // UCIC Pendency
    public decimal UcicAadhaarPrevMonth { get; set; }
    public decimal UcicAadhaarPrevDay { get; set; }
    public decimal UcicAadhaarToday { get; set; }
    public decimal UcicPanPrevMonth { get; set; }
    public decimal UcicPanPrevDay { get; set; }
    public decimal UcicPanToday { get; set; }

    // Customer Complaints
    public decimal ComplaintsPrevMonth { get; set; }
    public decimal ComplaintsPrevDay { get; set; }
    public decimal ComplaintsToday { get; set; }
}

/// <summary>Cash Holding panel model for Focus360.</summary>
public class F360CashHoldingModel
{
    public string PrevMonthDt { get; set; } = "—";
    public string PrevDayDt { get; set; } = "—";
    public string TodayDt { get; set; } = "—";

    public decimal CashLimitPrevMonth { get; set; }
    public decimal CashLimitPrevDay { get; set; }
    public decimal CashLimitToday { get; set; }
    public decimal CashActualPrevMonth { get; set; }
    public decimal CashActualPrevDay { get; set; }
    public decimal CashActualToday { get; set; }
}

/// <summary>Customer Complaints panel model for Focus360.</summary>
public class F360CustomerComplaintsModel
{
    public string PrevMonthDt { get; set; } = "—";
    public string PrevDayDt { get; set; } = "—";
    public string TodayDt { get; set; } = "—";

    public decimal ComplaintsPrevMonth { get; set; }
    public decimal ComplaintsPrevDay { get; set; }
    public decimal ComplaintsToday { get; set; }
}
