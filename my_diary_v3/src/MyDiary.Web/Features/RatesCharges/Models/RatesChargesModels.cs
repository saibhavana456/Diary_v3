namespace MyDiary.Web.Features.RatesCharges.Models
{
    public class DepositCategory
    {
        public int CategoryId { get; set; }
        public string CategoryCode { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string CategoryName_HI { get; set; } = "";   // ✅ NEW
        public int SortOrder { get; set; }

        /// <summary>
        /// "STRUCTURED" = render via DepositRate / DepositRateMultiCurrency (existing fixed-column path).
        /// "HTML"       = render via CategoryContentBlock (free-form table + notes stored as HTML).
        /// BULKDB -> BULKdepositRATES database
        /// Lets each category be migrated to the HTML path independently, without touching code.
        /// </summary>
        public string ContentMode { get; set; } = "STRUCTURED";   // ✅ NEW
    }

    public class DepositRate
    {
        public string Period { get; set; } = "";
        public string Period_HI { get; set; } = "";         // ✅ NEW
        public string ColumnHeader { get; set; } = "";
        public string Rate { get; set; } = "";
        public DateTime EffectiveDate { get; set; }
        public int SortOrder { get; set; }
    }

    public class DepositRateMultiCurrency
    {
        public string SubSection { get; set; } = "";
        public string SubSection_HI { get; set; } = "";     // ✅ NEW

        public string Period { get; set; } = "";
        public string Period_HI { get; set; } = "";         // ✅ NEW

        public string Currency { get; set; } = "";
        public string Rate { get; set; } = "";
        public DateTime EffectiveDate { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Groups multi-currency rates by SubSection for rendering pivot tables
    /// </summary>

    public class MultiCurrencySection
    {
        public string SubSection { get; set; } = "";
        public string SubSection_HI { get; set; } = "";     // ✅ NEW
        public List<string> Currencies { get; set; } = new();
        public List<MultiCurrencyRow> Rows { get; set; } = new();
        public DateTime EffectiveDate { get; set; }
    }



    public class MultiCurrencyRow
    {
        public string Period { get; set; } = "";
        public string Period_HI { get; set; } = "";         // ✅ NEW
        public Dictionary<string, string> RateByCurrency { get; set; } = new();
    }

    /// <summary>
    /// ✅ NEW — a single free-form block of content (a rate table + its footnotes, as HTML)
    /// belonging to one deposit category. A category can have one block (e.g. NRE Deposit —
    /// one table) or several (e.g. FCNR — one block per currency-slab subsection), each
    /// rendered in SortOrder. This is what lets the underlying table shape (columns, rowspans,
    /// footnote bullets) change without any Blazor/code change — only the HTML in the DB changes.
    /// </summary>
    public class CategoryContentBlock
    {
        public int BlockId { get; set; }
        public int CategoryId { get; set; }
        public DateTime EffectiveDate { get; set; }

        /// <summary>Sanitized HTML fragment: a &lt;table&gt; plus optional trailing &lt;p&gt;/&lt;ul&gt; notes. No inline styles/classes — styling comes from the portal's own CSS.</summary>
        public string HtmlContent { get; set; } = "";

        /// <summary>Optional Hindi version of the same fragment. Falls back to HtmlContent when empty.</summary>
        public string HtmlContent_HI { get; set; } = "";

        public int SortOrder { get; set; }
    }

    // For Bulk deposit Rates header text and footer
    public class BulkUiMessage
    {
        public string Type { get; set; } = "";
        public string Text { get; set; } = "";
    }

    public class BulkDepositUiConfig
    {
        public string HeaderTemplate { get; set; } = "";
        public List<BulkUiMessage> HeaderMessages { get; set; } = new();
        public List<BulkUiMessage> FooterMessages { get; set; } = new();
    }

}
