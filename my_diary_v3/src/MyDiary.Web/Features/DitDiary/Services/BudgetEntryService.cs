// MyDiary.Web.Features.DitDiary/Services/BudgetEntryService.cs

using MyDiary.Web.Features.DitDiary.Data;
using MyDiary.Web.Features.DitDiary.Interfaces;
using MyDiary.Web.Features.DitDiary.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using MyDiary.Web.Features.DitDiary.Models.DTO;


namespace MyDiary.Web.Features.DitDiary.Services
{
	public class BudgetEntryService : IBudgetEntryService
	{
		// A scoped DitDbContext is shared by the whole Blazor circuit. If the user clicks
		// a filter while a load is still running, two operations hit the same context and
		// the Oracle provider throws a NullReferenceException, killing the circuit.
		// Each method below opens its own short-lived context from the pooled factory.
		private readonly IDbContextFactory<DitDbContext> _factory;

		public BudgetEntryService(IDbContextFactory<DitDbContext> factory)
		{
			_factory = factory;
		}

		public async Task<List<Department>> GetDepartmentsAsync()
		{
			using var _context = _factory.CreateDbContext();
			return await _context.Departments.AsNoTracking().OrderBy(d => d.DEPT_NAME).ToListAsync();
		}

		public async Task<List<Section>> GetCapitalSectionsByDeptAsync(long? deptId)
		{
			using var _context = _factory.CreateDbContext();
			var q = _context.Sections.AsNoTracking().Where(s => s.STATUS == "Active");
			if (deptId.HasValue) q = q.Where(s => s.DEPT_ID == deptId);
			return await q.OrderBy(s => s.NAME).ToListAsync();
		}

		public async Task<List<BudgetRow>> GetCapitalRowsAsync(int fyId, long? deptId, string? sectionName, string month)
		{
			using var _context = _factory.CreateDbContext();

			var secQ = _context.Sections.AsNoTracking().Where(s => s.STATUS == "Active");
			if (deptId.HasValue) secQ = secQ.Where(s => s.DEPT_ID == deptId);
			if (!string.IsNullOrEmpty(sectionName) && sectionName != "All") secQ = secQ.Where(s => s.NAME == sectionName);
			var sections = await secQ.ToListAsync();
			var secIds = sections.Select(s => s.SECTION_ID).ToList();
			var secNames = sections.Select(s => s.NAME).ToList();

			if (secIds.Count == 0) return new List<BudgetRow>();

			// Link projects by SECTION_ID (rename-safe); fall back to the section NAME for older rows.
			var projects = await _context.ProjectsMaster.AsNoTracking()
				.Where(p => (p.SECTION_ID != null && secIds.Contains(p.SECTION_ID.Value))
						 || (p.SECTION_ID == null && p.IT_OWNER_SEC_NAME != null && secNames.Contains(p.IT_OWNER_SEC_NAME)))
				.ToListAsync();
			var projIds = projects.Select(p => p.ProjectId).ToList();

			if (projIds.Count == 0) return new List<BudgetRow>();

			var allocs = await _context.BudgetAllocations.AsNoTracking()
				.Where(a => a.BudgetType == "CAPITAL" && a.FyId == fyId && a.IsActive == "Y"
							&& a.ProjectId != null && projIds.Contains(a.ProjectId.Value)).ToListAsync();

			var utils = await _context.CapitalUtilizations.AsNoTracking()
				.Where(c => c.FyId == fyId && c.MonthName == month && c.IsActive == "Y"
							&& projIds.Contains(c.ProjectId)).ToListAsync();

			var rows = new List<BudgetRow>();
			foreach (var a in allocs)
			{
				var proj = projects.First(p => p.ProjectId == a.ProjectId!.Value);
				var u = utils.FirstOrDefault(c => c.ProjectId == a.ProjectId!.Value);
				var sec = proj.SECTION_ID != null
					? sections.FirstOrDefault(s => s.SECTION_ID == proj.SECTION_ID.Value)
					: sections.FirstOrDefault(s => s.NAME == proj.IT_OWNER_SEC_NAME);
				rows.Add(new BudgetRow
				{
					SectionName = sec?.NAME ?? proj.IT_OWNER_SEC_NAME ?? "",
					SectionId = sec?.SECTION_ID ?? 0,
					ProjectId = a.ProjectId,
					ProjectName = proj.ProjectName ?? "",
					TotalAllotted = a.TotalAllotted,
					Fresh = a.FreshAllotted,
					Spillover = a.SpilloverAllotted,
					Utilized = u == null ? 0 : u.ActualSpillover + u.ActualFresh,
					HasEntry = u != null,
					SubmittedBy = u?.SubmittedBy
				});
			}
			return rows.OrderBy(r => r.SectionName).ThenBy(r => r.ProjectName).ToList();
		}

        public async Task<List<BudgetRow>> GetRevenueRowsAsync(int fyId, long? deptId, string? sectionName, string month)
        {
            using var _context = _factory.CreateDbContext();

            var secQ = _context.Sections.AsNoTracking().Where(s => s.STATUS == "Active");
            if (deptId.HasValue) secQ = secQ.Where(s => s.DEPT_ID == deptId);
            if (!string.IsNullOrEmpty(sectionName) && sectionName != "All") secQ = secQ.Where(s => s.NAME == sectionName);
            var sections = await secQ.ToListAsync();
            var secIds = sections.Select(s => s.SECTION_ID).ToList();

            if (secIds.Count == 0) return new List<BudgetRow>();

            // Revenue is per PROJECT now, exactly like Capital.
            var projects = await _context.ProjectsMaster.AsNoTracking()
                .Where(p => p.SECTION_ID != null && secIds.Contains(p.SECTION_ID.Value))
                .ToListAsync();
            var projIds = projects.Select(p => p.ProjectId).ToList();

            if (projIds.Count == 0) return new List<BudgetRow>();

            var allocs = await _context.BudgetAllocations.AsNoTracking()
                .Where(a => a.BudgetType == "REVENUE" && a.FyId == fyId && a.IsActive == "Y"
                            && a.ProjectId != null && projIds.Contains(a.ProjectId.Value)).ToListAsync();

            var utils = await _context.RevenueUtilizations.AsNoTracking()
                .Where(r => r.FyId == fyId && r.MonthName == month && r.IsActive == "Y"
                            && r.ProjectId != null && projIds.Contains(r.ProjectId.Value)).ToListAsync();

            var rows = new List<BudgetRow>();
            foreach (var a in allocs)
            {
                var proj = projects.First(p => p.ProjectId == a.ProjectId!.Value);
                var sec = sections.FirstOrDefault(s => s.SECTION_ID == proj.SECTION_ID);
                var mine = utils.Where(u => u.ProjectId == a.ProjectId!.Value).ToList();

                rows.Add(new BudgetRow
                {
                    SectionName = sec?.NAME ?? "",
                    SectionId = sec?.SECTION_ID ?? 0,
                    ProjectId = a.ProjectId,
                    ProjectName = proj.ProjectName ?? "",
                    TotalAllotted = a.TotalAllotted,
                    Fresh = a.FreshAllotted,
                    Spillover = a.SpilloverAllotted,
                    Utilized = mine.Sum(u => u.Amount),
                    HasEntry = mine.Count > 0,
                    SubmittedBy = mine.FirstOrDefault()?.SubmittedBy
                });
            }
            return rows.OrderBy(r => r.SectionName).ThenBy(r => r.ProjectName).ToList();
        }

        public async Task<List<FinancialYear>> GetFinancialYearsAsync()
		{
			using var _context = _factory.CreateDbContext();
			return await _context.FinancialYears.AsNoTracking()
				.Where(f => f.IsActive == "Y").OrderBy(f => f.FyId).ToListAsync();
		}

		public async Task<FinancialYear?> GetCurrentFyAsync()
		{
			using var _context = _factory.CreateDbContext();
			return await _context.FinancialYears.AsNoTracking().FirstOrDefaultAsync(f => f.IsCurrent == "Y");
		}

		public async Task<List<ExpenditureHead>> GetHeadsAsync()
		{
			using var _context = _factory.CreateDbContext();
			return await _context.ExpenditureHeads.AsNoTracking()
				.Where(h => h.IsActive == "Y").OrderBy(h => h.HeadId).ToListAsync();
		}

		public async Task<List<Section>> GetSectionsAsync(string appliesTo)
		{
			using var _context = _factory.CreateDbContext();
			return await _context.Sections.AsNoTracking()
				.Where(s => s.STATUS == "Active")
				.OrderBy(s => s.NAME).ToListAsync();
		}

		// DIT Diary maps a project to its section via PROJECTS_MASTER.IT_OWNER_SEC_NAME (text)
		// Link by SECTION_ID (rename-safe); fall back to the stored name for old projects.
		public async Task<List<ProjectsMaster>> GetProjectsForSectionAsync(string? sectionName)
		{
			using var _context = _factory.CreateDbContext();

			var q = _context.ProjectsMaster.AsNoTracking();
			if (!string.IsNullOrWhiteSpace(sectionName) && sectionName != "All")
			{
				var sec = await _context.Sections.AsNoTracking()
					.FirstOrDefaultAsync(s => s.NAME == sectionName);
				long? secId = sec?.SECTION_ID;
				q = q.Where(p => (secId != null && p.SECTION_ID == secId)
							  || (p.SECTION_ID == null && p.IT_OWNER_SEC_NAME == sectionName));
			}
			return await q.OrderBy(p => p.ProjectName).ToListAsync();
		}

		public async Task<BudgetAllocation?> GetCapitalAllocationAsync(int projectId, int fyId)
		{
			using var _context = _factory.CreateDbContext();
			return await _context.BudgetAllocations.AsNoTracking().FirstOrDefaultAsync(a =>
				a.BudgetType == "CAPITAL" && a.ProjectId == projectId && a.FyId == fyId && a.IsActive == "Y");
		}

        public async Task<BudgetAllocation?> GetRevenueAllocationAsync(int projectId, int fyId)
        {
            using var _context = _factory.CreateDbContext();
            return await _context.BudgetAllocations.AsNoTracking().FirstOrDefaultAsync(a =>
                a.BudgetType == "REVENUE" && a.ProjectId == projectId && a.FyId == fyId && a.IsActive == "Y");
        }

        public async Task<decimal> GetCapitalCumulativeAsync(int projectId, int fyId)
		{
			using var _context = _factory.CreateDbContext();
			return await _context.CapitalUtilizations
				.Where(c => c.ProjectId == projectId && c.FyId == fyId && c.IsActive == "Y")
				.SumAsync(c => (decimal?)(c.ActualSpillover + c.ActualFresh)) ?? 0;
		}

        public async Task<decimal> GetRevenueCumulativeAsync(int projectId, int fyId)
        {
            using var _context = _factory.CreateDbContext();
            return await _context.RevenueUtilizations
                .Where(r => r.ProjectId == projectId && r.FyId == fyId && r.IsActive == "Y")
                .SumAsync(r => (decimal?)r.Amount) ?? 0;
        }

        // added this method for (B1)
        public async Task<decimal> GetCapitalFyUtilizedAsync(int fyId)
		{
			using var _context = _factory.CreateDbContext();
			return await _context.CapitalUtilizations
				.Where(c => c.FyId == fyId && c.IsActive == "Y")
				.SumAsync(c => (decimal?)(c.ActualSpillover + c.ActualFresh)) ?? 0;
		}

		// added this method for (B1)
		public async Task<decimal> GetRevenueFyUtilizedAsync(int fyId)
		{
			using var _context = _factory.CreateDbContext();
			return await _context.RevenueUtilizations
				.Where(r => r.FyId == fyId && r.IsActive == "Y")
				.SumAsync(r => (decimal?)r.Amount) ?? 0;
		}

		public async Task<CapitalUtilization?> GetCapitalEntryAsync(int projectId, int fyId, string month)
		{
			using var _context = _factory.CreateDbContext();
			return await _context.CapitalUtilizations.AsNoTracking().FirstOrDefaultAsync(c =>
				c.ProjectId == projectId && c.FyId == fyId && c.MonthName == month && c.IsActive == "Y");
		}

        public async Task<List<RevenueUtilization>> GetRevenueEntriesAsync(int projectId, int fyId, string month)
        {
            using var _context = _factory.CreateDbContext();
            return await _context.RevenueUtilizations.AsNoTracking().Where(r =>
                r.ProjectId == projectId && r.FyId == fyId && r.MonthName == month && r.IsActive == "Y").ToListAsync();
        }

        public async Task<string?> SubmitCapitalAsync(CapitalUtilization entry, string fyLabel, string sectionName, BudgetAllocation alloc)
		{
			using var _context = _factory.CreateDbContext();

			var dupC = await _context.CapitalUtilizations.AsNoTracking().FirstOrDefaultAsync(c =>
				c.ProjectId == entry.ProjectId && c.FyId == entry.FyId &&
				c.MonthName == entry.MonthName && c.IsActive == "Y");
			if (dupC != null)
				return $"This month is already submitted by {dupC.SubmittedBy} on {dupC.SubmissionDate:dd-MM-yyyy}. Re-submission is not allowed.";

			// CAPITAL RULE (BRD): cumulative must NOT exceed allotted -> BLOCK
			var cumulative = await _context.CapitalUtilizations
				.Where(c => c.ProjectId == entry.ProjectId && c.FyId == entry.FyId && c.IsActive == "Y")
				.SumAsync(c => (decimal?)(c.ActualSpillover + c.ActualFresh)) ?? 0;
			var proposed = cumulative + entry.ActualSpillover + entry.ActualFresh;
			if (proposed > alloc.TotalAllotted)
			{
				_context.OverbudgetLogs.Add(new OverbudgetLog
				{
					BudgetType = "CAPITAL",
					ProjectId = entry.ProjectId,
					FyId = entry.FyId,
					MonthName = entry.MonthName,
					AttemptedAmount = entry.ActualSpillover + entry.ActualFresh,
					ExceededBy = proposed - alloc.TotalAllotted,
					SubmittedBy = entry.SubmittedBy
				});
				await _context.SaveChangesAsync();
				return "Entered budget utilization exceeds the allocated budget. Please enter a valid amount within the approved budget limit.";
			}

			_context.CapitalUtilizations.Add(entry);

			// Mirror a summary row so the existing list + dashboard show the entry
			var projectName = (await _context.ProjectsMaster.AsNoTracking()
				.FirstOrDefaultAsync(p => p.ProjectId == entry.ProjectId))?.ProjectName ?? "";
			_context.BudgetUtilizations.Add(new BudgetUtilization
			{
				FINANCIAL_YEAR = fyLabel,
				MONTH = ToTitle(entry.MonthName),          // their table stores 'July' style
				VERTICAL = "DIT",
				PROJECT_NAME = projectName,
				FRESH_BUDGET = alloc.FreshAllotted,
				SPILL_OVER_BUDGET = alloc.SpilloverAllotted,
				TOTAL_BUDGET_ALLOCATED = alloc.TotalAllotted,
				ACTUAL_BUDGET = alloc.TotalAllotted,
				BUDGET_UTILIZATION_VAL = entry.ActualSpillover + entry.ActualFresh,
				BUDGET_TYPE = "Capital",
				SECTION = sectionName
			});

			await _context.SaveChangesAsync();
			return null;
		}

		public async Task SubmitRevenueAsync(List<RevenueUtilization> rows, string fyLabel, string sectionName, BudgetAllocation alloc, bool overBudget, decimal exceededBy)
		{
			using var _context = _factory.CreateDbContext();

			var first = rows.FirstOrDefault();
			if (first != null)
			{
                var dupR = await _context.RevenueUtilizations.AsNoTracking().FirstOrDefaultAsync(r =>
    r.ProjectId == first.ProjectId && r.FyId == first.FyId &&
    r.MonthName == first.MonthName && r.IsActive == "Y");
                if (dupR != null)
					throw new InvalidOperationException(
						$"This month is already submitted by {dupR.SubmittedBy} on {dupR.SubmissionDate:dd-MM-yyyy}. Re-submission is not allowed.");
			}
			// REVENUE RULE (client): over-budget = warning only, still saves
			if (overBudget)
			{
				var firsts = rows.First();
				_context.OverbudgetLogs.Add(new OverbudgetLog
				{
					BudgetType = "REVENUE",
					SectionId = firsts.SectionId,
					FyId = firsts.FyId,
					MonthName = firsts.MonthName,
					AttemptedAmount = rows.Sum(r => r.Amount),
					ExceededBy = exceededBy,
					SubmittedBy = firsts.SubmittedBy
				});
			}

			foreach (var r in rows) _context.RevenueUtilizations.Add(r);

			_context.BudgetUtilizations.Add(new BudgetUtilization
			{
				FINANCIAL_YEAR = fyLabel,
				MONTH = ToTitle(rows.First().MonthName),
				VERTICAL = "DIT",
				PROJECT_NAME = null,
				FRESH_BUDGET = null,
				SPILL_OVER_BUDGET = null,
				TOTAL_BUDGET_ALLOCATED = alloc.TotalAllotted,
				ACTUAL_BUDGET = alloc.TotalAllotted,
				BUDGET_UTILIZATION_VAL = rows.Sum(r => r.Amount),
				BUDGET_TYPE = "Revenue",
				SECTION = sectionName
			});

			await _context.SaveChangesAsync();
		}

		// Returns a message if this project/section+month is already submitted, else null.
		public async Task<string?> GetSubmissionOwnerAsync(string view, int? projectId, long? sectionId, int fyId, string month)
		{
			using var _context = _factory.CreateDbContext();

			if (view == "Capital" && projectId.HasValue)
			{
				var e = await _context.CapitalUtilizations.AsNoTracking().FirstOrDefaultAsync(c =>
					c.ProjectId == projectId.Value && c.FyId == fyId && c.MonthName == month && c.IsActive == "Y");
				return e == null ? null
					: $"Already submitted for {month} by {e.SubmittedBy} on {e.SubmissionDate:dd-MM-yyyy}.";
			}
			if (view == "Revenue" && sectionId.HasValue)
			{
				var e = await _context.RevenueUtilizations.AsNoTracking().FirstOrDefaultAsync(r =>
					r.SectionId == sectionId.Value && r.FyId == fyId && r.MonthName == month && r.IsActive == "Y");
				return e == null ? null
					: $"Already submitted for {month} by {e.SubmittedBy} on {e.SubmissionDate:dd-MM-yyyy}.";
			}
			return null;
		}

		private static readonly string[] FyMonths = { "APRIL", "MAY", "JUNE", "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER", "JANUARY", "FEBRUARY", "MARCH" };

		public async Task<BudgetDashboardData> GetDashboardAsync(string view, int fyId, long? deptId,
			string? sectionName, string month, List<long>? scopeSectionIds)
		{
			using var _context = _factory.CreateDbContext();

			var d = new BudgetDashboardData();
			int idx = Array.IndexOf(FyMonths, month);
			if (idx < 0) idx = 0;
			var upto = FyMonths.Take(idx + 1).ToList();

			var secQ = _context.Sections.AsNoTracking()
				.Where(s => s.STATUS == "Active");
			
			if (deptId.HasValue) secQ = secQ.Where(s => s.DEPT_ID == deptId);
			if (!string.IsNullOrEmpty(sectionName) && sectionName != "All") secQ = secQ.Where(s => s.NAME == sectionName);
			var sections = await secQ.ToListAsync();
			if (scopeSectionIds != null) sections = sections.Where(s => scopeSectionIds.Contains(s.SECTION_ID)).ToList();

			var secIds = sections.Select(s => s.SECTION_ID).ToList();
			var secNames = sections.Select(s => s.NAME).ToList();

			if (secIds.Count == 0) return d;

			if (view == "Capital")
			{
				var projects = await _context.ProjectsMaster.AsNoTracking()
					.Where(p => (p.SECTION_ID != null && secIds.Contains(p.SECTION_ID.Value))
							 || (p.SECTION_ID == null && p.IT_OWNER_SEC_NAME != null && secNames.Contains(p.IT_OWNER_SEC_NAME)))
					.Select(p => p.ProjectId).ToListAsync();

				if (projects.Count == 0) return d;

				var allocs = await _context.BudgetAllocations.AsNoTracking()
					.Where(a => a.BudgetType == "CAPITAL" && a.FyId == fyId && a.IsActive == "Y"
								&& a.ProjectId != null && projects.Contains(a.ProjectId.Value)).ToListAsync();
				d.SpilloverAllocated = allocs.Sum(a => a.SpilloverAllotted);
				d.FreshAllocated = allocs.Sum(a => a.FreshAllotted);
				d.TotalAllocated = allocs.Sum(a => a.TotalAllotted);

				var utils = await _context.CapitalUtilizations.AsNoTracking()
					.Where(c => c.FyId == fyId && c.IsActive == "Y" && projects.Contains(c.ProjectId)).ToListAsync();

				var till = utils.Where(u => upto.Contains(u.MonthName ?? "")).ToList();
				d.UtilizedSpilloverTill = till.Sum(u => u.ActualSpillover);
				d.UtilizedFreshTill = till.Sum(u => u.ActualFresh);
				d.UtilizedTill = d.UtilizedSpilloverTill + d.UtilizedFreshTill;

				var cur = utils.Where(u => u.MonthName == month).ToList();
				d.ThisMonth = cur.Sum(u => u.ActualSpillover + u.ActualFresh);
				d.ProjectedSpilloverNext = cur.Sum(u => u.EstSpilloverNext);
				d.ProjectedFreshNext = cur.Sum(u => u.EstFreshNext);
				d.ProjectedNext = d.ProjectedSpilloverNext + d.ProjectedFreshNext;
			}
            else
            {
                var revProjects = await _context.ProjectsMaster.AsNoTracking()
                    .Where(p => p.SECTION_ID != null && secIds.Contains(p.SECTION_ID.Value))
                    .Select(p => p.ProjectId).ToListAsync();

                if (revProjects.Count == 0) return d;

                var allocs = await _context.BudgetAllocations.AsNoTracking()
                    .Where(a => a.BudgetType == "REVENUE" && a.FyId == fyId && a.IsActive == "Y"
                                && a.ProjectId != null && revProjects.Contains(a.ProjectId.Value)).ToListAsync();
                d.SpilloverAllocated = allocs.Sum(a => a.SpilloverAllotted);
                d.FreshAllocated = allocs.Sum(a => a.FreshAllotted);
                d.TotalAllocated = allocs.Sum(a => a.TotalAllotted);

                var utils = await _context.RevenueUtilizations.AsNoTracking()
                    .Where(r => r.FyId == fyId && r.IsActive == "Y"
                                && r.ProjectId != null && revProjects.Contains(r.ProjectId.Value)).ToListAsync();

                d.UtilizedTill = utils.Where(u => upto.Contains(u.MonthName ?? "")).Sum(u => u.Amount);
                d.UtilizedFreshTill = d.UtilizedTill;
                d.ThisMonth = utils.Where(u => u.MonthName == month).Sum(u => u.Amount);
            }
            return d;
		}


        /// <summary>Has this project's month already been submitted?</summary>
        public async Task<bool> IsMonthSubmittedAsync(string view, int projectId, int fyId, string month)
        {
            using var _context = _factory.CreateDbContext();

            if (view == "Capital")
                return await _context.CapitalUtilizations.AsNoTracking()
                    .CountAsync(c => c.ProjectId == projectId && c.FyId == fyId
                                     && c.MonthName == month && c.IsActive == "Y") > 0;

            return await _context.RevenueUtilizations.AsNoTracking()
                .CountAsync(r => r.ProjectId == projectId && r.FyId == fyId
                                 && r.MonthName == month && r.IsActive == "Y") > 0;
        }

        /// <summary>Financial-year index (APRIL = 0) of the month the project started.
        /// The previous-month rule never looks earlier than this.</summary>
        public async Task<int> GetProjectStartMonthIndexAsync(int projectId)
        {
            using var _context = _factory.CreateDbContext();

            var p = await _context.ProjectsMaster.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProjectId == projectId);

            var start = p?.ActualStartDate ?? p?.ProjectStartDate;
            if (start == null) return 0;   // no date recorded -> no restriction

            int idx = Array.IndexOf(FyMonths, start.Value.ToString("MMMM").ToUpperInvariant());
            return idx < 0 ? 0 : idx;
        }

        private static string ToTitle(string upperMonth) =>
			CultureInfo.InvariantCulture.TextInfo.ToTitleCase(upperMonth.ToLowerInvariant());
	}
}