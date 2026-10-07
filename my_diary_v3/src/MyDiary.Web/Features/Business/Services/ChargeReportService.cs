using Microsoft.Data.SqlClient;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Business.Models;
using System.Data;

namespace MyDiary.Web.Services
{
    public class ChargeReportService : IChargeReportService
    {
        private readonly SqlDbProvider _db;
        private const string ConnName = "PortalDBConnection";
        private readonly ILogger<ChargeReportService> _logger;

        public ChargeReportService(SqlDbProvider db, ILogger<ChargeReportService> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ── shared helper: upsert master + clear-and-reinsert all details ──
        private async Task<(int reportId, int handingOverId)> UpsertPartAAsync(
            ChargePartAViewModel partA, bool isSubmitted, int knownReportId = 0)
        {
            var dt = await _db.ExecuteStoredProcAsync("dbo.usp_Upsert_Charge_HandingOver",
                new[]
                {
                    new SqlParameter("@EmployeeNo",         partA.EmployeeNo),
                    new SqlParameter("@OfficerName",        partA.OfficerName),
                    new SqlParameter("@Designation",        (object?)partA.Designation        ?? DBNull.Value),
                    new SqlParameter("@IsJointCustodian",   partA.IsJointCustodian),
                    new SqlParameter("@IncomingEmployeeNo", (object?)partA.IncomingEmployeeNo ?? DBNull.Value),
                    new SqlParameter("@DateChargeTaken",    (object?)partA.ChargeTakenDate    ?? DBNull.Value),
                    new SqlParameter("@DateRelieved",       (object?)partA.RelievedDate       ?? DBNull.Value),
                    new SqlParameter("@RelievedBy",         (object?)partA.RelievedBy         ?? DBNull.Value),
                    new SqlParameter("@DeclarationDate",    (object?)partA.DeclarationDate    ?? DBNull.Value),
                    new SqlParameter("@OutgoingSignature",  (object?)partA.OutgoingOfficerSignature ?? DBNull.Value),
                    new SqlParameter("@LastInspectionDate", (object?)partA.LastInspectionDate ?? DBNull.Value),
                    new SqlParameter("@IsSubmitted",        isSubmitted),
                    new SqlParameter("@ReportID",           knownReportId > 0 ? (object)knownReportId : DBNull.Value),
                    new SqlParameter("@RelievedByName", (object?)partA.RelievedByName ?? DBNull.Value),
                    new SqlParameter("@IncomingOfficerName", (object?)partA.IncomingOfficerName ?? DBNull.Value),
                }, ConnName);

            int reportId = Convert.ToInt32(dt.Rows[0]["ReportID"]);
            int handingOverId = Convert.ToInt32(dt.Rows[0]["HandingOverID"]);

            // Clear old detail rows so re-saves never accumulate duplicates
            await _db.ExecuteStoredProcAsync("dbo.usp_Clear_PartA_Details",
                new[] { new SqlParameter("@HandingOverID", handingOverId) }, ConnName);

            return (reportId, handingOverId);
        }

        private async Task<int> UpsertPartBAsync(
            int reportId, ChargePartBViewModel partB)
        {
            var dt = await _db.ExecuteStoredProcAsync("dbo.usp_Upsert_Charge_TakingOver",
                new[]
                {
                    new SqlParameter("@ReportID",            reportId),
                    new SqlParameter("@EmployeeNo",          partB.EmployeeNo),
                    new SqlParameter("@OfficerName",         (object?)partB.OfficerName        ?? DBNull.Value),
                    new SqlParameter("@Designation",         (object?)partB.Designation        ?? DBNull.Value),
                    new SqlParameter("@DateReportedForDuty", (object?)partB.DateReportedForDuty ?? DBNull.Value),
                    new SqlParameter("@DateChargeTaken",     (object?)partB.DateChargeTaken    ?? DBNull.Value),
                    new SqlParameter("@LastInspectionDate",  (object?)partB.LastInspectionDate ?? DBNull.Value),
                    new SqlParameter("@DeclarationDate",     (object?)partB.DeclarationDate    ?? DBNull.Value),
                    new SqlParameter("@IncomingSignature",   (object?)partB.IncomingOfficerSignature ?? DBNull.Value),
                }, ConnName);

            int takingOverId = Convert.ToInt32(dt.Rows[0]["TakingOverID"]);

            // Clear old detail rows
            await _db.ExecuteStoredProcAsync("dbo.usp_Clear_PartB_Details",
                new[] { new SqlParameter("@TakingOverID", takingOverId) }, ConnName);

            return takingOverId;
        }


        public async Task<string?> GetStaffNameByEmplIdAsync(string emplId)
        {
            var dt = await _db.ExecuteQueryAsync(
                "SELECT Name FROM StaffDetails WHERE EMPLID = @EMPLID",
                new[] { new SqlParameter("@EMPLID", emplId) },
                "SQLServerConnection"
            );

            if (dt.Rows.Count == 0)
                return null;

            return dt.Rows[0]["Name"]?.ToString();
        }

        // ── legacy method kept for backward compatibility ──────────────────
        public async Task SavePartAAsync(ChargePartAViewModel partA)
        {
            var (_, handingOverId) = await UpsertPartAAsync(partA, isSubmitted: true);
            await InsertPartADetailsAsync(handingOverId, partA);
        }

        public async Task SavePartBAsync(int reportId, ChargePartBViewModel partB)
        {
            int takingOverId = await UpsertPartBAsync(reportId, partB);
            await InsertPartBDetailsAsync(takingOverId, partB);
            await _db.ExecuteStoredProcAsync("usp_Mark_Charge_TakenOver",
                new[] { new SqlParameter("@ReportID", reportId) }, ConnName);
        }

        public async Task<ActiveChargeContextDto> GetActiveChargeForUserAsync(string employeeNo)
        {
            var dt = await _db.ExecuteStoredProcAsync("usp_Get_Active_Charge_For_User",
                new[] { new SqlParameter("@EmployeeNo", employeeNo) }, ConnName);

            if (dt.Rows.Count == 0)
                return new ActiveChargeContextDto { IsOutgoing = true };

            var r = dt.Rows[0];
            return new ActiveChargeContextDto
            {
                ReportID = r["ReportID"] == DBNull.Value ? null : Convert.ToInt32(r["ReportID"]),
                IsOutgoing = Convert.ToBoolean(r["IsOutgoing"]),
                IsIncoming = Convert.ToBoolean(r["IsIncoming"]),
                IsSubmitted = Convert.ToBoolean(r["IsSubmitted"]),
                IsTakenOver = Convert.ToBoolean(r["IsTakenOver"]),
                IncomingEmployeeNo = r["IncomingEmployeeNo"]?.ToString()
            };
        }

        // ===== DRAFT SAVE =====

        public async Task<int> SaveDraftPartAAsync(ChargePartAViewModel partA, int knownReportId = 0)
        {
            var (reportId, handingOverId) = await UpsertPartAAsync(partA, isSubmitted: false, knownReportId);
            await InsertPartADetailsAsync(handingOverId, partA);
            return reportId;
        }

        public async Task SaveDraftPartBAsync(int reportId, ChargePartBViewModel partB)
        {
            int takingOverId = await UpsertPartBAsync(reportId, partB);
            await InsertPartBDetailsAsync(takingOverId, partB);
        }

        // ===== SUBMIT =====

        public async Task<int> SubmitPartAAsync(ChargePartAViewModel partA, int knownReportId = 0)
        {
            var (reportId, handingOverId) = await UpsertPartAAsync(partA, isSubmitted: true, knownReportId);
            await InsertPartADetailsAsync(handingOverId, partA);
            return reportId;
        }

        public async Task SubmitPartBAsync(int reportId, ChargePartBViewModel partB)
        {
            int takingOverId = await UpsertPartBAsync(reportId, partB);
            await InsertPartBDetailsAsync(takingOverId, partB);
            await _db.ExecuteStoredProcAsync("usp_Mark_Charge_TakenOver",
                new[] { new SqlParameter("@ReportID", reportId) }, ConnName);
        }

        // ===== LOAD DRAFTS =====

        public async Task<ChargePartAViewModel?> LoadDraftPartAAsync(int reportId)
        {
            var ds = await _db.ExecuteStoredProcMultipleAsync("usp_Load_Draft_PartA",
                new[] { new SqlParameter("@ReportID", reportId) }, ConnName);

            // Result set 0 — master scalar row
            if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0) return null;
            var r = ds.Tables[0].Rows[0];

            var model = new ChargePartAViewModel
            {
                OfficerName = r["OfficerName"]?.ToString() ?? "",
                EmployeeNo = r["EmployeeNo"]?.ToString() ?? "",
                Designation = r["Designation"]?.ToString() ?? "",
                IsJointCustodian = r["IsJointCustodian"] is bool b1 && b1,
                ChargeTakenDate = r["DateChargeTaken"] as DateTime?,
                RelievedDate = r["DateRelieved"] as DateTime?,
                RelievedBy = r["RelievedBy"]?.ToString(),
                IncomingEmployeeNo = r["IncomingEmployeeNo"]?.ToString(),
                DeclarationDate = r["DeclarationDate"] as DateTime?,
                OutgoingOfficerSignature = r["OutgoingSignature"]?.ToString(),
                LastInspectionDate = r["LastInspectionDate"] as DateTime?,
                SafeCustodyReceiptAvailable = Convert.ToBoolean(r["SafeCustodyAvailable"]),
                FirstKeyHolder = r["FirstSetHeldBy"]?.ToString(),
                SecondKeyHolder = r["SecondSetHeldBy"]?.ToString(),
                DuplicateKeysLodgedAt = r["DuplicateKeysBranch"]?.ToString(),
                DuplicateKeysLodgedDate = r["DuplicateKeysLodgedDate"] as DateTime?,
                KycPendingComments = r["KycComments"]?.ToString(),
                LoanDocumentException = r["LoanDocException"]?.ToString(),
                TimeBarredDebts = r["TimeBarredDetails"]?.ToString(),
                AuditPendingStatus = r["AuditRemarks"]?.ToString(),
                PermanentFileUpdated = Convert.ToBoolean(r["PermanentFileUpdated"]),
                OtherMatters = r["OtherMatters"]?.ToString(),
                RelievedByName = r["RelievedByName"]?.ToString(),
                IncomingOfficerName = r["IncomingOfficerName"]?.ToString(),
                FirstKeyHolderName = r["FirstSetHeldByName"]?.ToString(),
                SecondKeyHolderName = r["SecondSetHeldByName"]?.ToString(),
            };

            // Result set 1 — documents
            model.Documents = ds.Tables.Count > 1
                ? ds.Tables[1].AsEnumerable().Select(row => new PartADocumentVm
                {
                    DocumentName = row["DocumentName"]?.ToString() ?? "",
                    IsAvailable = Convert.ToBoolean(row["IsAvailable"])
                }).ToList()
                : new();

            int basePdfIndex = ds.Tables.Count - 6;

            // Section 3
            LoadPdf(ds, basePdfIndex, p => {
                model.AdhocPdfFileName = p.FileName;
                model.AdhocPdfContentType = p.ContentType;
                model.AdhocPdfData = p.Data;
            });

            // Section 4
            LoadPdf(ds, basePdfIndex + 1, p => {
                model.ReviewPendingPdfFileName = p.FileName;
                model.ReviewPendingPdfContentType = p.ContentType;
                model.ReviewPendingPdfData = p.Data;
            });

            // Section 5
            LoadPdf(ds, basePdfIndex + 2, p => {
                model.SeizedDocsPdfFileName = p.FileName;
                model.SeizedDocsPdfContentType = p.ContentType;
                model.SeizedDocsPdfData = p.Data;
            });

            // Section 6
            LoadPdf(ds, basePdfIndex + 3, p => {
                model.ServiceChargePdfFileName = p.FileName;
                model.ServiceChargePdfContentType = p.ContentType;
                model.ServiceChargePdfData = p.Data;
            });

            // Section 7
            LoadPdf(ds, basePdfIndex + 4, p => {
                model.KycPendingPdfFileName = p.FileName;
                model.KycPendingPdfContentType = p.ContentType;
                model.KycPendingPdfData = p.Data;
            });

            // ✅ Section 9
            LoadPdf(ds, basePdfIndex + 5, p => {
                model.TimeBarredDebtsPdfFileName = p.FileName;
                model.TimeBarredDebtsPdfContentType = p.ContentType;
                model.TimeBarredDebtsPdfData = p.Data;
            });

            return model;
        }

        private void LoadPdf(DataSet ds, int index, Action<(string? FileName, string? ContentType, byte[]? Data)> assign)
        {
            if (index >= 0 && index < ds.Tables.Count && ds.Tables[index].Rows.Count > 0)
            {
                var r = ds.Tables[index].Rows[0];
                assign((
                    r["FileName"]?.ToString(),
                    r["ContentType"]?.ToString(),
                    r["FileData"] as byte[]
                ));
            }
        }


        public async Task<ChargePartBViewModel?> LoadDraftPartBAsync(int reportId)
        {
            var ds = await _db.ExecuteStoredProcMultipleAsync("usp_Load_Draft_PartB",
                new[] { new SqlParameter("@ReportID", reportId) }, ConnName);

            // Result set 0 — master scalar row
            if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0) return null;
            var r = ds.Tables[0].Rows[0];

            var model = new ChargePartBViewModel
            {
                OfficerName = r["OfficerName"]?.ToString() ?? "",
                EmployeeNo = r["EmployeeNo"]?.ToString() ?? "",
                Designation = r["Designation"]?.ToString() ?? "",
                DateReportedForDuty = r["DateReportedForDuty"] as DateTime?,
                DateChargeTaken = r["DateChargeTaken"] as DateTime?,
                LastInspectionDate = r["LastInspectionDate"] as DateTime?,
                DeclarationDate = r["DeclarationDate"] as DateTime?,
                IncomingOfficerSignature = r["IncomingSignature"]?.ToString() ?? "",
                SafeCustodyReceiptAvailable = Convert.ToBoolean(r["SafeCustodyAvailable"]),
                FirstKeyHolder = r["FirstSetHeldBy"]?.ToString() ?? "",
                FirstKeyHolderName = r["FirstSetHeldByName"]?.ToString() ?? "",
                SecondKeyHolder = r["SecondSetHeldBy"]?.ToString() ?? "",
                SecondKeyHolderName = r["SecondSetHeldByName"]?.ToString() ?? "",
                DuplicateKeysLodgedAt = r["DuplicateKeysBranch"]?.ToString() ?? "",
                DuplicateKeysLodgedDate = r["DuplicateKeysLodgedDate"] as DateTime?,
                VehicleDocumentsInOrder = Convert.ToBoolean(r["VehicleDocsInOrder"]),
                InsuranceRenewalDueDate = r["InsuranceRenewalDueDate"] as DateTime?,
                GoldBagCountMatches = Convert.ToBoolean(r["GoldBagCountMatches"]),
                LockerKeysProperlyHeld = Convert.ToBoolean(r["LockerKeysProperlyHeld"]),
                GunLicenseVerified = Convert.ToBoolean(r["GunLicenseVerified"]),
                GunDiscrepancies = r["GunDiscrepancies"]?.ToString() ?? "",
                CashTalliesWithRegister = Convert.ToBoolean(r["CashTallied"]),
                PermanentFileUpdated = Convert.ToBoolean(r["PermanentFileUpdated"]),
                OtherMatters = r["OtherMatters"]?.ToString() ?? "",

            };

            // Result set 1 — documents
            model.Documents = ds.Tables.Count > 1
                ? ds.Tables[1].AsEnumerable().Select(row => new ChargePartBViewModel.PartBDocumentVm
                {
                    DocumentName = row["DocumentName"]?.ToString() ?? "",
                    IsAvailable = Convert.ToBoolean(row["IsAvailable"])
                }).ToList()
                : new();

            // ================= PART‑B PDF RESULT SETS (FIXED ORDER) =================

            // Index 9 — Section 3: Customer Complaints PDF
            LoadPdf(ds, 9, p =>
            {
                model.CustomerComplaintsPdfFileName = p.FileName;
                model.CustomerComplaintsPdfContentType = p.ContentType;
                model.CustomerComplaintsPdfData = p.Data;
            });

            // Index 10 — Section 4: Consumer Cases PDF
            LoadPdf(ds, 10, p =>
            {
                model.ConsumerCasesPdfFileName = p.FileName;
                model.ConsumerCasesPdfContentType = p.ContentType;
                model.ConsumerCasesPdfData = p.Data;
            });

            // Index 11 — Section 5: Time Barred Accounts PDF
            LoadPdf(ds, 11, p =>
            {
                model.TimeBarredAccountsPdfFileName = p.FileName;
                model.TimeBarredAccountsPdfContentType = p.ContentType;
                model.TimeBarredAccountsPdfData = p.Data;
            });

            // Index 12 — Section 6: Capital Assets PDF
            LoadPdf(ds, 12, p =>
            {
                model.CapitalAssetsPdfFileName = p.FileName;
                model.CapitalAssetsPdfContentType = p.ContentType;
                model.CapitalAssetsPdfData = p.Data;
            });

            // Index 13 — Section 7: Sundry Balances PDF
            LoadPdf(ds, 13, p =>
            {
                model.SundryBalancesPdfFileName = p.FileName;
                model.SundryBalancesPdfContentType = p.ContentType;
                model.SundryBalancesPdfData = p.Data;
            });

            // Index 14 — Section 9: Godown Keys PDF
            LoadPdf(ds, 14, p =>
            {
                model.GodownKeysPdfFileName = p.FileName;
                model.GodownKeysPdfContentType = p.ContentType;
                model.GodownKeysPdfData = p.Data;
            });


            LoadPdf(ds, 15, p =>
            {
                model.GoldDiscrepanciesPdfFileName = p.FileName;
                model.GoldDiscrepanciesPdfContentType = p.ContentType;
                model.GoldDiscrepanciesPdfData = p.Data;
            });



            // Result set 7 — godown keys
            model.GodownKeysVerification = ds.Tables.Count > 7
                ? ds.Tables[7].AsEnumerable().Select(row => new PartBAccountVm
                {
                    AccountNo = row["AccountNo"]?.ToString(),
                    BorrowerName = row["BorrowerName"]?.ToString(),
                    Remarks = row["Remarks"]?.ToString()
                }).ToList()
                : new();

            // Result set 8 — gold discrepancies
            model.GoldDiscrepancies = ds.Tables.Count > 8
                ? ds.Tables[8].AsEnumerable().Select(row => new PartBAccountVm
                {
                    AccountNo = row["AccountNo"]?.ToString(),
                    BorrowerName = row["BorrowerName"]?.ToString(),
                    Remarks = row["Remarks"]?.ToString()
                }).ToList()
                : new();

            // Lists not stored separately (initialise empty so UI doesn't null-ref)
            model.RandomJLDiscrepancies ??= new();

            return model;
        }

        // ===== DETAIL INSERT HELPERS =====
        // These are called after the master row is upserted and old details cleared.

        private async Task InsertPartADetailsAsync(int handingOverId, ChargePartAViewModel partA)
        {
            await SavePartADocumentsAsync(handingOverId, partA);
            await SavePartASafeCustodyAsync(handingOverId, partA);
            await SavePartADoubleLockAsync(handingOverId, partA);
            await SavePartADuplicateKeysAsync(handingOverId, partA);
            //await SavePartAReviewPendingAsync(handingOverId, partA);
            //await SavePartASeizedDocsAsync(handingOverId, partA);
            //await SavePartAServiceChargesAsync(handingOverId, partA);
            //await SavePartAKycAsync(handingOverId, partA);
            await SavePartATimeBarredAsync(handingOverId, partA);
            await SavePartAAuditAsync(handingOverId, partA);
            await SavePartAPermanentFileAsync(handingOverId, partA);
            await SavePartAOtherMattersAsync(handingOverId, partA);
            //await SavePartAAdhocAccountsAsync(handingOverId, partA);
            await SavePartAAdhocPdfAsync(handingOverId, partA);
            await SavePartAReviewPendingPdfAsync(handingOverId, partA);
            await SavePartASeizedDocumentsPdfAsync(handingOverId, partA); // legacy rows
            await SavePartAServiceChargePdfAsync(handingOverId, partA);
            await SavePartAKycPendingPdfAsync(handingOverId, partA);
            await SavePartATimeBarredDebtsPdfAsync(handingOverId, partA);
        }

        private async Task InsertPartBDetailsAsync(int takingOverId, ChargePartBViewModel partB)
        {
            await SavePartBDocumentsAsync(takingOverId, partB);

            await SavePartBSafeCustodyAsync(takingOverId, partB);
            await SavePartBDoubleLockAsync(takingOverId, partB);
            await SavePartBDuplicateKeysAsync(takingOverId, partB);
            //await SavePartBCustomerComplaintsAsync(takingOverId, partB);
            //await SavePartBConsumerCasesAsync(takingOverId, partB);
            //await SavePartBTimeBarredAccountsAsync(takingOverId, partB);
            //await SavePartBCapitalAssetsAsync(takingOverId, partB);
            //await SavePartBSundryBalancesAsync(takingOverId, partB);
            await SavePartBVehicleDocumentsAsync(takingOverId, partB);
            //await SavePartBGodownKeysAsync(takingOverId, partB);
            await SavePartBGoldVerificationAsync(takingOverId, partB);
            //await SavePartBGoldDiscrepanciesAsync(takingOverId, partB);
            await SavePartBLockerKeysAsync(takingOverId, partB);
            await SavePartBGunLicenseAsync(takingOverId, partB);
            await SavePartBCashVerificationAsync(takingOverId, partB);
            await SavePartBPermanentFileAsync(takingOverId, partB);
            await SavePartBOtherMattersAsync(takingOverId, partB);


            await SavePartBCustomerComplaintsPdfAsync(takingOverId, partB);
            await SavePartBConsumerCasesPdfAsync(takingOverId, partB);
            await SavePartBTimeBarredAccountsPdfAsync(takingOverId, partB);
            await SavePartBCapitalAssetsPdfAsync(takingOverId, partB);
            await SavePartBSundryBalancesPdfAsync(takingOverId, partB);
            await SavePartBGodownKeysPdfAsync(takingOverId, partB);
            await SavePartBGoldDiscrepanciesPdfAsync(takingOverId, partB);

        }

        // ===== LEGACY PRIVATE HELPERS (kept for SavePartAAsync / SavePartBAsync) =====

        private async Task SavePartBGoldDiscrepanciesPdfAsync(
    int takingOverId,
    ChargePartBViewModel model)
        {
            if (model.GoldDiscrepanciesPdfData == null || model.GoldDiscrepanciesPdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartB_GoldDiscrepanciesPdf",
                new[]
                {
            new SqlParameter("@TakingOverID", takingOverId),
            new SqlParameter("@FileName", model.GoldDiscrepanciesPdfFileName),
            new SqlParameter("@ContentType", model.GoldDiscrepanciesPdfContentType),
            new SqlParameter("@FileData", model.GoldDiscrepanciesPdfData)
                },
                ConnName
            );
        }

        private async Task SavePartBCustomerComplaintsPdfAsync(int takingOverId, ChargePartBViewModel model)
        {
            if (model.CustomerComplaintsPdfData == null) return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartB_CustomerComplaintsPdf",
                new[]
                {
            new SqlParameter("@TakingOverID", takingOverId),
            new SqlParameter("@FileName", model.CustomerComplaintsPdfFileName),
            new SqlParameter("@ContentType", model.CustomerComplaintsPdfContentType),
            new SqlParameter("@FileData", model.CustomerComplaintsPdfData)
                }, ConnName);
        }

        private async Task SavePartBConsumerCasesPdfAsync(
    int takingOverId,
    ChargePartBViewModel model)
        {
            if (model.ConsumerCasesPdfData == null || model.ConsumerCasesPdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartB_ConsumerCasesPdf",
                new[]
                {
            new SqlParameter("@TakingOverID", takingOverId),
            new SqlParameter("@FileName", model.ConsumerCasesPdfFileName),
            new SqlParameter("@ContentType", model.ConsumerCasesPdfContentType),
            new SqlParameter("@FileData", model.ConsumerCasesPdfData)
                },
                ConnName
            );
        }

        private async Task SavePartBTimeBarredAccountsPdfAsync(
    int takingOverId,
    ChargePartBViewModel model)
        {
            if (model.TimeBarredAccountsPdfData == null || model.TimeBarredAccountsPdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartB_TimeBarredAccountsPdf",
                new[]
                {
            new SqlParameter("@TakingOverID", takingOverId),
            new SqlParameter("@FileName", model.TimeBarredAccountsPdfFileName),
            new SqlParameter("@ContentType", model.TimeBarredAccountsPdfContentType),
            new SqlParameter("@FileData", model.TimeBarredAccountsPdfData)
                },
                ConnName
            );
        }
        private async Task SavePartBCapitalAssetsPdfAsync(
    int takingOverId,
    ChargePartBViewModel model)
        {
            if (model.CapitalAssetsPdfData == null || model.CapitalAssetsPdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartB_CapitalAssetsPdf",
                new[]
                {
            new SqlParameter("@TakingOverID", takingOverId),
            new SqlParameter("@FileName", model.CapitalAssetsPdfFileName),
            new SqlParameter("@ContentType", model.CapitalAssetsPdfContentType),
            new SqlParameter("@FileData", model.CapitalAssetsPdfData)
                },
                ConnName
            );
        }

        private async Task SavePartBGodownKeysPdfAsync(
    int takingOverId,
    ChargePartBViewModel model)
        {
            if (model.GodownKeysPdfData == null || model.GodownKeysPdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartB_GodownKeysPdf",
                new[]
                {
            new SqlParameter("@TakingOverID", takingOverId),
            new SqlParameter("@FileName", model.GodownKeysPdfFileName),
            new SqlParameter("@ContentType", model.GodownKeysPdfContentType),
            new SqlParameter("@FileData", model.GodownKeysPdfData)
                },
                ConnName
            );
        }
        private async Task SavePartBSundryBalancesPdfAsync(
    int takingOverId,
    ChargePartBViewModel model)
        {
            if (model.SundryBalancesPdfData == null || model.SundryBalancesPdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartB_SundryBalancesPdf",
                new[]
                {
            new SqlParameter("@TakingOverID", takingOverId),
            new SqlParameter("@FileName", model.SundryBalancesPdfFileName),
            new SqlParameter("@ContentType", model.SundryBalancesPdfContentType),
            new SqlParameter("@FileData", model.SundryBalancesPdfData)
                },
                ConnName
            );
        }



        private async Task SavePartATimeBarredDebtsPdfAsync(
    int handingOverId,
    ChargePartAViewModel partA)
        {
            if (partA.TimeBarredDebtsPdfData == null || partA.TimeBarredDebtsPdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartA_TimeBarredDebtsPdf",
                new[]
                {
            new SqlParameter("@HandingOverID", handingOverId),
            new SqlParameter("@FileName", partA.TimeBarredDebtsPdfFileName),
            new SqlParameter("@ContentType", partA.TimeBarredDebtsPdfContentType),
            new SqlParameter("@FileData", partA.TimeBarredDebtsPdfData)
                },
                ConnName
            );
        }
        private async Task SavePartAKycPendingPdfAsync(
    int handingOverId,
    ChargePartAViewModel partA)
        {
            if (partA.KycPendingPdfData == null || partA.KycPendingPdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartA_KycPendingPdf",
                new[]
                {
            new SqlParameter("@HandingOverID", handingOverId),
            new SqlParameter("@FileName", partA.KycPendingPdfFileName),
            new SqlParameter("@ContentType", partA.KycPendingPdfContentType),
            new SqlParameter("@FileData", partA.KycPendingPdfData)
                },
                ConnName
            );
        }

        private async Task SavePartAServiceChargePdfAsync(
    int handingOverId,
    ChargePartAViewModel partA)
        {
            if (partA.ServiceChargePdfData == null || partA.ServiceChargePdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartA_ServiceChargePdf",
                new[]
                {
            new SqlParameter("@HandingOverID", handingOverId),
            new SqlParameter("@FileName", partA.ServiceChargePdfFileName),
            new SqlParameter("@ContentType", partA.ServiceChargePdfContentType),
            new SqlParameter("@FileData", partA.ServiceChargePdfData)
                },
                ConnName
            );
        }

        private async Task SavePartASeizedDocumentsPdfAsync(
    int handingOverId,
    ChargePartAViewModel partA)
        {
            if (partA.SeizedDocsPdfData == null || partA.SeizedDocsPdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartA_SeizedDocumentsPdf",
                new[]
                {
            new SqlParameter("@HandingOverID", handingOverId),
            new SqlParameter("@FileName", partA.SeizedDocsPdfFileName),
            new SqlParameter("@ContentType", partA.SeizedDocsPdfContentType),
            new SqlParameter("@FileData", partA.SeizedDocsPdfData)
                },
                ConnName
            );
        }
        private async Task SavePartAReviewPendingPdfAsync(
    int handingOverId,
    ChargePartAViewModel partA)
        {
            if (partA.ReviewPendingPdfData == null || partA.ReviewPendingPdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartA_ReviewPendingPdf",
                new[]
                {
            new SqlParameter("@HandingOverID", handingOverId),
            new SqlParameter("@FileName", partA.ReviewPendingPdfFileName),
            new SqlParameter("@ContentType", partA.ReviewPendingPdfContentType),
            new SqlParameter("@FileData", partA.ReviewPendingPdfData)
                },
                ConnName
            );
        }
        private async Task SavePartAAdhocPdfAsync(int handingOverId, ChargePartAViewModel partA)
        {
            if (partA.AdhocPdfData == null || partA.AdhocPdfData.Length == 0)
                return;

            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartA_AdhocODPdf",
                new[]
                {
            new SqlParameter("@HandingOverID", handingOverId),
            new SqlParameter("@FileName", partA.AdhocPdfFileName),
            new SqlParameter("@ContentType", partA.AdhocPdfContentType),
            new SqlParameter("@FileData", partA.AdhocPdfData)
                },
                ConnName
            );
        }

        private async Task SavePartADocumentsAsync(int handingOverId, ChargePartAViewModel partA)
        {
            foreach (var d in partA.Documents)
                await _db.ExecuteStoredProcAsync("dbo.usp_Save_Charge_PartA_DocumentsAvailability",
                    new[] { new SqlParameter("@HandingOverID", handingOverId), new SqlParameter("@DocumentName", d.DocumentName), new SqlParameter("@IsAvailable", d.IsAvailable) }, ConnName);
        }

        private async Task SavePartASafeCustodyAsync(int handingOverId, ChargePartAViewModel partA)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartA_SafeCustody",
                new[] { new SqlParameter("@HandingOverID", handingOverId), new SqlParameter("@IsAvailable", partA.SafeCustodyReceiptAvailable) }, ConnName);
        }

        private async Task SavePartADoubleLockAsync(int handingOverId, ChargePartAViewModel partA)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartA_DoubleLockKeys",
                new[]
                {
                    new SqlParameter("@HandingOverID", handingOverId),
                    new SqlParameter("@FirstSetHeldBy", string.IsNullOrWhiteSpace(partA.FirstKeyHolder) ? DBNull.Value : partA.FirstKeyHolder),
                    new SqlParameter("@SecondSetHeldBy", string.IsNullOrWhiteSpace(partA.SecondKeyHolder) ? DBNull.Value : partA.SecondKeyHolder),
                    new SqlParameter("@FirstSetHeldByName", (object?)partA.FirstKeyHolderName ?? DBNull.Value),
                    new SqlParameter("@SecondSetHeldByName", (object?)partA.SecondKeyHolderName ?? DBNull.Value),
                }, ConnName);
        }

        private async Task SavePartADuplicateKeysAsync(int handingOverId, ChargePartAViewModel partA)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartA_DuplicateKeysLodged",
                new[] { new SqlParameter("@HandingOverID", handingOverId), new SqlParameter("@Branch", partA.DuplicateKeysLodgedAt), new SqlParameter("@LodgedDate", (object?)partA.DuplicateKeysLodgedDate ?? DBNull.Value) }, ConnName);
        }

        private async Task SavePartAReviewPendingAsync(int handingOverId, ChargePartAViewModel partA)
        {
            foreach (var a in partA.ReviewPendingAccounts)
                await _db.ExecuteStoredProcAsync("usp_Save_PartA_ReviewPendingAccounts",
                    new[] { new SqlParameter("@HandingOverID", handingOverId), new SqlParameter("@AccountNo", a.AccountNo), new SqlParameter("@BorrowerName", a.BorrowerName), new SqlParameter("@Remarks", a.Remarks) }, ConnName);
        }

        private async Task SavePartASeizedDocsAsync(int handingOverId, ChargePartAViewModel partA)
        {
            foreach (var s in partA.SeizedDocuments)
                await _db.ExecuteStoredProcAsync("usp_Save_PartA_SeizedDocuments",
                    new[] { new SqlParameter("@HandingOverID", handingOverId), new SqlParameter("@AccountNo", s.AccountNo), new SqlParameter("@BorrowerName", s.BorrowerName), new SqlParameter("@Remarks", s.Remarks) }, ConnName);
        }

        private async Task SavePartAServiceChargesAsync(int handingOverId, ChargePartAViewModel partA)
        {
            foreach (var sc in partA.ServiceChargeConcessions)
                await _db.ExecuteStoredProcAsync("usp_Save_PartA_ServiceChargeConcessions",
                    new[] { new SqlParameter("@HandingOverID", handingOverId), new SqlParameter("@PartyName", sc.PartyName), new SqlParameter("@ConcessionDetails", sc.ConcessionDetails), new SqlParameter("@Remarks", sc.Remarks) }, ConnName);
        }

        private async Task SavePartAKycAsync(int handingOverId, ChargePartAViewModel partA)
        {
            if (!string.IsNullOrWhiteSpace(partA.KycPendingComments))
                await _db.ExecuteStoredProcAsync("usp_Save_PartA_KYCPending",
                    new[] { new SqlParameter("@HandingOverID", handingOverId), new SqlParameter("@Comments", partA.KycPendingComments) }, ConnName);
        }

        private async Task SavePartATimeBarredAsync(int handingOverId, ChargePartAViewModel partA)
        {
            if (!string.IsNullOrWhiteSpace(partA.TimeBarredDebts))
                await _db.ExecuteStoredProcAsync("usp_Save_PartA_TimeBarredDebts",
                    new[] { new SqlParameter("@HandingOverID", handingOverId), new SqlParameter("@Details", partA.TimeBarredDebts) }, ConnName);
        }

        private async Task SavePartAAuditAsync(int handingOverId, ChargePartAViewModel partA)
        {
            if (!string.IsNullOrWhiteSpace(partA.AuditPendingStatus))
                await _db.ExecuteStoredProcAsync("usp_Save_PartA_AuditReportsPending",
                    new[] { new SqlParameter("@HandingOverID", handingOverId), new SqlParameter("@Remarks", partA.AuditPendingStatus) }, ConnName);
        }

        private async Task SavePartAPermanentFileAsync(int handingOverId, ChargePartAViewModel partA)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartA_PermanentFileStatus",
                new[] { new SqlParameter("@HandingOverID", handingOverId), new SqlParameter("@IsUpdated", partA.PermanentFileUpdated) }, ConnName);
        }

        private async Task SavePartAOtherMattersAsync(int handingOverId, ChargePartAViewModel partA)
        {
            if (!string.IsNullOrWhiteSpace(partA.OtherMatters))
                await _db.ExecuteStoredProcAsync("usp_Save_PartA_OtherMatters",
                    new[] { new SqlParameter("@HandingOverID", handingOverId), new SqlParameter("@Remarks", partA.OtherMatters) }, ConnName);
        }

        private async Task SavePartAAdhocAccountsAsync(int handingOverId, ChargePartAViewModel partA)
        {
            foreach (var a in partA.AdhocAccounts)
                await _db.ExecuteStoredProcAsync("dbo.usp_Save_PartA_AdhocODAccounts",
                    new[]
                    {
                        new SqlParameter("@HandingOverID", handingOverId),
                        new SqlParameter("@AccountNo", (object?)a.AccountNo ?? DBNull.Value),
                        new SqlParameter("@BorrowerName", (object?)a.BorrowerName ?? DBNull.Value),
                        new SqlParameter("@Remarks", (object?)a.Remarks ?? DBNull.Value)
                    }, ConnName);
        }

        // ===== PART B DETAIL METHODS =====
        private async Task SavePartBDocumentsAsync(int takingOverId, ChargePartBViewModel partB)
        {
            foreach (var doc in partB.Documents)
                await _db.ExecuteStoredProcAsync("usp_Save_PartB_DocumentsAvailability",
                    new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@DocumentName", doc.DocumentName), new SqlParameter("@IsAvailable", doc.IsAvailable) }, ConnName);
        }

        private async Task SavePartBSafeCustodyAsync(int takingOverId, ChargePartBViewModel partB)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartB_SafeCustodyReceipt",
                new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@IsAvailable", partB.SafeCustodyReceiptAvailable) }, ConnName);
        }

        private async Task SavePartBDoubleLockAsync(
    int takingOverId,
    ChargePartBViewModel partB)
        {
            await _db.ExecuteStoredProcAsync(
                "usp_Save_PartB_DoubleLockKeys",
                new[]
                {
            new SqlParameter("@TakingOverID", takingOverId),
            new SqlParameter("@FirstSetHeldBy", partB.FirstKeyHolder),
            new SqlParameter("@FirstSetHeldByName",
                (object?)partB.FirstKeyHolderName ?? DBNull.Value),
            new SqlParameter("@SecondSetHeldBy", partB.SecondKeyHolder),
            new SqlParameter("@SecondSetHeldByName",
                (object?)partB.SecondKeyHolderName ?? DBNull.Value),
                },
                ConnName
            );
        }

        private async Task SavePartBDuplicateKeysAsync(int takingOverId, ChargePartBViewModel partB)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartB_DoubleLockKeys",
    new[]
    {
        new SqlParameter("@TakingOverID", takingOverId),
        new SqlParameter("@FirstSetHeldBy", partB.FirstKeyHolder),
        new SqlParameter("@FirstSetHeldByName", (object?)partB.FirstKeyHolderName ?? DBNull.Value),
        new SqlParameter("@SecondSetHeldBy", partB.SecondKeyHolder),
        new SqlParameter("@SecondSetHeldByName", (object?)partB.SecondKeyHolderName ?? DBNull.Value)
    }, ConnName);
        }

        private async Task SavePartBCustomerComplaintsAsync(int takingOverId, ChargePartBViewModel partB)
        {
            foreach (var c in partB.CustomerComplaints)
                await _db.ExecuteStoredProcAsync("usp_Save_PartB_CustomerComplaints",
                    new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@ComplainantName", c.ComplainantName), new SqlParameter("@ComplaintDate", c.ComplaintDate), new SqlParameter("@Remarks", c.Remarks) }, ConnName);
        }

        private async Task SavePartBConsumerCasesAsync(int takingOverId, ChargePartBViewModel partB)
        {
            foreach (var c in partB.ConsumerCases)
                await _db.ExecuteStoredProcAsync("usp_Save_PartB_ConsumerCases",
                    new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@ComplainantName", c.ComplainantName), new SqlParameter("@ComplaintDate", c.ComplaintDate), new SqlParameter("@Remarks", c.Remarks) }, ConnName);
        }

        private async Task SavePartBTimeBarredAccountsAsync(int takingOverId, ChargePartBViewModel partB)
        {
            foreach (var a in partB.TimeBarredAccounts)
                await _db.ExecuteStoredProcAsync("usp_Save_PartB_TimeBarredAccounts",
                    new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@AccountNo", a.AccountNo), new SqlParameter("@BorrowerName", a.BorrowerName), new SqlParameter("@Remarks", a.Remarks) }, ConnName);
        }

        private async Task SavePartBCapitalAssetsAsync(int takingOverId, ChargePartBViewModel partB)
        {
            foreach (var a in partB.CapitalAssets)
                await _db.ExecuteStoredProcAsync("usp_Save_PartB_CapitalAssets",
                    new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@AssetDescription", a.AssetDescription), new SqlParameter("@Discrepancy", a.Discrepancy), new SqlParameter("@Remarks", a.Remarks) }, ConnName);
        }

        private async Task SavePartBSundryBalancesAsync(int takingOverId, ChargePartBViewModel partB)
        {
            foreach (var s in partB.SundryBalances)
                await _db.ExecuteStoredProcAsync("usp_Save_PartB_SundryBalances",
                    new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@EntryDate", s.EntryDate), new SqlParameter("@Amount", s.Amount), new SqlParameter("@Particulars", s.Particulars) }, ConnName);
        }

        private async Task SavePartBVehicleDocumentsAsync(int takingOverId, ChargePartBViewModel partB)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartB_VehicleDocuments",
                new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@IsInOrder", partB.VehicleDocumentsInOrder), new SqlParameter("@InsuranceRenewalDueDate", (object?)partB.InsuranceRenewalDueDate ?? DBNull.Value) }, ConnName);
        }

        private async Task SavePartBGodownKeysAsync(int takingOverId, ChargePartBViewModel partB)
        {
            foreach (var g in partB.GodownKeysVerification)
                await _db.ExecuteStoredProcAsync("usp_Save_PartB_GodownKeys",
                    new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@AccountNo", g.AccountNo), new SqlParameter("@BorrowerName", g.BorrowerName), new SqlParameter("@Remarks", g.Remarks) }, ConnName);
        }

        private async Task SavePartBGoldVerificationAsync(int takingOverId, ChargePartBViewModel partB)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartB_GoldVerification",
                new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@BagCountMatches", partB.GoldBagCountMatches) }, ConnName);
        }

        private async Task SavePartBGoldDiscrepanciesAsync(int takingOverId, ChargePartBViewModel partB)
        {
            foreach (var g in partB.GoldDiscrepancies)
                await _db.ExecuteStoredProcAsync("usp_Save_PartB_GoldDiscrepancies",
                    new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@AccountNo", g.AccountNo), new SqlParameter("@BorrowerName", g.BorrowerName), new SqlParameter("@Remarks", g.Remarks) }, ConnName);
        }

        private async Task SavePartBLockerKeysAsync(int takingOverId, ChargePartBViewModel partB)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartB_LockerKeys",
                new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@IsProperlyHeld", partB.LockerKeysProperlyHeld) }, ConnName);
        }

        private async Task SavePartBGunLicenseAsync(int takingOverId, ChargePartBViewModel partB)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartB_GunLicense",
                new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@IsVerified", partB.GunLicenseVerified), new SqlParameter("@Discrepancies", partB.GunDiscrepancies) }, ConnName);
        }

        private async Task SavePartBCashVerificationAsync(int takingOverId, ChargePartBViewModel partB)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartB_CashVerification",
                new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@IsTallied", partB.CashTalliesWithRegister) }, ConnName);
        }

        private async Task SavePartBPermanentFileAsync(int takingOverId, ChargePartBViewModel partB)
        {
            await _db.ExecuteStoredProcAsync("usp_Save_PartB_PermanentFileStatus",
                new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@IsUpdated", partB.PermanentFileUpdated) }, ConnName);
        }

        private async Task SavePartBOtherMattersAsync(int takingOverId, ChargePartBViewModel partB)
        {
            if (!string.IsNullOrWhiteSpace(partB.OtherMatters))
                await _db.ExecuteStoredProcAsync("usp_Save_PartB_OtherMatters",
                    new[] { new SqlParameter("@TakingOverID", takingOverId), new SqlParameter("@Remarks", partB.OtherMatters) }, ConnName);
        }
    }
}
