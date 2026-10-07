using ClosedXML.Excel;
using MyDiary.Web.Features.DitDiary.Data;
using MyDiary.Web.Features.DitDiary.Helpers;
using MyDiary.Web.Features.DitDiary.Interfaces;
using MyDiary.Web.Features.DitDiary.Models.DTO;
using MyDiary.Web.Features.DitDiary.Models.Entities;
using Microsoft.EntityFrameworkCore;


namespace MyDiary.Web.Features.DitDiary.Services
{
    public class ProjectMasterService : IProjectMasterService
    {
        private readonly DitDbContext _context;
        private readonly ILogger<ProjectMasterService> _logger;

        public ProjectMasterService(DitDbContext context, ILogger<ProjectMasterService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Dashboard and List Methods

        public async Task<List<ProjectMaster>> GetProjectMastersAsync()
        {
            try
            {
                return await _context.ProjectMasters.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Project Master records.");
                return new List<ProjectMaster>();
            }
        }

        public ProjectDashboardStats GetDashboardStats(List<ProjectMaster> projects)
        {
            var stats = new ProjectDashboardStats();
            if (projects == null || !projects.Any()) return stats;

            stats.TotalProjects = projects.Count;
            stats.TotalCost = (decimal)projects.Sum(x =>
            {
                if (double.TryParse(x.COST, out var v)) return v;
                return 0;
            });

            var statusGroups = projects
                .GroupBy(x => string.IsNullOrWhiteSpace(x.STATUS) ? "Undefined" : x.STATUS.Trim())
                .Select(g => new { Status = g.Key, Count = (double)g.Count() })
                .ToList();

            stats.PieLabels = statusGroups.Select(x => x.Status).ToArray();
            stats.PieData = statusGroups.Select(x => x.Count).ToArray();

            var vendorFinancials = projects
                .Where(x => !string.IsNullOrWhiteSpace(x.VENDORNAME))
                .GroupBy(x => x.VENDORNAME!.Trim())
                .Select(g => new {
                    Vendor = g.Key,
                    TotalCost = g.Sum(x => { double.TryParse(x.COST, out var v); return v; })
                })
                .OrderByDescending(x => x.TotalCost)
                .Take(5)
                .ToList();

            stats.VendorLabels = vendorFinancials.Select(x => x.Vendor).ToArray();
            stats.VendorData = vendorFinancials.Select(x => x.TotalCost).ToArray();

            var timelineGroups = projects
                .Where(x => x.PROJECTSTARTDATE.HasValue)
                .GroupBy(x => x.PROJECTSTARTDATE!.Value.Year.ToString())
                .Select(g => new { Year = g.Key, Count = (double)g.Count() })
                .OrderBy(x => x.Year)
                .ToList();

            stats.TimelineLabels = timelineGroups.Select(x => x.Year).ToArray();
            stats.TimelineData = timelineGroups.Select(x => x.Count).ToArray();

            stats.TotalVendorsCount = projects.Select(x => x.VENDORNAME).Distinct().Count();

            var timeline = GetTimelineStats(projects);
            stats.OnTrackCount = timeline.OnTrack;
            stats.DelayedCount = timeline.Delayed;
            stats.DroppedCount = timeline.Dropped;
            stats.TimelineStatusData = new double[] { timeline.OnTrack, timeline.Delayed, timeline.Dropped };

            return stats;
        }

        #endregion

        #region Tab-by-Tab Save Methods ("Save & Next")

        public async Task<ProjectsMaster?> SaveProjectGeneralAsync(ProjectsMaster proj)
        {
            try
            {
                if (proj.ProjectId == 0)
                {
                    _context.ProjectsMaster.Add(proj);
                }
                else
                {
                    // FIX: Prevent Tracking Collision
                    var tracked = _context.ProjectsMaster.Local.FirstOrDefault(e => e.ProjectId == proj.ProjectId);
                    if (tracked != null) _context.Entry(tracked).State = EntityState.Detached;

                    _context.ProjectsMaster.Update(proj);
                }

                await _context.SaveChangesAsync();
                await _context.Entry(proj).ReloadAsync(); // Crucial to capture Oracle-generated ProjectCode
                return proj;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving ProjectsMaster (General Info)");
                return null;
            }
        }

        public async Task<bool> SaveMilestoneAsync(ProjectMilestone m)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(m.ProjectCode) || string.IsNullOrWhiteSpace(m.MilestoneName))
                    return false;

                if (m.MilestoneId == 0m)
                {
                    _context.ProjectMilestones.Add(m);
                }
                else
                {
                    var tracked = _context.ProjectMilestones.Local.FirstOrDefault(e => e.MilestoneId == m.MilestoneId);
                    if (tracked != null) _context.Entry(tracked).State = EntityState.Detached;

                    _context.ProjectMilestones.Update(m);
                }

                var success = await _context.SaveChangesAsync() > 0;
                if (success) await _context.Entry(m).ReloadAsync();

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database Error saving Milestone: {Message}", ex.Message);
                return false;
            }
        }

        public async Task<bool> SaveIssueAsync(ProjectIssue i)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(i.ProjectCode)) return false;

                if (i.IssueId == 0m)
                {
                    _context.ProjectIssues.Add(i);
                }
                else
                {
                    // FIX: Prevent Tracking Collision
                    var tracked = _context.ProjectIssues.Local.FirstOrDefault(e => e.IssueId == i.IssueId);
                    if (tracked != null) _context.Entry(tracked).State = EntityState.Detached;

                    _context.ProjectIssues.Update(i);
                }

                var success = await _context.SaveChangesAsync() > 0;
                if (success) await _context.Entry(i).ReloadAsync();

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Issue: {Inner}", ex.InnerException?.Message);
                return false;
            }
        }

        public async Task<bool> SaveResourceAsync(ProjectResource r)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(r.ProjectCode)) return false;
                if (!string.IsNullOrEmpty(r.ResourceType)) r.ResourceType = r.ResourceType.ToUpper();

                if (r.ResourceId == 0m)
                {
                    _context.ProjectResources.Add(r);
                }
                else
                {
                    // FIX: Prevent Tracking Collision
                    var tracked = _context.ProjectResources.Local.FirstOrDefault(e => e.ResourceId == r.ResourceId);
                    if (tracked != null) _context.Entry(tracked).State = EntityState.Detached;

                    _context.ProjectResources.Update(r);
                }

                var success = await _context.SaveChangesAsync() > 0;
                if (success) await _context.Entry(r).ReloadAsync();

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Resource: {Inner}", ex.InnerException?.Message);
                return false;
            }
        }

        public async Task<bool> SaveDocumentAsync(Document d)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(d.ProjectCode) || string.IsNullOrWhiteSpace(d.DocName))
                    return false;

                if (d.DocId == 0m)
                {
                    _context.Documents.Add(d);
                }
                else
                {
                    // FIX: Prevent Tracking Collision
                    var tracked = _context.Documents.Local.FirstOrDefault(e => e.DocId == d.DocId);
                    if (tracked != null) _context.Entry(tracked).State = EntityState.Detached;

                    _context.Documents.Update(d);
                }

                var success = await _context.SaveChangesAsync() > 0;

                // FIX: ReloadAsync was missing here! This ensures DocId and DocCode update in the UI.
                if (success) await _context.Entry(d).ReloadAsync();

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Document: {Inner}", ex.InnerException?.Message);
                return false;
            }
        }
        #endregion


        public async Task<List<Document>> GetDocumentListByProjectAsync(string projectCode)
        {
            try
            {
                return await _context.Documents
                                     .Where(d => d.ProjectCode == projectCode)
                                     .OrderBy(d => d.DocName)
                                     .ToListAsync();
            }
            catch (Exception)
            {
                return new List<Document>();
            }
        }


        // Ensure these methods are in your ProjectMasterService.cs
        public async Task<List<ProjectMilestone>> GetMilestonesByProjectAsync(string projectCode)
        {
            return await _context.ProjectMilestones
                                 .Where(x => x.ProjectCode == projectCode)
                                 .ToListAsync();
        }

        public async Task<List<ProjectIssue>> GetIssuesByProjectAsync(string projectCode)
        {
            return await _context.ProjectIssues
                                 .Where(x => x.ProjectCode == projectCode)
                                 .ToListAsync();
        }

        public async Task<List<ProjectResource>> GetResourcesByProjectAsync(string projectCode)
        {
            return await _context.ProjectResources
                                 .Where(x => x.ProjectCode == projectCode)
                                 .ToListAsync();
        }

        // Implementation for Documents
        public async Task<List<Document>> GetDocumentsByProjectAsync(string projectCode)
        {
            return await _context.Documents
                                 .Where(x => x.ProjectCode == projectCode)
                                 .ToListAsync();
        }


        // Update these in ProjectMasterService.cs
        // Services/ProjectMasterService.cs
        // Services/ProjectMasterService.cs

        // --- LIST FETCHING METHODS (Match the IProjectMasterService contract) ---

        public async Task<List<ProjectMilestone>> GetMilestoneListByProjectAsync(string projectCode)
        {
            return await _context.ProjectMilestones
                                 .Where(m => m.ProjectCode == projectCode)
                                 .ToListAsync();
        }

        public async Task<List<ProjectIssue>> GetIssueListByProjectAsync(string projectCode)
        {
            return await _context.ProjectIssues
                                 .Where(i => i.ProjectCode == projectCode)
                                 .OrderByDescending(i => i.DateRaised)
                                 .ToListAsync();
        }

        public async Task<List<ProjectResource>> GetResourceListByProjectAsync(string projectCode)
        {
            return await _context.ProjectResources
                                 .Where(r => r.ProjectCode == projectCode)
                                 .ToListAsync();
        }


       

        #region Individual CRUD Methods

        public async Task<bool> AddProjectAsync(ProjectsMaster project)
        {
            try
            {
                await _context.ProjectsMaster.AddAsync(project);
                return await _context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new ProjectsMaster.");
                return false;
            }
        }

        public async Task<bool> UpdateProjectAsync(ProjectsMaster project)
        {
            try
            {
                _context.ProjectsMaster.Update(project);
                return await _context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ProjectsMaster with ID: {Id}", project.ProjectId);
                return false;
            }
        }

        public async Task<bool> DeleteProjectAsync(int projectId)
        {
            // Start a transaction so that if one delete fails, none are deleted
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var project = await _context.ProjectsMaster.FindAsync(projectId);
                if (project == null) return false;

                string pCode = project.ProjectCode;

                // 1. Delete associated Milestones
                var milestones = _context.ProjectMilestones.Where(m => m.ProjectCode == pCode);
                _context.ProjectMilestones.RemoveRange(milestones);

                // 2. Delete associated Issues
                var issues = _context.ProjectIssues.Where(i => i.ProjectCode == pCode);
                _context.ProjectIssues.RemoveRange(issues);

                // 3. Delete associated Resources
                var resources = _context.ProjectResources.Where(r => r.ProjectCode == pCode);
                _context.ProjectResources.RemoveRange(resources);

                // 4. Delete associated Documents
                var docs = _context.Documents.Where(d => d.ProjectCode == pCode);
                _context.Documents.RemoveRange(docs);

                // 5. Finally, delete the Project itself
                _context.ProjectsMaster.Remove(project);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to delete project {Id} and its dependencies", projectId);
                return false;
            }
        }


        public string GetProjectTimelineStatus(string? status, DateTime? expectedCompletion)
        {
            if (status?.Trim().Equals("GO LIVE", StringComparison.OrdinalIgnoreCase) == true) return "On Track";
            if (status?.Trim().Equals("DROP", StringComparison.OrdinalIgnoreCase) == true) return "Dropped";
            if (!expectedCompletion.HasValue) return "N/A";

            var delayDays = (DateTime.Now - expectedCompletion.Value).Days;
            return delayDays > 0 ? $"Delayed by {delayDays} Days" : "On Track";
        }
        #endregion

        #region Excel and Template Utilities

        public async Task<(int SuccessCount, string ErrorMessage)> UploadProjectExcelAsync(Stream fileStream)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var workbook = new XLWorkbook(memoryStream);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1);
                var projects = new List<ProjectMaster>();

                foreach (var row in rows)
                {
                    projects.Add(new ProjectMaster
                    {
                        ID = GetSafeString(row.Cell(1)),
                        NAME = GetSafeString(row.Cell(2)) ?? "Unnamed Project",
                        ACTION = GetSafeString(row.Cell(3)),
                        PROJECTCATEGORY = GetSafeString(row.Cell(4)),
                        PROJECTTYPE = GetSafeString(row.Cell(5)),
                        DEPARTMENT = GetSafeString(row.Cell(6)),
                        PROJECTLEAD = GetSafeString(row.Cell(7)),
                        BANKLEAD = GetSafeString(row.Cell(8)),
                        TEAMLEADER = GetSafeString(row.Cell(9)),
                        TEAMNAME = GetSafeString(row.Cell(10)),
                        DGM = GetSafeString(row.Cell(11)),
                        DGMID = GetSafeString(row.Cell(12)),
                        MAKER = GetSafeString(row.Cell(13)),
                        MAKERNAME = GetSafeString(row.Cell(14)),
                        CHECKER = GetSafeString(row.Cell(15)),
                        ROLES = GetSafeString(row.Cell(16)),
                        APPROVINGAUTH = GetSafeString(row.Cell(17)),
                        ADMINAPPROVALAUTH = GetSafeString(row.Cell(18)),
                        ADMINAPPROVALDATE = GetSafeDate(row.Cell(19)),
                        FINANCIALAPPROVALAUTH = GetSafeString(row.Cell(20)),
                        FINANCIALAPPROVALDATE = GetSafeDate(row.Cell(21)),
                        DATEOFACCEPTANCE = GetSafeDate(row.Cell(22)),
                        NEED = GetSafeString(row.Cell(23)),
                        BRIEF = GetSafeString(row.Cell(24)),
                        BENEFITS = GetSafeString(row.Cell(25)),
                        REMARKS = GetSafeString(row.Cell(26)),
                        PMOREMARKS = GetSafeString(row.Cell(27)),
                        ENTRYDATE = GetSafeDate(row.Cell(28)) ?? DateTime.Now,
                        PROJECTSTARTDATE = GetSafeDate(row.Cell(29)),
                        ITSTRDATE = GetSafeDate(row.Cell(30)),
                        SCHEDULEDCOMPLETIONDATE = GetSafeDate(row.Cell(31)),
                        EXPECTEDCOMPLETIONDATE = GetSafeDate(row.Cell(32)),
                        PROJECTCOMPLETIONDATE = GetSafeDate(row.Cell(33)),
                        COMPLETION = GetSafeDate(row.Cell(34)),
                        ISSUEDATE = GetSafeDate(row.Cell(35)),
                        UPDATEDATE = GetSafeDate(row.Cell(36)),
                        REVIEWDATE = GetSafeDate(row.Cell(37)),
                        TIMELINE = GetSafeString(row.Cell(38)),
                        BASECOST = GetSafeString(row.Cell(39)),
                        TAX = GetSafeString(row.Cell(40)),
                        COST = GetSafeString(row.Cell(41)),
                        AMOUNTPAIDTOVENDOR = GetSafeString(row.Cell(42)),
                        ESTIMATEDCOSTOVERRUN = GetSafeString(row.Cell(43)),
                        REVISEDCOSTAFTERECO = GetSafeString(row.Cell(44)),
                        VENDORNAME = GetSafeString(row.Cell(45)),
                        VENDORCONTACT = GetSafeString(row.Cell(46)),
                        RFPFLOATED = GetSafeString(row.Cell(47)),
                        RFPFLOATINGDATE = GetSafeDate(row.Cell(48)),
                        RFPAPPDATE = GetSafeDate(row.Cell(49)),
                        RFPCOMPLETIONDATE = GetSafeDate(row.Cell(50)),
                        RFPSTATUS = GetSafeString(row.Cell(51)),
                        PURCHASEORDERISSUE = GetSafeString(row.Cell(52)),
                        MILESTONE_1 = GetSafeString(row.Cell(53)),
                        MILESTONE_DATE_1 = GetSafeDate(row.Cell(54)),
                        MILESTONE_2 = GetSafeString(row.Cell(55)),
                        MILESTONE_DATE_2 = GetSafeDate(row.Cell(56)),
                        MILESTONE_3 = GetSafeString(row.Cell(57)),
                        MILESTONE_DATE_3 = GetSafeDate(row.Cell(58)),
                        MILESTONE_4 = GetSafeString(row.Cell(59)),
                        MILESTONE_DATE_4 = GetSafeDate(row.Cell(60)),
                        MILESTONE_5 = GetSafeString(row.Cell(61)),
                        MILESTONE_DATE_5 = GetSafeDate(row.Cell(62)),
                        SUPPVERT = GetSafeString(row.Cell(63)),
                        SUPPORTING_DEPARTMENT = GetSafeString(row.Cell(64)),
                        STATUS = GetSafeString(row.Cell(65)),
                        PREVSTATUS = GetSafeString(row.Cell(66)),
                        TICKETSTATUS = GetSafeString(row.Cell(67)),
                        PENDING = GetSafeInt(row.Cell(68)),
                        CHECKERVERIFIED = GetSafeInt(row.Cell(69)),
                        FLAG = GetSafeInt(row.Cell(70)) ?? 0,
                        REASONDELAY = GetSafeString(row.Cell(71)),
                        REJECTREASON = GetSafeString(row.Cell(72)),
                        FILENAME = GetSafeString(row.Cell(73)),
                        CONSULTREQ = GetSafeInt(row.Cell(74)),
                        IPPHONE = GetSafeString(row.Cell(75)),
                        LATESTMEETING = GetSafeString(row.Cell(76)),
                        UPDATEDDEPARTMENT = GetSafeString(row.Cell(77)),
                        UPDATEDDGM = GetSafeString(row.Cell(78)),
                        UPDATEDDGMID = GetSafeString(row.Cell(79)),
                        UPDATEDMAKER = GetSafeString(row.Cell(80)),
                        SECTION = GetSafeString(row.Cell(81))
                    });
                }

                if (projects.Any())
                {
                    await _context.ProjectMasters.AddRangeAsync(projects);
                    await _context.SaveChangesAsync();
                }

                return (projects.Count, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel Upload Failed");
                return (0, ex.Message);
            }
        }

        public byte[] GenerateExcelExport(IEnumerable<ProjectMaster> projects)
        {
            return FileProcessingHelper.ExportToExcel(projects, "Project Report");
        }

        public byte[] GenerateProjectTemplate()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Project Template");

            string[] headers = {
                "ID", "NAME", "ACTION", "PROJECTCATEGORY", "PROJECTTYPE", "DEPARTMENT", "PROJECTLEAD", "BANKLEAD", "TEAMLEADER", "TEAMNAME",
                "DGM", "DGMID", "MAKER", "MAKERNAME", "CHECKER", "ROLES", "APPROVINGAUTH", "ADMINAPPROVALAUTH", "ADMINAPPROVALDATE", "FINANCIALAPPROVALAUTH",
                "FINANCIALAPPROVALDATE", "DATEOFACCEPTANCE", "NEED", "BRIEF", "BENEFITS", "REMARKS", "PMOREMARKS", "ENTRYDATE", "PROJECTSTARTDATE", "ITSTRDATE",
                "SCHEDULEDCOMPLETIONDATE", "EXPECTEDCOMPLETIONDATE", "PROJECTCOMPLETIONDATE", "COMPLETION", "ISSUEDATE", "UPDATEDATE", "REVIEWDATE", "TIMELINE", "BASECOST", "TAX",
                "COST", "AMOUNTPAIDTOVENDOR", "ESTIMATEDCOSTOVERRUN", "REVISEDCOSTAFTERECO", "VENDORNAME", "VENDORCONTACT", "RFPFLOATED", "RFPFLOATINGDATE", "RFPAPPDATE", "RFPCOMPLETIONDATE",
                "RFPSTATUS", "PURCHASEORDERISSUE", "MILESTONE_1", "MILESTONE_DATE_1", "MILESTONE_2", "MILESTONE_DATE_2", "MILESTONE_3", "MILESTONE_DATE_3", "MILESTONE_4", "MILESTONE_DATE_4",
                "MILESTONE_5", "MILESTONE_DATE_5", "SUPPVERT", "SUPPORTING_DEPARTMENT", "STATUS", "PREVSTATUS", "TICKETSTATUS", "PENDING", "CHECKERVERIFIED", "FLAG",
                "REASONDELAY", "REJECTREASON", "FILENAME", "CONSULTREQ", "IPPHONE", "LATESTMEETING", "UPDATEDDEPARTMENT", "UPDATEDDGM", "UPDATEDDGMID", "UPDATEDMAKER", "SECTION"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
            }
            worksheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;

            worksheet.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        #endregion

        #region Helper and Filtering Methods

        public string GetProjectTimelineStatus(ProjectMaster project)
        {
            if (project.STATUS?.Trim().Equals("GO LIVE", StringComparison.OrdinalIgnoreCase) == true) return "On Track";
            if (project.STATUS?.Trim().Equals("DROP", StringComparison.OrdinalIgnoreCase) == true) return "Dropped";
            if (!project.EXPECTEDCOMPLETIONDATE.HasValue) return "N/A";

            var delayDays = (DateTime.Now - project.EXPECTEDCOMPLETIONDATE.Value).Days;
            return delayDays > 0 ? $"Delayed by {delayDays} Days" : "On Track";
        }

        public IEnumerable<ProjectMaster> FilterProjects(List<ProjectMaster> projects, string search, string status, string timelineFilter = "All", string department = "All", string section = "All")
        {
            var filtered = projects.Where(x =>
                (string.IsNullOrWhiteSpace(search) || x.NAME.Contains(search, StringComparison.OrdinalIgnoreCase) || (x.VENDORNAME?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                && (status == "All" || (x.STATUS != null && x.STATUS.Trim().Equals(status.Trim(), StringComparison.OrdinalIgnoreCase)))
                && (department == "All" || (x.DEPARTMENT != null && x.DEPARTMENT.Trim().Equals(department.Trim(), StringComparison.OrdinalIgnoreCase)))
                && (section == "All" || (x.SECTION != null && x.SECTION.Trim().Equals(section.Trim(), StringComparison.OrdinalIgnoreCase)))
            );

            if (timelineFilter == "All") return filtered;

            return filtered.Where(x => {
                var currentStatus = GetProjectTimelineStatus(x);
                if (timelineFilter == "Delayed") return currentStatus.StartsWith("Delayed");
                return currentStatus.Equals(timelineFilter, StringComparison.OrdinalIgnoreCase);
            });
        }

        private (int OnTrack, int Delayed, int Dropped) GetTimelineStats(List<ProjectMaster> projects)
        {
            int onTrack = 0; int delayed = 0; int dropped = 0;
            foreach (var project in projects)
            {
                var status = GetProjectTimelineStatus(project);
                if (status == "On Track") onTrack++;
                else if (status.StartsWith("Delayed")) delayed++;
                else if (status == "Dropped") dropped++;
            }
            return (onTrack, delayed, dropped);
        }

        private string? GetSafeString(IXLCell cell) => cell.IsEmpty() ? null : cell.GetValue<string>().Trim();

        private int? GetSafeInt(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            var val = cell.GetValue<string>();
            if (int.TryParse(val, out int result)) return result;
            if (double.TryParse(val, out double dResult)) return (int)dResult;
            return null;
        }

        private DateTime? GetSafeDate(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            try { return cell.GetDateTime(); }
            catch
            {
                if (DateTime.TryParse(cell.GetValue<string>(), out DateTime dt)) return dt;
                return null;
            }
        }

        public async Task<List<ProjectsMaster>> GetProjectsMastersAsync()
        {
            try
            {
                return await _context.ProjectsMaster.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ProjectsMaster records.");
                return new List<ProjectsMaster>();
            }
        }


        // Add to ProjectMasterService.cs
        public async Task<ProjectMilestone?> GetMilestoneByProjectAsync(string projectCode) =>
            await _context.ProjectMilestones.FirstOrDefaultAsync(m => m.ProjectCode == projectCode);

        public async Task<ProjectIssue?> GetIssueByProjectAsync(string projectCode) =>
            await _context.ProjectIssues.FirstOrDefaultAsync(i => i.ProjectCode == projectCode);

        // In ProjectMasterService.cs
        // Add/Update this in ProjectMasterService.cs
        public async Task<ProjectResource?> GetResourceByProjectAsync(string projectCode)
        {
            try
            {
                if (string.IsNullOrEmpty(projectCode)) return null;

                // Using Trim() on the database side to handle Oracle CHAR/VARCHAR2 padding
                return await _context.ProjectResources
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ProjectCode.Trim() == projectCode.Trim());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Resource for ProjectCode: {Code}", projectCode);
                return null;
            }
        }



        public async Task<Document?> GetDocumentByProjectAsync(string projectCode) =>
            await _context.Documents.FirstOrDefaultAsync(d => d.ProjectCode == projectCode);

        public async Task<bool> AddFullProjectEntryAsync(ProjectsMaster proj, ProjectMilestone m, ProjectIssue i, ProjectResource r, Document d)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Save Project General Info
                _context.ProjectsMaster.Add(proj);
                await _context.SaveChangesAsync();

                // Ensure child entities get the newly generated ProjectCode/ID
                string projectCode = proj.ProjectCode;
                decimal projectId = proj.ProjectId;

                // 2. Link and Save Milestone
                if (!string.IsNullOrWhiteSpace(m.MilestoneName))
                {
                    m.ProjectCode = projectCode;
                    _context.ProjectMilestones.Add(m);
                }

                // 3. Link and Save Issue
                if (!string.IsNullOrWhiteSpace(i.IssueDescription))
                {
                    i.ProjectCode = projectCode;
                    _context.ProjectIssues.Add(i);
                }

                // 4. Link and Save Resource
                if (!string.IsNullOrWhiteSpace(r.ResourceName))
                {
                    r.ProjectCode = projectCode;
                    r.ProjectId = projectId;
                    _context.ProjectResources.Add(r);
                }

                // 5. Link and Save Document
                if (!string.IsNullOrWhiteSpace(d.DocName))
                {
                    d.ProjectCode = projectCode;
                    _context.Documents.Add(d);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Transaction failed in AddFullProjectEntryAsync");
                return false;
            }
        }

        #endregion
    }
}