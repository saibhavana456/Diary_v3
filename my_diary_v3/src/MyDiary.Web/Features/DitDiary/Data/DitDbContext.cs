using Microsoft.EntityFrameworkCore;
using MyDiary.Web.Features.DitDiary.Models.Entities;

namespace MyDiary.Web.Features.DitDiary.Data
{
    /// <summary>DIT Diary (IT projects + budget utilization). Tables are prefixed RP_DIT_ and live in the RP_OWNER schema;
    /// org masters (ZONE/REGION/BRANCH_MASTER, ORGANISATION.STAFF_DETAILS) are read-only shared tables.</summary>
    public class DitDbContext : DbContext
    {
        public DitDbContext(DbContextOptions<DitDbContext> options) : base(options) { }

        public DbSet<ProjectMaster> ProjectMasters { get; set; }          // legacy flat register
        public DbSet<ProjectsMaster> ProjectsMaster { get; set; }

        public DbSet<ProjectMilestone> ProjectMilestones { get; set; }
        public DbSet<ProjectIssue> ProjectIssues { get; set; }
        public DbSet<ProjectResource> ProjectResources { get; set; }
        public DbSet<Document> Documents { get; set; }

        public DbSet<Department> Departments { get; set; }
        public DbSet<Section> Sections { get; set; }
        public DbSet<BudgetUtilization> BudgetUtilizations { get; set; }
        public DbSet<FinancialYear> FinancialYears { get; set; }
        public DbSet<ExpenditureHead> ExpenditureHeads { get; set; }
        public DbSet<BudgetAllocation> BudgetAllocations { get; set; }
        public DbSet<CapitalUtilization> CapitalUtilizations { get; set; }
        public DbSet<RevenueUtilization> RevenueUtilizations { get; set; }
        public DbSet<OverbudgetLog> OverbudgetLogs { get; set; }
        public DbSet<SectionTeamMember> SectionTeamMembers { get; set; }

        public DbSet<ZoneMaster> ZoneMasters { get; set; }
        public DbSet<RegionMaster> RegionMasters { get; set; }
        public DbSet<BranchMaster> BranchMasters { get; set; }
        public DbSet<DitStaffDetail> StaffDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Department>().Property(d => d.DEPT_ID).ValueGeneratedNever();
            modelBuilder.Entity<Section>().Property(s => s.SECTION_ID).ValueGeneratedNever();


            // Identity primary keys.
            modelBuilder.Entity<ProjectMilestone>().Property(p => p.MilestoneId).ValueGeneratedOnAdd();
            modelBuilder.Entity<ProjectIssue>().Property(p => p.IssueId).ValueGeneratedOnAdd();
            modelBuilder.Entity<ProjectResource>().Property(p => p.ResourceId).ValueGeneratedOnAdd();
            modelBuilder.Entity<Document>().Property(p => p.DocId).ValueGeneratedOnAdd();

            // The *_CODE columns are Oracle VIRTUAL columns (derived from the id): EF must never INSERT/UPDATE them
            // (ORA-54013) and re-reads them after SaveChanges.
            modelBuilder.Entity<ProjectsMaster>().Property(p => p.ProjectCode).ValueGeneratedOnAddOrUpdate();
            modelBuilder.Entity<ProjectMilestone>().Property(p => p.MilestoneCode).ValueGeneratedOnAddOrUpdate();
            modelBuilder.Entity<ProjectIssue>().Property(p => p.IssueCode).ValueGeneratedOnAddOrUpdate();
            modelBuilder.Entity<ProjectResource>().Property(p => p.ResourceCode).ValueGeneratedOnAddOrUpdate();
            modelBuilder.Entity<Document>().Property(p => p.DocCode).ValueGeneratedOnAddOrUpdate();
        }
    }
}
