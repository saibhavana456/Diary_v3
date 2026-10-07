using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyDiary.Web.Features.Procurement.Models.TABLES;
using MyDiary.Web.Features.Procurement.Models.VIEWS;

namespace MyDiary.Web.Features.Procurement.Data
{
    public class ProcurementDbContext : DbContext
    {
        public ProcurementDbContext(DbContextOptions<ProcurementDbContext> options) : base(options) { }
        public DbSet<ItemRequestMaster> ITEM_REQUEST_MASTER => Set<ItemRequestMaster>();
        public DbSet<ATMRequestMaster> ATM_REQUEST_MASTER => Set<ATMRequestMaster>();
        public DbSet<RequestItem> REQUEST_ITEM => Set<RequestItem>();
        public DbSet<RequestATM> REQUEST_ATM => Set<RequestATM>();
        public DbSet<AtmMaster> ATM_MASTER => Set<AtmMaster>();
        public DbSet<UserMaster> USER_MASTER => Set<UserMaster>();
        public DbSet<NewATMRequestMaster> NEW_ATM_REQUEST_MASTER => Set<NewATMRequestMaster>();
        public virtual DbSet<UserMasterView> UserMasterView { get; set; }


        public DbSet<ItemVendorMaster> ITEM_VENDOR_MASTER => Set<ItemVendorMaster>();
        public DbSet<ATMVendorMaster> ATM_VENDOR_MASTER => Set<ATMVendorMaster>();



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var boolToCharConverter = new ValueConverter<bool?, string>(
                    toDb => toDb == null ? null : (toDb.Value ? "Y" : "N"),
                    fromDb => fromDb == null ? (bool?)null : fromDb == "Y"
            );

            var boolToNumberConverter = new ValueConverter<bool?, int?>(
    toDb => toDb == null ? null : (toDb.Value ? 1 : 0),
    fromDb => fromDb == null ? (bool?)null : fromDb == 1
);
            modelBuilder.Entity<NewATMRequestMaster>(entity =>
            {

                entity.ToTable("NEW_ATM_REQUEST_MASTER");

                entity.HasKey(e => e.Sno)
                      .HasName("PK_NEW_ATM_REQUEST_MASTER");

                entity.Property(e => e.Sno)
                      .HasColumnName("SNO")
                      .ValueGeneratedOnAdd();

                entity.Property(e => e.RequestId)
                      .HasColumnName("REQUESTID")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.BranchCode)
                      .HasColumnName("BRANCH_CODE")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.BranchName)
                      .HasColumnName("BRANCH_NAME")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.Region)
                      .HasColumnName("REGION")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.FinacleId)
                      .HasColumnName("FINACLE_ID")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.ItemCategory)
                      .HasColumnName("ITEM_CATEGORY")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.RaisedBy)
                      .HasColumnName("RAISED_BY")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.RaisedDate)
                      .HasColumnName("RAISED_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.ProposedAtmCenter)
                      .HasColumnName("PROPOSED_ATM_CENTER")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.LinkBranchIp)
                      .HasColumnName("LINK_BRANCH_IP")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.PopulationCategory)
                      .HasColumnName("POPULATION_CATEGORY")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.AtmSiteAddress)
                      .HasColumnName("ATM_SITE_ADDRESS")
                      .HasMaxLength(500)
                      .IsUnicode(false);

                entity.Property(e => e.DistanceFromBranch)
                      .HasColumnName("DISTANCE_FROM_BRANCH")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.AtmCount1Km)
                      .HasColumnName("ATM_COUNT_1KM")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.Justification)
                      .HasColumnName("JUSTIFICATION")
                      .HasColumnType("NCLOB");

                entity.Property(e => e.RoMakerRecommendation)
                      .HasColumnName("RO_MAKER_RECOMMENDATION")
                      .HasMaxLength(100);

                entity.Property(e => e.RoMakerName)
                      .HasColumnName("RO_MAKER_NAME")
                      .HasMaxLength(200);

                entity.Property(e => e.BusinessMixParentBranch)
                      .HasColumnName("BUSINESS_MIX_PARENT_BRANCH")
                      .HasMaxLength(100);

                entity.Property(e => e.CasaAccounts)
                      .HasColumnName("CASA_ACCOUNTS")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.DebitCardsIssued)
                      .HasColumnName("DEBIT_CARDS_ISSUED")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.CasaBusinessVolume)
                      .HasColumnName("CASA_BUSINESS_VOLUME")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.AvgCashWithdrawals)
                      .HasColumnName("AVG_CASH_WITHDRAWALS")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.ExpectedAtmTransactions)
                      .HasColumnName("EXPECTED_ATM_TRANSACTIONS")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.PremisesAcquired)
                      .HasColumnName("PREMISES_ACQUIRED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToNumberConverter);

                entity.Property(e => e.ExpectedRent)
                      .HasColumnName("EXPECTED_RENT")
                      .HasMaxLength(100);

                entity.Property(e => e.NewSbAccountsExpected)
                      .HasColumnName("NEW_SB_ACCOUNTS_EXPECTED")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.ExpectedAtmCards)
                      .HasColumnName("EXPECTED_ATM_CARDS")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.NearbyBankAtms)
                      .HasColumnName("NEARBY_BANK_ATMS")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.AvgHits)
                      .HasColumnName("AVG_HITS")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.VisibilityParkingSpace)
                      .HasColumnName("VISIBILITY_PARKING_SPACE")
                      .HasMaxLength(100);

                entity.Property(e => e.SalaryAccounts)
                      .HasColumnName("SALARY_ACCOUNTS")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.WorkingHours)
                      .HasColumnName("WORKING_HOURS")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.RoMakerForwardTo)
                      .HasColumnName("RO_MAKER_FORWARD_TO")
                      .HasMaxLength(100);

                entity.Property(e => e.RoMakerModifiedDate)
                      .HasColumnName("RO_MAKER_MODIFIED_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.CheckerRecommendation)
                      .HasColumnName("CHECKER_RECOMMENDATION")
                      .HasMaxLength(100);

                entity.Property(e => e.CheckerRemarks)
                      .HasColumnName("CHECKER_REMARKS")
                      .HasMaxLength(100);

                entity.Property(e => e.CheckerForwardTo)
                      .HasColumnName("CHECKER_FORWARD_TO")
                      .HasMaxLength(100);

                entity.Property(e => e.CheckerName)
                      .HasColumnName("CHECKER_NAME")
                      .HasMaxLength(100);

                entity.Property(e => e.CheckerModifiedDate)
                      .HasColumnName("CHECKER_MODIFIED_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.RoCheckerRecommendation)
                      .HasColumnName("RO_CHECKER_RECOMMENDATION")
                      .HasMaxLength(100);

                entity.Property(e => e.RoCheckerRemarks)
                      .HasColumnName("RO_CHECKER_REMARKS")
                      .HasMaxLength(100);

                entity.Property(e => e.RoCheckerForwardTo)
                      .HasColumnName("RO_CHECKER_FORWARD_TO")
                      .HasMaxLength(100);

                entity.Property(e => e.RoCheckerName)
                      .HasColumnName("RO_CHECKER_NAME")
                      .HasMaxLength(100);

                entity.Property(e => e.RoCheckerModifiedDate)
                      .HasColumnName("RO_CHECKER_MODIFIED_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.ZoRecommendation)
                      .HasColumnName("ZO_RECOMMENDATION")
                      .HasMaxLength(100);

                entity.Property(e => e.ZoRemarks)
                      .HasColumnName("ZO_REMARKS")
                      .HasMaxLength(100);

                entity.Property(e => e.ZoForwardTo)
                      .HasColumnName("ZO_FORWARD_TO")
                      .HasMaxLength(100);

                entity.Property(e => e.ZoCheckerName)
                      .HasColumnName("ZO_CHECKER_NAME")
                      .HasMaxLength(100);

                entity.Property(e => e.ZoModifiedDate)
                      .HasColumnName("ZO_MODIFIED_DATE")
                      .HasColumnType("DATE");


                entity.Property(e => e.CoRecommendation)
                      .HasColumnName("CO_RECOMMENDATION")
                      .HasMaxLength(100);

                entity.Property(e => e.CoRemarks)
                      .HasColumnName("CO_REMARKS")
                      .HasMaxLength(100);

                entity.Property(e => e.CoApproverName)
                      .HasColumnName("CO_APPROVER_NAME")
                      .HasMaxLength(100);

                entity.Property(e => e.CoModifiedDate)
                      .HasColumnName("CO_MODIFIED_DATE")
                      .HasColumnType("DATE");

            });

            modelBuilder.Entity<RequestItem>(entity =>
            {

                entity.ToTable("REQUEST_ITEM", "PRCRMNT");


                entity.HasKey(e => e.SR_NO)
                      .HasName("PK_REQUEST_ITEM");


                entity.Property(e => e.SR_NO)
                      .HasColumnName("SR_NO")
                      .ValueGeneratedOnAdd();


                entity.Property(e => e.RequestCode)
                      .HasColumnName("REQUEST_CODE")
                      .HasMaxLength(100)
                      .IsUnicode(false);


                entity.Property(e => e.ItemCode)
                      .HasColumnName("ITEM_CODE")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.Quantity)
                      .HasColumnName("QUANTITY")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.CostBeforeTax)
                .HasColumnName("COST_BEFORE_TAX")
                .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.CostAfterTax)
                    .HasColumnName("COST_AFTER_TAX")
                    .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.Remarks)
      .HasColumnName("REMARKS")
      .HasColumnType("NCLOB");

                entity.Property(e => e.IsPOIssued)
                      .HasColumnName("IS_PO_ISSUED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToCharConverter);

                entity.Property(e => e.POIssuedBy)
                     .HasColumnName("PO_ISSUED_BY")
                     .HasMaxLength(100)
                     .IsUnicode(false);

                entity.Property(e => e.POIssuedOn)
                .HasColumnName("PO_ISSUED_ON")
                .HasColumnType("DATE");

                entity.Property(e => e.IsPoAccepted)
                      .HasColumnName("IS_PO_ACCEPTED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToCharConverter);

                entity.Property(e => e.PoAcceptedBy)
                   .HasColumnName("PO_ACCEPTED_BY")
                   .HasMaxLength(100)
                   .IsUnicode(false);

                entity.Property(e => e.PoAcceptedOn)
                .HasColumnName("PO_ACCEPTED_ON")
                .HasColumnType("DATE");

                entity.Property(e => e.OrderedBy)
                   .HasColumnName("ORDERED_BY")
                   .HasMaxLength(100)
                   .IsUnicode(false);

                entity.Property(e => e.OrderedOn)
                .HasColumnName("ORDERED_ON")
                .HasColumnType("DATE");

                entity.Property(e => e.PreBookedAmt)
                    .HasColumnName("PRE_BOOKED_AMT")
                    .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.ReceivedBy)
                    .HasColumnName("RECEIVED_BY")
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.DeliveredOn)
                .HasColumnName("DELIVERED_ON")
                .HasColumnType("DATE");

                entity.Property(e => e.PostDeliveredAmt)
                   .HasColumnName("POST_DELIVERED_AMT")
                   .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.InstalledOn)
               .HasColumnName("INSTALLED_ON")
               .HasColumnType("DATE");

                entity.Property(e => e.PostInstalledAmt)
                   .HasColumnName("POST_INSTALLED_AMT")
                   .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.IsDeleted)
                      .HasColumnName("IS_DELETED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToCharConverter);

                entity.Property(e => e.DeletedBy)
                   .HasColumnName("DELETED_BY")
                   .HasMaxLength(100)
                   .IsUnicode(false);

                entity.Property(e => e.DeletedOn)
                .HasColumnName("DELETED_ON")
                .HasColumnType("DATE");


                entity.HasIndex(e => e.RequestCode)
                      .HasDatabaseName("IX_REQUEST_ITEM_REQUEST_CODE");


                entity.HasOne(ri => ri.RequestMaster)
                              .WithMany(rm => rm.RequestItem)
                              .HasForeignKey(ri => ri.RequestCode)
                              .HasPrincipalKey(rm => rm.RequestCode)
                              .OnDelete(DeleteBehavior.Cascade)
                              .HasConstraintName("FK_REQUEST_ITEM_MASTER");


            });

            modelBuilder.Entity<RequestATM>(entity =>
            {

                entity.ToTable("REQUEST_ATM", "PRCRMNT");


                entity.HasKey(e => e.SR_NO)
                      .HasName("PK_REQUEST_ATM");


                entity.Property(e => e.SR_NO)
                      .HasColumnName("SR_NO")
                      .ValueGeneratedOnAdd();


                entity.Property(e => e.RequestCode)
                      .HasColumnName("REQUEST_CODE")
                      .HasMaxLength(100)
                      .IsUnicode(false);


                entity.Property(e => e.AtmCode)
                      .HasColumnName("ATM_CODE")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.Quantity)
                      .HasColumnName("QUANTITY")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.CostBeforeTax)
                .HasColumnName("COST_BEFORE_TAX")
                .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.ApprovedQuantity)
                     .HasColumnName("APPROVED_QUANTITY")
                     .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.Remarks)
      .HasColumnName("REMARKS")
      .HasColumnType("NCLOB");

                entity.Property(e => e.IsPOIssued)
                      .HasColumnName("IS_PO_ISSUED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToCharConverter);

                entity.Property(e => e.POIssuedBy)
                     .HasColumnName("PO_ISSUED_BY")
                     .HasMaxLength(100)
                     .IsUnicode(false);

                entity.Property(e => e.POIssuedOn)
                .HasColumnName("PO_ISSUED_ON")
                .HasColumnType("DATE");

                entity.Property(e => e.IsPoAccepted)
                      .HasColumnName("IS_PO_ACCEPTED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToCharConverter);

                entity.Property(e => e.PoAcceptedBy)
                   .HasColumnName("PO_ACCEPTED_BY")
                   .HasMaxLength(100)
                   .IsUnicode(false);

                entity.Property(e => e.PoAcceptedOn)
                .HasColumnName("PO_ACCEPTED_ON")
                .HasColumnType("DATE");

                entity.Property(e => e.OrderedBy)
                   .HasColumnName("ORDERED_BY")
                   .HasMaxLength(100)
                   .IsUnicode(false);

                entity.Property(e => e.OrderedOn)
                .HasColumnName("ORDERED_ON")
                .HasColumnType("DATE");

                entity.Property(e => e.PreBookedAmt)
                    .HasColumnName("PRE_BOOKED_AMT")
                    .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.ReceivedBy)
                    .HasColumnName("RECEIVED_BY")
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.DeliveredOn)
                .HasColumnName("DELIVERED_ON")
                .HasColumnType("DATE");

                entity.Property(e => e.PostDeliveredAmt)
                   .HasColumnName("POST_DELIVERED_AMT")
                   .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.InstalledOn)
               .HasColumnName("INSTALLED_ON")
               .HasColumnType("DATE");

                entity.Property(e => e.PostInstalledAmt)
                   .HasColumnName("POST_INSTALLED_AMT")
                   .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.PaymentDate)
        .HasColumnName("PAYMENT_DATE")
        .HasColumnType("DATE");

                entity.Property(e => e.InvoiceNumber)
                    .HasColumnName("INVOICE_NUMBER")
                    .HasMaxLength(100)
                    .IsUnicode(false); // VARCHAR2

                entity.Property(e => e.PaymentBy)
                    .HasColumnName("PAYMENT_BY")
                    .HasMaxLength(100)
                    .IsUnicode(true); // NVARCHAR2 supports Unicode

                entity.Property(e => e.InvoiceDoc)
                    .HasColumnName("INVOICE_DOC")
                    .HasColumnType("BLOB");

                entity.Property(e => e.InvoiceDocName)
                    .HasColumnName("INVOICE_DOC_NAME")
                    .HasMaxLength(100)
                    .IsUnicode(false); // VARCHAR2

                entity.Property(e => e.IsDeleted)
                      .HasColumnName("IS_DELETED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToCharConverter);

                entity.Property(e => e.DeletedBy)
                   .HasColumnName("DELETED_BY")
                   .HasMaxLength(100)
                   .IsUnicode(false);

                entity.Property(e => e.DeletedOn)
                .HasColumnName("DELETED_ON")
                .HasColumnType("DATE");

                entity.Property(e => e.PoGeneratedDoc)
            .HasColumnName("PO_GENERATED_DOC")
            .HasColumnType("BLOB");

                entity.Property(e => e.PoGeneratedDocName)
                    .HasColumnName("PO_GENERATED_DOC_NAME")
                    .HasMaxLength(100);

                // Document 2: PO Accepted
                entity.Property(e => e.PoAcceptedDoc)
                    .HasColumnName("PO_ACCEPTED_DOC")
                    .HasColumnType("BLOB");

                entity.Property(e => e.PoAcceptedDocName)
                    .HasColumnName("PO_ACCEPTED_DOC_NAME")
                    .HasMaxLength(100);



                // Document 3: Pre Booked
                entity.Property(e => e.PreBookedDoc)
                    .HasColumnName("PRE_BOOKED_DOC")
                    .HasColumnType("BLOB");

                entity.Property(e => e.PreBookedDocName)
                    .HasColumnName("PRE_BOOKED_DOC_NAME")
                    .HasMaxLength(100);

                // Document 4: Order Received
                entity.Property(e => e.OrderRecivedDoc)
                    .HasColumnName("ORDER_RECIVED_DOC")
                    .HasColumnType("BLOB");

                entity.Property(e => e.OrderRecivedDocName)
                    .HasColumnName("ORDER_RECIVED_DOC_NAME")
                    .HasMaxLength(100);

                // Document 5: Post Installation
                entity.Property(e => e.PostInstallationDoc)
                    .HasColumnName("POST_INSTALLATION_DOC")
                    .HasColumnType("BLOB");

                entity.Property(e => e.PostInstallationDocName)
                    .HasColumnName("POST_INSTALLATION_DOC_NAME")
                    .HasMaxLength(100);


                entity.HasIndex(e => e.RequestCode)
                      .HasDatabaseName("IX_REQUEST_ATM_REQUEST_CODE");


                entity.HasOne(ri => ri.RequestMaster)
                              .WithMany(rm => rm.RequestATM)
                              .HasForeignKey(ri => ri.RequestCode)
                              .HasPrincipalKey(rm => rm.RequestCode)
                              .OnDelete(DeleteBehavior.Cascade)
                              .HasConstraintName("FK_REQUEST_ATM_MASTER");


            });

            modelBuilder.Entity<UserMaster>(entity =>
            {

                entity.ToTable("USER_MASTER", "PRCRMNT");

                entity.HasKey(e => e.PF_NO)
                      .HasName("PK_USER_MASTER");

                var srNo = entity.Property(e => e.SR_NO)
                                     .HasColumnName("SR_NO")
                                     .ValueGeneratedOnAdd();

                // PK column
                entity.Property(e => e.PF_NO)
                      .HasColumnName("PF_NO")
                      .HasMaxLength(100)
                      .IsUnicode(false) // VARCHAR2
                      .IsRequired();



                entity.Property(e => e.OfficeType)
                      .HasColumnName("OFFICE_TYPE")
                      .HasMaxLength(50)
                      .IsUnicode(false);

                entity.Property(e => e.Role)
                      .HasColumnName("ROLE")
                      .HasMaxLength(50)
                      .IsUnicode(false);

                entity.Property(e => e.AccessLevel)
                      .HasColumnName("ACCESS_LEVEL")
                      .HasMaxLength(50)
                      .IsUnicode(false);

                entity.Property(e => e.ReportingOfficerPFNo)
                      .HasColumnName("REPORTING_OFFICER_PF_NO")
                      .HasMaxLength(200)
                      .IsUnicode(false);

                entity.Property(e => e.ReportingOfficerName)
                      .HasColumnName("REPORTING_OFFICER_NAME")
                      .HasMaxLength(200)
                      .IsUnicode(false);


                entity.Property(e => e.EffectiveDate)
                      .HasColumnName("EFFECTIVE_DATE")
                      .HasColumnType("DATE");


                entity.Property(e => e.REMARKS)
                      .HasColumnName("REMARKS")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.CreatedBy)
                      .HasColumnName("CREATED_BY")
                      .HasMaxLength(20)
                      .IsUnicode(false);

                entity.Property(e => e.CreatedDate)
                      .HasColumnName("CREATED_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.ISDELETED)
                      .HasColumnName("IS_DELETED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToCharConverter);

                entity.Property(e => e.DeletedBy)
                      .HasColumnName("DELETED_BY")
                      .HasColumnType("NVARCHAR2(100)")
                      .IsUnicode(true);

                entity.Property(e => e.DeletedOn)
                      .HasColumnName("DELETED_ON")
                      .HasColumnType("DATE");
                entity.HasIndex(e => e.SR_NO).HasDatabaseName("IX_USER_MASTER_SR_NO");


            });
            modelBuilder.Entity<ItemRequestMaster>(entity =>
            {
                entity.ToTable("ITEM_REQUEST_MASTER", "PRCRMNT");

                entity.HasKey(e => e.RequestCode)
                      .HasName("PK_ITEM_REQUEST_MASTER");

                // Identity: defined in DB, so ValueGeneratedOnAdd is enough
                entity.Property(e => e.SR_NO)
                      .HasColumnName("SR_NO")
                      .ValueGeneratedOnAdd();

                entity.Property(e => e.RequestCode)
                      .HasColumnName("REQUEST_CODE")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.BranchCode)
                      .HasColumnName("BRANCH_CODE")
                      .HasMaxLength(50)
                      .IsUnicode(false);

                entity.Property(e => e.FinYear)
                      .HasColumnName("FIN_YEAR")
                      .HasColumnType("NVARCHAR2(50)")
                      .HasMaxLength(50)
                      .IsUnicode(true);

                entity.Property(e => e.Month)
                      .HasColumnName("MONTH")
                      .HasColumnType("NVARCHAR2(50)")
                      .HasMaxLength(50)
                      .IsUnicode(true);

                entity.Property(e => e.FinacleId)
                      .HasColumnName("FINACLE_ID")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.Justification)
                      .HasColumnName("JUSTIFICATION")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.UrgencyLevel)
                      .HasColumnName("URGENCY_LEVEL")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.Attachment)
                      .HasColumnName("ATTACHMENT")
                      .HasColumnType("BLOB");

                entity.Property(e => e.AttachmentName)
                     .HasColumnName("ATTACHMENT_NAME")
                     .HasColumnType("NVARCHAR2(100)")
                     .HasMaxLength(100)
                     .IsUnicode(true);


                entity.Property(e => e.RaisedBy)
                      .HasColumnName("RAISED_BY")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.RaisedOn)
                      .HasColumnName("RAISED_ON")
                      .HasColumnType("DATE");

                entity.Property(e => e.BranchRecommendation)
                      .HasColumnName("BRANCH_RECOMMENDATION")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.BranchRemarks)
                      .HasColumnName("BRANCH_REMARKS")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.BranchStatusDate)
                      .HasColumnName("BRANCH_STATUS_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.CheckerApproverName)
                      .HasColumnName("CHECKER_APPROVER_NAME")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.RegionRecommendation)
                      .HasColumnName("REGION_RECOMMENDATION")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.RegionRemarks)
                      .HasColumnName("REGION_REMARKS")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.RegionStatusDate)
                      .HasColumnName("REGION_STATUS_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.RegionApprovedQuantity)
                      .HasColumnName("REGION_APPROVED_QUANTITY")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.RegionForwardTo)
                      .HasColumnName("REGION_FORWARD_TO")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.RegionApproverPfNo)
                      .HasColumnName("REGION_APPROVER_PF_NO")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.RegionApproverName)
                      .HasColumnName("REGION_APPROVER_NAME")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);


                entity.Property(e => e.ZoneRecommendation)
                      .HasColumnName("ZONE_RECOMMENDATION")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.ZoneRemarks)
                      .HasColumnName("ZONE_REMARKS")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.ZoneStatusDate)
                      .HasColumnName("ZONE_STATUS_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.ZoneApprovedQuantity)
                      .HasColumnName("ZONE_APPROVED_QUANTITY")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.ZoneForwardTo)
                      .HasColumnName("ZONE_FORWARD_TO")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.ZoneApproverPfNo)
                      .HasColumnName("ZONE_APPROVER_PF_NO")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.ZoneApproverName)
                      .HasColumnName("ZONE_APPROVER_NAME")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.CoRecommendation)
                      .HasColumnName("CO_RECOMMENDATION")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.CoRemarks)
                      .HasColumnName("CO_REMARKS")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.CoStatusDate)
                      .HasColumnName("CO_STATUS_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.CoApprovedQuantity)
                      .HasColumnName("CO_APPROVED_QUANTITY")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.CoForwardTo)
                      .HasColumnName("CO_FORWARD_TO")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.CoApproverPfNo)
                      .HasColumnName("CO_APPROVER_PF_NO")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.CoApproverName)
                      .HasColumnName("CO_APPROVER_NAME")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                // Soft-delete: CHAR(1) with converter
                entity.Property(e => e.ISDELETED)
                      .HasColumnName("IS_DELETED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToCharConverter);

                entity.Property(e => e.DeletedBy)
                      .HasColumnName("DELETED_BY")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.DeletedOn)
                      .HasColumnName("DELETED_ON")
                      .HasColumnType("DATE");

                // Optional helpful indexes
                entity.HasIndex(e => e.RaisedOn).HasDatabaseName("IX_ITEM_REQUEST_MASTER_RAISED_ON");
                entity.HasIndex(e => e.BranchCode).HasDatabaseName("IX_ITEM_REQUEST_MASTER_BRANCH_CODE");
            });

            modelBuilder.Entity<ATMRequestMaster>(entity =>
            {
                entity.ToTable("ATM_REQUEST_MASTER", "PRCRMNT");

                entity.HasKey(e => e.RequestCode)
                      .HasName("PK_ATM_REQUEST_MASTER");

                // Identity: defined in DB, so ValueGeneratedOnAdd is enough
                entity.Property(e => e.SR_NO)
                      .HasColumnName("SR_NO")
                      .ValueGeneratedOnAdd();

                entity.Property(e => e.RequestCode)
                      .HasColumnName("REQUEST_CODE")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.BranchCode)
                      .HasColumnName("BRANCH_CODE")
                      .HasMaxLength(50)
                      .IsUnicode(false);

                entity.Property(e => e.ATMId)
                      .HasColumnName("ATM_ID")
                      .HasMaxLength(100)
                      .IsUnicode(false);

                entity.Property(e => e.FinYear)
                      .HasColumnName("FIN_YEAR")
                      .HasColumnType("NVARCHAR2(50)")
                      .HasMaxLength(50)
                      .IsUnicode(true);

                entity.Property(e => e.Month)
                      .HasColumnName("MONTH")
                      .HasColumnType("NVARCHAR2(50)")
                      .HasMaxLength(50)
                      .IsUnicode(true);

                entity.Property(e => e.FinacleId)
                      .HasColumnName("FINACLE_ID")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.Justification)
                      .HasColumnName("JUSTIFICATION")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.UrgencyLevel)
                      .HasColumnName("URGENCY_LEVEL")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.Attachment)
                      .HasColumnName("ATTACHMENT")
                      .HasColumnType("BLOB");

                entity.Property(e => e.AttachmentName)
                     .HasColumnName("ATTACHMENT_NAME")
                     .HasColumnType("NVARCHAR2(100)")
                     .HasMaxLength(100)
                     .IsUnicode(true);


                entity.Property(e => e.RaisedBy)
                      .HasColumnName("RAISED_BY")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.RaisedOn)
                      .HasColumnName("RAISED_ON")
                      .HasColumnType("DATE");

                entity.Property(e => e.BranchRecommendation)
                      .HasColumnName("BRANCH_RECOMMENDATION")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.BranchRemarks)
                      .HasColumnName("BRANCH_REMARKS")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.BranchStatusDate)
                      .HasColumnName("BRANCH_STATUS_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.CheckerApproverName)
                      .HasColumnName("CHECKER_APPROVER_NAME")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.RegionRecommendation)
                      .HasColumnName("REGION_RECOMMENDATION")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.RegionRemarks)
                      .HasColumnName("REGION_REMARKS")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.RegionStatusDate)
                      .HasColumnName("REGION_STATUS_DATE")
                      .HasColumnType("DATE");


                entity.Property(e => e.RegionForwardTo)
                      .HasColumnName("REGION_FORWARD_TO")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.RegionApproverPfNo)
                      .HasColumnName("REGION_APPROVER_PF_NO")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.RegionApproverName)
                      .HasColumnName("REGION_APPROVER_NAME")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.RegionCheckerName)
                     .HasColumnName("REGION_CHECKER_NAME")
                     .HasColumnType("NVARCHAR2(100)")
                     .HasMaxLength(100)
                     .IsUnicode(true);

                entity.Property(e => e.RegionApproverRecommendation)
                    .HasColumnName("REGION_APPROVER_RECOMMENDATION")
                    .HasColumnType("NVARCHAR2(100)")
                    .HasMaxLength(100)
                    .IsUnicode(true);

                entity.Property(e => e.RegionApproverRemarks)
                      .HasColumnName("REGION_APPROVER_REMARKS")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.RegionApproverDate)
                      .HasColumnName("REGION_APPROVER_DATE")
                      .HasColumnType("DATE");

                //entity.Property(e => e.ZoneRecommendation)
                //      .HasColumnName("ZONE_RECOMMENDATION")
                //      .HasColumnType("NVARCHAR2(100)")
                //      .HasMaxLength(100)
                //      .IsUnicode(true);

                //entity.Property(e => e.ZoneRemarks)
                //      .HasColumnName("ZONE_REMARKS")
                //      .HasColumnType("NCLOB")
                //      .IsUnicode(true);

                //entity.Property(e => e.ZoneStatusDate)
                //      .HasColumnName("ZONE_STATUS_DATE")
                //      .HasColumnType("DATE");

                //entity.Property(e => e.ZoneApprovedQuantity)
                //      .HasColumnName("ZONE_APPROVED_QUANTITY")
                //      .HasColumnType("NUMBER(18,0)");

                //entity.Property(e => e.ZoneForwardTo)
                //      .HasColumnName("ZONE_FORWARD_TO")
                //      .HasColumnType("NVARCHAR2(100)")
                //      .HasMaxLength(100)
                //      .IsUnicode(true);

                //entity.Property(e => e.ZoneApproverPfNo)
                //      .HasColumnName("ZONE_APPROVER_PF_NO")
                //      .HasColumnType("NVARCHAR2(100)")
                //      .HasMaxLength(100)
                //      .IsUnicode(true);

                //entity.Property(e => e.ZoneApproverName)
                //      .HasColumnName("ZONE_APPROVER_NAME")
                //      .HasColumnType("NVARCHAR2(100)")
                //      .HasMaxLength(100)
                //      .IsUnicode(true);

                entity.Property(e => e.Feedback)
                    .HasColumnName("FEEDBACK")
                    .HasColumnType("NVARCHAR2(255)")
                    .HasMaxLength(255)
                    .IsUnicode(true);


                entity.Property(e => e.FeedbackBy)
                      .HasColumnName("FEEDBACKBY")
                      .HasColumnType("NVARCHAR2(10)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.FeedbackDate)
                      .HasColumnName("FEEDBACKDATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.CoRecommendation)
                      .HasColumnName("CO_RECOMMENDATION")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.CoRemarks)
                      .HasColumnName("CO_REMARKS")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.CoStatusDate)
                      .HasColumnName("CO_STATUS_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.CoApprovedQuantity)
                      .HasColumnName("CO_APPROVED_QUANTITY")
                      .HasColumnType("NUMBER(18,0)");

                entity.Property(e => e.CoForwardTo)
                      .HasColumnName("CO_FORWARD_TO")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.CoApproverPfNo)
                      .HasColumnName("CO_APPROVER_PF_NO")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.CoApproverName)
                      .HasColumnName("CO_APPROVER_NAME")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.CoCheckerRecommendation)
                     .HasColumnName("CO_CHECKER_RECOMMENDATION")
                     .HasColumnType("NVARCHAR2(100)")
                     .HasMaxLength(100)
                     .IsUnicode(true);
                entity.Property(e => e.CoCheckerRemarks)
                      .HasColumnName("CO_CHECKER_REMARKS")
                      .HasColumnType("NCLOB")
                      .IsUnicode(true);

                entity.Property(e => e.CoApproverDate)
                      .HasColumnName("CO_APPROVER_DATE")
                      .HasColumnType("DATE");

                entity.Property(e => e.CoCheckerName)
                      .HasColumnName("CO_CHECKER_NAME")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.IsPoIssued)
                     .HasColumnName("IS_PO_ISSUED")
                     .HasColumnType("CHAR(1)")
                     .HasConversion(boolToCharConverter);

                entity.Property(e => e.PoIssuedBy)
                .HasColumnName("PO_ISSUED_BY")
                .HasMaxLength(100)
                .IsUnicode(true);

                entity.Property(e => e.PoIssuedOn)
                .HasColumnName("PO_ISSUED_ON")
                .HasColumnType("DATE");

                entity.Property(e => e.IsPoAccepted)
                     .HasColumnName("IS_PO_ACCEPTED")
                     .HasColumnType("CHAR(1)")
                     .HasConversion(boolToCharConverter);

                entity.Property(e => e.PoAcceptedBy)
                .HasColumnName("PO_ACCEPTED_BY")
                .HasMaxLength(100)
                .IsUnicode(true);

                entity.Property(e => e.PoAcceptedOn)
                .HasColumnName("PO_ACCEPTED_ON")
                .HasColumnType("DATE");

                entity.Property(e => e.PoAcceptedDoc)
                     .HasColumnName("PO_ACCEPTED_DOC")
                     .HasColumnType("BLOB");

                entity.Property(e => e.PoAcceptedDocName)
                     .HasColumnName("PO_ACCEPTED_DOC_NAME")
                     .HasColumnType("NVARCHAR2(100)")
                     .HasMaxLength(100)
                     .IsUnicode(true);


                entity.Property(e => e.PoRefno)
                     .HasColumnName("PO_REF_NO")
                     .HasColumnType("NVARCHAR2(100)")
                     .HasMaxLength(100)
                     .IsUnicode(true);

                // Soft-delete: CHAR(1) with converter
                entity.Property(e => e.ISDELETED)
                      .HasColumnName("IS_DELETED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToCharConverter);

                entity.Property(e => e.DeletedBy)
                      .HasColumnName("DELETED_BY")
                      .HasColumnType("NVARCHAR2(100)")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.DeletedOn)
                      .HasColumnName("DELETED_ON")
                      .HasColumnType("DATE");

                // Optional helpful indexes
                entity.HasIndex(e => e.RaisedOn).HasDatabaseName("IX_ATM_REQUEST_MASTER_RAISED_ON");
                entity.HasIndex(e => e.BranchCode).HasDatabaseName("IX_ATM_REQUEST_MASTER_BRANCH_CODE");
            });

            modelBuilder.Entity<ItemVendorMaster>(entity =>
            {
                entity.ToTable("ITEM_VENDOR_MASTER", "PRCRMNT");

                entity.HasKey(e => e.SrNo)
                      .HasName("PK_ITEM_VENDOR_MASTER");

                entity.Property(e => e.SrNo)
                      .HasColumnName("SR_NO")
                      .ValueGeneratedOnAdd();

                entity.Property(e => e.ItemCode)
                      .HasColumnName("ITEM_CODE")
                      .HasMaxLength(100)
                      .IsUnicode(false)
                      .IsRequired();

                entity.Property(e => e.ItemName)
                      .HasColumnName("ITEM_NAME")
                      .HasMaxLength(200)
                      .IsUnicode(true);

                entity.Property(e => e.ItemCategory)
                      .HasColumnName("ITEM_CATEGORY")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.ItemCost)
                      .HasColumnName("ITEM_COST")
                      .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.ItemGST)
                      .HasColumnName("ITEM_GST")
                      .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.Specification)
                      .HasColumnName("SPECIFICATION")
                      .HasColumnType("NCLOB");


                entity.Property(e => e.VendorName)
                      .HasColumnName("VENDOR_NAME")
                      .HasMaxLength(200)
                      .IsUnicode(true);

                entity.Property(e => e.VendorGst)
                      .HasColumnName("VENDOR_GST")
                      .HasMaxLength(50)
                      .IsUnicode(true);

                entity.Property(e => e.PinCode)
                      .HasColumnName("PIN_CODE")
                      .HasMaxLength(20)
                      .IsUnicode(true);

                entity.Property(e => e.BudgetHead)
                      .HasColumnName("BUDGET_HEAD")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.IsDeleted)
                      .HasColumnName("IS_DELETED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToCharConverter);
            });

            modelBuilder.Entity<ATMVendorMaster>(entity =>
            {
                entity.ToTable("ATM_VENDOR_MASTER", "PRCRMNT");

                entity.HasKey(e => e.SrNo)
                      .HasName("PK_ATM_VENDOR_MASTER");

                entity.Property(e => e.SrNo)
                      .HasColumnName("SR_NO")
                      .ValueGeneratedOnAdd();

                entity.Property(e => e.ItemCode)
                      .HasColumnName("ITEM_CODE")
                      .HasMaxLength(100)
                      .IsUnicode(false)
                      .IsRequired();

                entity.Property(e => e.ItemName)
                      .HasColumnName("ITEM_NAME")
                      .HasMaxLength(200)
                      .IsUnicode(true);

                entity.Property(e => e.ItemCategory)
                      .HasColumnName("ITEM_CATEGORY")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.ItemCost)
                      .HasColumnName("ITEM_COST")
                      .HasColumnType("NUMBER(18,2)");

                entity.Property(e => e.Specification)
                      .HasColumnName("SPECIFICATION")
                      .HasColumnType("NCLOB");

                entity.Property(e => e.VendorName)
                      .HasColumnName("VENDOR_NAME")
                      .HasMaxLength(200)
                      .IsUnicode(true);

                entity.Property(e => e.VendorAddress)
                    .HasColumnName("VENDOR_ADDRESS")
                    .HasMaxLength(1000)
                    .IsUnicode(true);

                entity.Property(e => e.VendorGst)
                      .HasColumnName("VENDOR_GST")
                      .HasMaxLength(50)
                      .IsUnicode(true);

                entity.Property(e => e.PinCode)
                      .HasColumnName("PIN_CODE")
                      .HasMaxLength(20)
                      .IsUnicode(true);

                entity.Property(e => e.BudgetHead)
                      .HasColumnName("BUDGET_HEAD")
                      .HasMaxLength(100)
                      .IsUnicode(true);

                entity.Property(e => e.IsDeleted)
                      .HasColumnName("IS_DELETED")
                      .HasColumnType("CHAR(1)")
                      .HasConversion(boolToCharConverter);
            });

            modelBuilder.Entity<AtmMaster>(entity =>
            {
                entity.ToTable("ATM_MASTER", "PRCRMNT");

                entity.HasKey(e => e.SrNo);

                entity.Property(e => e.SrNo)
                    .HasColumnName("SR_NO")
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.AtmId)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnName("ATM_ID")
                    .IsUnicode(false); // VARCHAR2

                entity.Property(e => e.Col1)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnName("COL_1")
                    .IsUnicode(false);

                entity.Property(e => e.Col2)
                    .HasMaxLength(10)
                    .HasColumnName("COL_2"); // NVARCHAR2 (Unicode by default)

                entity.Property(e => e.Col3)
                    .HasColumnType("NUMBER(18,2)")
                    .HasColumnName("COL_3");

                entity.Property(e => e.TerminalId8Digit)
                    .HasMaxLength(100)
                    .HasColumnName("TERMINAL_ID_8_DIGIT");

                entity.Property(e => e.Project)
                    .HasMaxLength(200)
                    .HasColumnName("PROJECT");

                entity.Property(e => e.InstallationDate)
                    .HasColumnType("DATE")
                    .HasColumnName("INSTALLATION_DATE");

                entity.Property(e => e.AtmMake)
                    .HasMaxLength(100)
                    .HasColumnName("ATM_MAKE");

                entity.Property(e => e.AtmCrm)
                    .HasMaxLength(100)
                    .HasColumnName("ATM_CRM");

                entity.Property(e => e.CapexOpex)
                    .HasMaxLength(100)
                    .HasColumnName("CAPEX_OPEX");

                entity.Property(e => e.CapexOpexOnly)
                    .HasMaxLength(100)
                    .HasColumnName("CAPEX_OPEX_ONLY");

                entity.Property(e => e.SolId)
                    .HasMaxLength(100)
                    .HasColumnName("SOL_ID");

                entity.Property(e => e.BranchCode)
                    .HasMaxLength(100)
                    .HasColumnName("BRANCH_CODE");

                entity.Property(e => e.BranchName)
                    .HasMaxLength(200)
                    .HasColumnName("BRANCH_NAME");

                entity.Property(e => e.Tier)
                    .HasColumnType("NUMBER(18,2)")
                    .HasColumnName("TIER");

                entity.Property(e => e.OffsiteOnsite)
                    .HasMaxLength(100)
                    .HasColumnName("OFFSITE_ONSITE");

                entity.Property(e => e.PopulationCategory)
                    .HasMaxLength(100)
                    .HasColumnName("POPULATION_CATEGORY");

                entity.Property(e => e.Address)
                    .HasMaxLength(1000)
                    .HasColumnName("ADDRESS");

                entity.Property(e => e.District)
                    .HasMaxLength(200)
                    .HasColumnName("DISTRICT");

                entity.Property(e => e.State)
                    .HasMaxLength(200)
                    .HasColumnName("STATE");

                entity.Property(e => e.GeographyWiseRegion)
                    .HasMaxLength(200)
                    .HasColumnName("GEOGRAPHY_WISE_REGION");

                entity.Property(e => e.RegionCode)
                    .HasMaxLength(100)
                    .HasColumnName("REGION_CODE");

                entity.Property(e => e.RegionDesc)
                    .HasMaxLength(200)
                    .HasColumnName("REGION_DESC");

                entity.Property(e => e.ZoneCode)
                    .HasMaxLength(100)
                    .HasColumnName("ZONE_CODE");

                entity.Property(e => e.ZoneDesc)
                    .HasMaxLength(200)
                    .HasColumnName("ZONE_DESC");

                entity.Property(e => e.RbiIssuedDepartment)
                    .HasMaxLength(200)
                    .HasColumnName("RBI_ISSUED_DEPARTMENT");

                entity.Property(e => e.MspFinal)
                    .HasMaxLength(100)
                    .HasColumnName("MSP_FINAL");

                entity.Property(e => e.CashVendor)
                    .HasMaxLength(100)
                    .HasColumnName("CASH_VENDOR");

                entity.Property(e => e.AvgUptime)
                    .HasColumnType("NUMBER(18,2)")
                    .HasColumnName("AVG_UPTIME");

                entity.Property(e => e.AvgHits)
                    .HasColumnType("NUMBER(18,2)")
                    .HasColumnName("AVG_HITS");
            });

        }
    }

}
