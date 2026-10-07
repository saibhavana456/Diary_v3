using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyDiary.Core.Services;
using MyDiary.Web.Features.Procurement.Data;
using MyDiary.Web.Features.Procurement.Interfaces;
using MyDiary.Web.Features.Procurement.Models.TABLES;
using MyDiary.Web.Features.Procurement.Models.VIEWS;
using Oracle.ManagedDataAccess.Client;
using static MyDiary.Web.Features.Procurement.Models.DTO.MiscClasses;

namespace MyDiary.Web.Features.Procurement.Services
{
    public class ATMIndentService : IATMIndentService
    {
        private readonly ProcurementDbContext _dbContext;
        private readonly SmtpSettings _smtp;
        private readonly string _OraganizationConnString;

        public ATMIndentService(ProcurementDbContext dbContext, IOptions<SmtpSettings> iOptions, IConfiguration config)
        {
            _dbContext = dbContext;
            _smtp = iOptions.Value;
            _OraganizationConnString = config.GetConnectionString("OrganizationConnection")!;
        }

        public async Task<string> DeleteATMIndentMaster(DeleteRequestDto requestMaster)
        {
            try
            {
                if (requestMaster == null)
                    return "Invalid payload.";

                var existing = await _dbContext.ATM_REQUEST_MASTER
                    .FirstOrDefaultAsync(e => e.RequestCode == requestMaster.RequestCode);
                existing.ISDELETED = true;
                existing.DeletedBy = requestMaster.DeletedBy;
                existing.DeletedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                var childItems = await _dbContext.REQUEST_ATM.Where(x => x.RequestCode == requestMaster.RequestCode).ToListAsync();

                foreach (var item in childItems)
                {
                    item.IsDeleted = true;
                    item.DeletedBy = requestMaster.DeletedBy;
                    item.DeletedOn = DateTime.UtcNow;
                }
                await _dbContext.SaveChangesAsync();
                return $"Deleted Successfully.";
            }
            catch (DbUpdateException dbEx)
            {
                return $"Delete failed: {dbEx.InnerException?.Message ?? dbEx.Message}";
            }
            catch (Exception ex)
            {
                return $"Delete failed (unexpected): {ex.Message}";
            }
        }

        public async Task<List<ATMRequestMaster>> GetAllATMIndentData(string Code, string userRole)
        {
            if (userRole == "MAKER" || userRole == "CHECKER")
            {

                return await _dbContext.ATM_REQUEST_MASTER
                    .AsNoTracking()
                    .Where(emp => emp.ISDELETED != true && emp.BranchCode == Code)
                    .OrderByDescending(emp => emp.RaisedOn)
                    .Select(e => new ATMRequestMaster
                    {
                        RequestCode = e.RequestCode,
                        BranchCode = e.BranchCode,
                        Justification = e.Justification,
                        CoRecommendation = e.CoRecommendation,
                        CoCheckerRecommendation = e.CoCheckerRecommendation,
                        //ZoneRecommendation = e.ZoneRecommendation,
                        RegionRecommendation = e.RegionRecommendation,
                        RegionApproverRecommendation = e.RegionApproverRecommendation,
                        BranchRecommendation = e.BranchRecommendation,
                        CoRemarks = e.CoRemarks,
                        CoCheckerRemarks = e.CoCheckerRemarks,
                        //ZoneRemarks = e.ZoneRemarks,
                        RegionRemarks = e.RegionRemarks,
                        RegionApproverRemarks = e.RegionApproverRemarks,
                        RegionCheckerName = e.RegionCheckerName,
                        BranchRemarks = e.BranchRemarks,
                        RaisedOn = e.RaisedOn,
                        RegionApproverName = e.RegionApproverName,
                        //ZoneApproverName = e.ZoneApproverName,
                        CoApproverName = e.CoApproverName,
                        CoCheckerName = e.CoApproverName,
                        CheckerApproverName = e.CheckerApproverName,
                        Feedback = e.Feedback,

                        TotalItemsCount = _dbContext.REQUEST_ATM
            .Count(child => child.RequestCode == e.RequestCode && child.IsDeleted != true),

                        PaidItemsCount = _dbContext.REQUEST_ATM
            .Count(child => child.RequestCode == e.RequestCode && child.IsDeleted != true
                  && child.PaymentBy != null && child.PaymentBy != ""),

                        TotalProcurementCost = _dbContext.REQUEST_ATM
                        .Where(child => child.RequestCode == e.RequestCode && child.IsDeleted != true)
                        .Sum(child => (decimal?)child.CostBeforeTax) ?? 0

                    })
                    .ToListAsync();
            }
            else if (userRole == "ROMAKER" || userRole == "ROAPPROVER")
            {
                var branchCodes = new List<string>();

                using (OracleConnection conn = new OracleConnection(_OraganizationConnString))
                {
                    await conn.OpenAsync();
                    using (OracleCommand cmd = conn.CreateCommand())
                    {
                        // The SQL query based on your requirement
                        cmd.CommandText = @"SELECT DISTINCT BRANCH_CODE 
                                FROM BRANCH_MASTER 
                                WHERE REGION_CODE = :regionCode";

                        cmd.Parameters.Add(new OracleParameter("regionCode", OracleDbType.Varchar2) { Value = Code });

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var code = reader["BRANCH_CODE"] == DBNull.Value
                                           ? ""
                                           : reader["BRANCH_CODE"].ToString();

                                if (!string.IsNullOrEmpty(code))
                                {
                                    branchCodes.Add(code);
                                }
                            }
                        }
                    }
                }
                return await _dbContext.ATM_REQUEST_MASTER
                    .AsNoTracking()
                    .Where(emp => emp.ISDELETED != true
                    //&& emp.BranchCode == "530816"
                    && branchCodes.Contains(emp.BranchCode)
                    )
                    .OrderByDescending(emp => emp.RaisedOn)
                    .Select(e => new ATMRequestMaster
                    {
                        RequestCode = e.RequestCode,
                        BranchCode = e.BranchCode,
                        Justification = e.Justification,
                        CoRecommendation = e.CoRecommendation,
                        CoCheckerRecommendation = e.CoCheckerRecommendation,
                        //ZoneRecommendation = e.ZoneRecommendation,
                        RegionRecommendation = e.RegionRecommendation,
                        RegionApproverRecommendation = e.RegionApproverRecommendation,
                        BranchRecommendation = e.BranchRecommendation,
                        CoRemarks = e.CoRemarks,
                        CoCheckerRemarks = e.CoCheckerRemarks,
                        //ZoneRemarks = e.ZoneRemarks,
                        RegionRemarks = e.RegionRemarks,
                        RegionApproverRemarks = e.RegionApproverRemarks,
                        RegionCheckerName = e.RegionCheckerName,
                        BranchRemarks = e.BranchRemarks,
                        RaisedOn = e.RaisedOn,
                        RegionApproverName = e.RegionApproverName,
                        //ZoneApproverName = e.ZoneApproverName,
                        CoApproverName = e.CoApproverName,
                        CoCheckerName = e.CoApproverName,
                        CheckerApproverName = e.CheckerApproverName,
                        Feedback = e.Feedback,

                        TotalItemsCount = _dbContext.REQUEST_ATM
                        .Count(child => child.RequestCode == e.RequestCode && child.IsDeleted != true),

                        PaidItemsCount = _dbContext.REQUEST_ATM
                        .Count(child => child.RequestCode == e.RequestCode && child.IsDeleted != true
                              && child.PaymentBy != null && child.PaymentBy != ""),

                        TotalProcurementCost = _dbContext.REQUEST_ATM
                                    .Where(child => child.RequestCode == e.RequestCode && child.IsDeleted != true)
                                    .Sum(child => (decimal?)child.CostBeforeTax) ?? 0

                    })
                    .ToListAsync();
            }
            else if (userRole == "ZOMAKER" || userRole == "ZOAPPROVER")
            {
                var branchCodes = new List<string>();

                using (OracleConnection conn = new OracleConnection(_OraganizationConnString))
                {
                    await conn.OpenAsync();
                    using (OracleCommand cmd = conn.CreateCommand())
                    {
                        // The SQL query based on your requirement
                        cmd.CommandText = @"SELECT DISTINCT BRANCH_CODE 
                                FROM BRANCH_MASTER 
                                WHERE ZONE_CODE = :zoneCode";

                        cmd.Parameters.Add(new OracleParameter("zoneCode", OracleDbType.Varchar2) { Value = Code });

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var code = reader["BRANCH_CODE"] == DBNull.Value
                                           ? ""
                                           : reader["BRANCH_CODE"].ToString();

                                if (!string.IsNullOrEmpty(code))
                                {
                                    branchCodes.Add(code);
                                }
                            }
                        }
                    }
                }
                return await _dbContext.ATM_REQUEST_MASTER
                    .AsNoTracking()
                    .Where(emp => emp.ISDELETED != true
                    //&& emp.BranchCode == "530816"
                    && branchCodes.Contains(emp.BranchCode)
                    )
                    .OrderByDescending(emp => emp.RaisedOn)
                    .Select(e => new ATMRequestMaster
                    {
                        RequestCode = e.RequestCode,
                        BranchCode = e.BranchCode,
                        Justification = e.Justification,
                        CoRecommendation = e.CoRecommendation,
                        CoCheckerRecommendation = e.CoCheckerRecommendation,
                        //ZoneRecommendation = e.ZoneRecommendation,
                        RegionRecommendation = e.RegionRecommendation,
                        RegionApproverRecommendation = e.RegionApproverRecommendation,
                        BranchRecommendation = e.BranchRecommendation,
                        CoRemarks = e.CoRemarks,
                        CoCheckerRemarks = e.CoCheckerRemarks,
                        //ZoneRemarks = e.ZoneRemarks,
                        RegionRemarks = e.RegionRemarks,
                        RegionApproverRemarks = e.RegionApproverRemarks,
                        RegionCheckerName = e.RegionCheckerName,
                        BranchRemarks = e.BranchRemarks,
                        RaisedOn = e.RaisedOn,
                        RegionApproverName = e.RegionApproverName,
                        //ZoneApproverName = e.ZoneApproverName,
                        CoApproverName = e.CoApproverName,
                        CoCheckerName = e.CoApproverName,
                        CheckerApproverName = e.CheckerApproverName,
                        Feedback = e.Feedback,

                        TotalItemsCount = _dbContext.REQUEST_ATM
                        .Count(child => child.RequestCode == e.RequestCode && child.IsDeleted != true),

                        PaidItemsCount = _dbContext.REQUEST_ATM
                        .Count(child => child.RequestCode == e.RequestCode && child.IsDeleted != true
                              && child.PaymentBy != null && child.PaymentBy != ""),

                        TotalProcurementCost = _dbContext.REQUEST_ATM
                                    .Where(child => child.RequestCode == e.RequestCode && child.IsDeleted != true)
                                    .Sum(child => (decimal?)child.CostBeforeTax) ?? 0

                    })
                    .ToListAsync();
            }
            else if (userRole == "COMAKER" || userRole == "COAPPROVER")
            {

                return await _dbContext.ATM_REQUEST_MASTER
                    .AsNoTracking()
                    .Where(emp => emp.ISDELETED != true)
                    .OrderByDescending(emp => emp.RaisedOn)
                    .Select(e => new ATMRequestMaster
                    {
                        RequestCode = e.RequestCode,
                        BranchCode = e.BranchCode,
                        Justification = e.Justification,
                        CoRecommendation = e.CoRecommendation,
                        CoCheckerRecommendation = e.CoCheckerRecommendation,
                        //ZoneRecommendation = e.ZoneRecommendation,
                        RegionRecommendation = e.RegionRecommendation,
                        RegionApproverRecommendation = e.RegionApproverRecommendation,
                        BranchRecommendation = e.BranchRecommendation,
                        CoRemarks = e.CoRemarks,
                        CoCheckerRemarks = e.CoCheckerRemarks,
                        //ZoneRemarks = e.ZoneRemarks,
                        RegionRemarks = e.RegionRemarks,
                        RegionApproverRemarks = e.RegionApproverRemarks,
                        RegionCheckerName = e.RegionCheckerName,
                        BranchRemarks = e.BranchRemarks,
                        RaisedOn = e.RaisedOn,
                        RegionApproverName = e.RegionApproverName,
                        //ZoneApproverName = e.ZoneApproverName,
                        CoApproverName = e.CoApproverName,
                        CoCheckerName = e.CoApproverName,
                        CheckerApproverName = e.CheckerApproverName,
                        Feedback = e.Feedback,

                        TotalItemsCount = _dbContext.REQUEST_ATM
            .Count(child => child.RequestCode == e.RequestCode && child.IsDeleted != true),

                        PaidItemsCount = _dbContext.REQUEST_ATM
            .Count(child => child.RequestCode == e.RequestCode && child.IsDeleted != true
                  && child.PaymentBy != null && child.PaymentBy != ""),

                        TotalProcurementCost = _dbContext.REQUEST_ATM
                        .Where(child => child.RequestCode == e.RequestCode && child.IsDeleted != true)
                        .Sum(child => (decimal?)child.CostBeforeTax) ?? 0

                    })
                    .ToListAsync();
            }

            return new List<ATMRequestMaster>();
        }

        public async Task<dynamic> GetAllATMIndentDataById(string id)
        {
            try
            {


                var master = await _dbContext.ATM_REQUEST_MASTER
                    .AsNoTracking()
                    .Where(e => e.RequestCode == id && e.ISDELETED != true)
                    .Include(e => e.RequestATM!)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync();

                if (master is null) return null;
                var totalItemsCount = _dbContext.REQUEST_ATM
          .Count(child => child.RequestCode == id && child.IsDeleted != true);

                var paidItemsCount = _dbContext.REQUEST_ATM
    .Count(child => child.RequestCode == id && child.IsDeleted != true
          && child.PaymentBy != null && child.PaymentBy != "");

                var result = new
                {
                    master.SR_NO,
                    master.RequestCode,
                    master.BranchCode,
                    master.ATMId,
                    master.FinYear,
                    master.Month,
                    master.FinacleId,
                    master.Justification,
                    master.UrgencyLevel,
                    master.Attachment,
                    master.AttachmentName,
                    master.RaisedBy,
                    master.RaisedOn,
                    master.BranchRecommendation,
                    master.BranchRemarks,
                    master.CheckerApproverName,
                    master.BranchStatusDate,
                    master.RegionRecommendation,
                    master.RegionRemarks,
                    master.RegionStatusDate,
                    master.RegionForwardTo,
                    master.RegionApproverPfNo,
                    master.RegionApproverName,
                    master.RegionCheckerName,
                    master.RegionApproverRecommendation,
                    master.RegionApproverRemarks,
                    //master.ZoneRecommendation,
                    //master.ZoneRemarks,
                    //master.ZoneStatusDate,
                    //master.ZoneApprovedQuantity,
                    //master.ZoneForwardTo,
                    //master.ZoneApproverPfNo,
                    //master.ZoneApproverName,
                    master.CoRecommendation,
                    master.CoRemarks,
                    master.CoStatusDate,
                    master.CoApprovedQuantity,
                    master.CoForwardTo,
                    master.CoApproverPfNo,
                    master.CoApproverName,
                    master.CoCheckerRecommendation,
                    master.CoCheckerRemarks,
                    master.CoCheckerName,
                    master.ISDELETED,
                    master.DeletedBy,
                    master.DeletedOn,
                    master.IsPoIssued,
                    master.PoIssuedBy,
                    master.PoIssuedOn,
                    master.IsPoAccepted,
                    master.PoAcceptedBy,
                    master.PoAcceptedOn,
                    master.PoAcceptedDoc,
                    master.PoAcceptedDocName,
                    master.PoRefno,
                    master.Feedback,
                    TotalItemsCount = totalItemsCount,
                    PaidItemsCount = paidItemsCount,
                    //IsPaymentCompleted = (totalItems > 0 && totalItems == paidItems),

                    // Items
                    RequestATM = (master.RequestATM ?? new List<RequestATM>())
                    .Where(i => i.IsDeleted != true)
                        .GroupBy(i => i.SR_NO)
                        .Select(g =>
                        {
                            var i = g.First();

                            return new
                            {
                                SR_NO = i.SR_NO,
                                AtmCode = i.AtmCode,
                                QUANTITY = i.Quantity,
                                CostBeforeTax = i.CostBeforeTax,
                                Remarks = i.Remarks,
                                IsPOIssued = i.IsPOIssued,
                                POIssuedBy = i.POIssuedBy,
                                POIssuedOn = i.POIssuedOn,
                                IsPoAccepted = i.IsPoAccepted,
                                PoAcceptedBy = i.PoAcceptedBy,
                                PoAcceptedOn = i.PoAcceptedOn,
                                OrderedBy = i.OrderedBy,
                                OrderedOn = i.OrderedOn,
                                PreBookedAmt = i.PreBookedAmt,
                                ReceivedBy = i.ReceivedBy,
                                DeliveredOn = i.DeliveredOn,
                                PostDeliveredAmt = i.PostDeliveredAmt,
                                InstalledOn = i.InstalledOn,
                                PostInstalledAmt = i.PostInstalledAmt,
                                ApprovedQuantity = i.ApprovedQuantity,
                                PoGeneratedDoc = i.PoGeneratedDoc,
                                PoGeneratedDocName = i.PoGeneratedDocName,
                                PoAcceptedDoc = i.PoAcceptedDoc,
                                PoAcceptedDocName = i.PoAcceptedDocName,
                                PreBookedDoc = i.PreBookedDoc,
                                PreBookedDocName = i.PreBookedDocName,
                                OrderRecivedDoc = i.OrderRecivedDoc,
                                OrderRecivedDocName = i.OrderRecivedDocName,
                                PostInstallationDoc = i.PostInstallationDoc,
                                PostInstallationDocName = i.PostInstallationDocName,
                                PaymentDate = i.PaymentDate,
                                InvoiceNumber = i.InvoiceNumber,
                                PaymentBy = i.PaymentBy,
                                InvoiceDoc = i.InvoiceDoc,
                                InvoiceDocName = i.InvoiceDocName,


                            };
                        })
                        .ToList()
                };

                return result;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        public async Task<List<AtmMaster>> GetAllATMMasterData()
        {
            return await _dbContext.ATM_MASTER
                       .AsNoTracking()
                       .Select(e => new AtmMaster
                       {
                           AtmId = e.AtmId,
                           Project = e.Project,
                           InstallationDate = e.InstallationDate,
                           AtmMake = e.AtmMake,
                           AtmCrm = e.AtmCrm,
                           CapexOpex = e.CapexOpex,
                           BranchCode = e.BranchCode

                       })
                       .ToListAsync();
        }

        public async Task<AtmMaster> GetAtmMasterById(string id)
        {
            return await _dbContext.ATM_MASTER
                        .AsNoTracking()
                        .Where(emp => emp.AtmId == id)
                        .Select(e => new AtmMaster
                        {
                            TerminalId8Digit = e.TerminalId8Digit,
                            Project = e.Project,
                            InstallationDate = e.InstallationDate,
                            AtmMake = e.AtmMake,
                            AtmCrm = e.AtmCrm,
                            CapexOpex = e.CapexOpex,
                            CapexOpexOnly = e.CapexOpexOnly,
                            AvgUptime = e.AvgUptime,
                            AvgHits = e.AvgHits
                        })
                        .FirstOrDefaultAsync();

        }

        public async Task<string> InsertOrUpdateATMIndent(ATMRequestMaster requestMaster, string Role)
        {
            if (requestMaster == null) return "Invalid payload.";

            _dbContext.ChangeTracker.Clear();
            await using var tx = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                if (requestMaster.RequestCode != null)
                {
                    // --- UPDATE LOGIC ---
                    var existing = await _dbContext.ATM_REQUEST_MASTER
                                                   .FirstOrDefaultAsync(x => x.RequestCode == requestMaster.RequestCode);

                    if (existing == null) return "Record not found.";

                    // Apply Role-Based updates to Master
                    UpdateMasterFieldsByRole(existing, requestMaster, Role);

                    // Handle Child Items (RequestATM)
                    if (Role == "Maker")
                    {
                        var childItems = await _dbContext.REQUEST_ATM
                                                        .Where(x => x.RequestCode == requestMaster.RequestCode)
                                                        .ToListAsync();

                        foreach (var item in childItems)
                        {
                            item.IsDeleted = true;
                            item.DeletedBy = requestMaster.DeletedBy;
                            item.DeletedOn = DateTime.UtcNow;
                        }

                        foreach (var objRequestATM in requestMaster.RequestATM)
                        {
                            _dbContext.REQUEST_ATM.Add(new RequestATM
                            {
                                AtmCode = objRequestATM.AtmCode,
                                RequestCode = requestMaster.RequestCode,
                                Quantity = objRequestATM.Quantity,
                                CostBeforeTax = objRequestATM.CostBeforeTax,
                                Remarks = objRequestATM.Remarks
                            });
                        }
                    }
                    else if (Role == "ROMAKER" || Role == "ROApprover")
                    {
                        // Snapshot the list to prevent enumeration errors
                        var incomingItems = requestMaster.RequestATM.ToList();
                        foreach (var objRequestATM in incomingItems)
                        {
                            var dbItem = await _dbContext.REQUEST_ATM
                                .FirstOrDefaultAsync(x => x.RequestCode == requestMaster.RequestCode
                                                       && x.AtmCode == objRequestATM.AtmCode
                                                       && x.IsDeleted != true);

                            if (dbItem != null)
                            {
                                if (objRequestATM.CostBeforeTax != null) dbItem.CostBeforeTax = objRequestATM.CostBeforeTax;
                                if (objRequestATM.ApprovedQuantity != null) dbItem.ApprovedQuantity = objRequestATM.ApprovedQuantity;
                            }
                        }
                    }

                    await _dbContext.SaveChangesAsync();
                    await tx.CommitAsync();
                    return "Record Updated Successfully.";
                }
                else
                {
                    var currentCount = await _dbContext.ATM_REQUEST_MASTER.Where(x => x.BranchCode == requestMaster.BranchCode)
                        .CountAsync();

                    var nextSuffix = currentCount + 1;
                    string RequestCode = $"INDATM{requestMaster.BranchCode}{nextSuffix:D4}";

                    requestMaster.RequestCode = RequestCode;
                    requestMaster.RaisedOn = DateTime.UtcNow;

                    // Important: Remove child items from the master object temporarily 
                    // to prevent EF from trying to insert them twice
                    var itemsToAdd = requestMaster.RequestATM.ToList();
                    requestMaster.RequestATM = null!;

                    _dbContext.ATM_REQUEST_MASTER.Add(requestMaster);
                    await _dbContext.SaveChangesAsync(); // Save Master first to get PK integrity

                    foreach (var item in itemsToAdd)
                    {
                        item.RequestCode = RequestCode;
                        _dbContext.REQUEST_ATM.Add(item);
                    }

                    await _dbContext.SaveChangesAsync(); // Save all children at once
                    await tx.CommitAsync();
                    return "Record Saved Successfully.";
                }
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return $"Operation failed: {ex.Message}";
            }
        }

        // Helper to keep the main method clean
        private void UpdateMasterFieldsByRole(ATMRequestMaster existing, ATMRequestMaster update, string Role)
        {
            switch (Role)
            {
                case "Maker":
                    existing.BranchCode = update.BranchCode;
                    existing.FinYear = update.FinYear;
                    existing.Month = update.Month;
                    existing.FinacleId = update.FinacleId;
                    existing.ATMId = update.ATMId;
                    existing.Justification = update.Justification;
                    existing.UrgencyLevel = update.UrgencyLevel;
                    existing.RaisedOn = DateTime.UtcNow;
                    existing.BranchRecommendation = null;
                    existing.BranchRemarks = null;
                    existing.BranchStatusDate = null;
                    existing.CheckerApproverName = null;


                    existing.RegionRecommendation = null;
                    existing.RegionRemarks = null;
                    existing.RegionStatusDate = null;
                    existing.RegionCheckerName = null;

                    existing.RegionApproverRecommendation = null;
                    existing.RegionApproverRemarks = null;
                    existing.RegionApproverDate = null;
                    existing.RegionForwardTo = null;
                    existing.RegionApproverName = null;

                    existing.CoCheckerRecommendation = null;
                    existing.CoCheckerRemarks = null;
                    existing.CoCheckerName = null;
                    existing.CoStatusDate = null;

                    existing.CoRecommendation = null;
                    existing.CoRemarks = null;
                    existing.CoApproverDate = null;
                    existing.CoApproverName = null;
                    break;
                case "Checker":
                    existing.BranchRecommendation = update.BranchRecommendation;
                    existing.BranchRemarks = update.BranchRemarks;
                    existing.BranchStatusDate = DateTime.UtcNow;
                    existing.CheckerApproverName = update.CheckerApproverName;
                    if (update.BranchRecommendation == "Return to Requestor")
                    {
                        existing.RegionRecommendation = null;
                        existing.RegionRemarks = null;
                        existing.RegionStatusDate = null;
                        existing.RegionCheckerName = null;

                        existing.RegionApproverRecommendation = null;
                        existing.RegionApproverRemarks = null;
                        existing.RegionApproverDate = null;
                        existing.RegionForwardTo = null;
                        existing.RegionApproverName = null;

                        existing.CoCheckerRecommendation = null;
                        existing.CoCheckerRemarks = null;
                        existing.CoCheckerName = null;
                        existing.CoStatusDate = null;

                        existing.CoRecommendation = null;
                        existing.CoRemarks = null;
                        existing.CoApproverDate = null;
                        existing.CoApproverName = null;
                    }
                    break;
                case "ROMAKER":
                    existing.RegionRecommendation = update.RegionRecommendation;
                    existing.RegionRemarks = update.RegionRemarks;
                    existing.RegionStatusDate = DateTime.UtcNow;
                    existing.RegionCheckerName = update.RegionCheckerName;

                    if (update.RegionRecommendation == "Return to Requestor")
                    {
                        existing.BranchRecommendation = null;
                        existing.BranchRemarks = null;
                        existing.BranchStatusDate = null;
                        existing.CheckerApproverName = null;

                        existing.RegionApproverRecommendation = null;
                        existing.RegionApproverRemarks = null;
                        existing.RegionApproverDate = null;
                        existing.RegionForwardTo = null;
                        existing.RegionApproverName = null;

                        existing.CoCheckerRecommendation = null;
                        existing.CoCheckerRemarks = null;
                        existing.CoCheckerName = null;
                        existing.CoStatusDate = null;

                        existing.CoRecommendation = null;
                        existing.CoRemarks = null;
                        existing.CoApproverDate = null;
                        existing.CoApproverName = null;
                    }
                    break;
                case "ROApprover":
                    existing.RegionApproverRecommendation = update.RegionApproverRecommendation;
                    existing.RegionApproverRemarks = update.RegionApproverRemarks;
                    existing.RegionApproverDate = DateTime.UtcNow;
                    existing.RegionForwardTo = update.RegionForwardTo;
                    existing.RegionApproverName = update.RegionApproverName;


                    if (update.RegionApproverRecommendation == "Return to Requestor")
                    {
                        existing.BranchRecommendation = null;
                        existing.BranchRemarks = null;
                        existing.BranchStatusDate = null;
                        existing.CheckerApproverName = null;

                        existing.RegionRecommendation = null;
                        existing.RegionRemarks = null;
                        existing.RegionStatusDate = null;
                        existing.RegionCheckerName = null;

                        existing.CoCheckerRecommendation = null;
                        existing.CoCheckerRemarks = null;
                        existing.CoCheckerName = null;
                        existing.CoStatusDate = null;

                        existing.CoRecommendation = null;
                        existing.CoRemarks = null;
                        existing.CoApproverDate = null;
                        existing.CoApproverName = null;
                    }
                    break;

                //case "ZO":
                //    existing.ZoneRecommendation = update.ZoneRecommendation;
                //    existing.ZoneRemarks = update.ZoneRemarks;
                //    existing.ZoneStatusDate = DateTime.UtcNow;
                //    existing.ZoneForwardTo = update.ZoneForwardTo;
                //    existing.ZoneApproverPfNo = update.ZoneApproverPfNo;
                //    existing.ZoneApproverName = update.ZoneApproverName;
                //    break;

                case "ZOMAKER":
                    existing.CoCheckerRecommendation = update.CoCheckerRecommendation;
                    existing.CoCheckerRemarks = update.CoCheckerRemarks;
                    existing.CoCheckerName = update.CoCheckerName;
                    existing.CoStatusDate = DateTime.UtcNow;

                    if (update.CoCheckerRecommendation == "Return to Requestor")
                    {
                        existing.BranchRecommendation = null;
                        existing.BranchRemarks = null;
                        existing.BranchStatusDate = null;
                        existing.CheckerApproverName = null;


                        existing.RegionRecommendation = null;
                        existing.RegionRemarks = null;
                        existing.RegionStatusDate = null;
                        existing.RegionCheckerName = null;

                        existing.RegionApproverRecommendation = null;
                        existing.RegionApproverRemarks = null;
                        existing.RegionApproverDate = null;
                        existing.RegionForwardTo = null;
                        existing.RegionApproverName = null;

                        existing.CoRecommendation = null;
                        existing.CoRemarks = null;
                        existing.CoApproverDate = null;
                        existing.CoApproverName = null;
                    }
                    break;

                case "ZOAPPROVER":
                    existing.CoRecommendation = update.CoRecommendation;
                    existing.CoRemarks = update.CoRemarks;
                    existing.CoApproverDate = DateTime.UtcNow;
                    existing.CoApprovedQuantity = update.CoApprovedQuantity;
                    existing.CoApproverPfNo = update.CoApproverPfNo;
                    existing.CoApproverName = update.CoApproverName;

                    if (update.CoRecommendation == "Return to Requestor")
                    {
                        existing.BranchRecommendation = null;
                        existing.BranchRemarks = null;
                        existing.BranchStatusDate = null;
                        existing.CheckerApproverName = null;


                        existing.RegionRecommendation = null;
                        existing.RegionRemarks = null;
                        existing.RegionStatusDate = null;
                        existing.RegionCheckerName = null;

                        existing.RegionApproverRecommendation = null;
                        existing.RegionApproverRemarks = null;
                        existing.RegionApproverDate = null;
                        existing.RegionForwardTo = null;
                        existing.RegionApproverName = null;

                        existing.CoCheckerRecommendation = null;
                        existing.CoCheckerRemarks = null;
                        existing.CoCheckerName = null;
                        existing.CoStatusDate = null;

                    }
                    break;
            }
        }

        public async Task<UserMasterView> GetUserDetailsViewByUserId(string userId)
        {
            try
            {
                var result = await _dbContext.UserMasterView
                        .Where(x => x.PF_NO == userId)
                        .FirstOrDefaultAsync();

                return result;
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        //public async Task<string> InsertOrUpdateATMIndent(ATMRequestMaster requestMaster, string Role)
        //{
        //    if (requestMaster == null)
        //        return "Invalid payload.";
        //    _dbContext.ChangeTracker.Clear();
        //    await using var tx = await _dbContext.Database.BeginTransactionAsync();
        //    if (requestMaster.RequestCode != null)
        //    {
        //        try
        //        {
        //            var existing = await _dbContext.ATM_REQUEST_MASTER
        //                                       .FirstOrDefaultAsync(x => x.RequestCode == requestMaster.RequestCode);
        //            if (Role == "Maker")
        //            {
        //                existing.BranchCode = requestMaster.BranchCode;
        //                existing.FinYear = requestMaster.FinYear;
        //                existing.Month = requestMaster.Month;
        //                existing.FinacleId = requestMaster.FinacleId;
        //                existing.ATMId = requestMaster.ATMId;
        //                existing.Attachment = requestMaster.Attachment;
        //                existing.AttachmentName = requestMaster.AttachmentName;
        //                existing.Justification = requestMaster.Justification;
        //                existing.UrgencyLevel = requestMaster.UrgencyLevel;
        //                existing.RaisedBy = requestMaster.RaisedBy;
        //                existing.RaisedOn = DateTime.UtcNow;
        //            }
        //            else if (Role == "Checker")
        //            {
        //                existing.BranchRecommendation = requestMaster.BranchRecommendation;
        //                existing.BranchRemarks = requestMaster.BranchRemarks;
        //                existing.CheckerApproverName = requestMaster.CheckerApproverName;
        //                existing.BranchStatusDate = DateTime.UtcNow;
        //            }
        //            else if (Role == "ROMAKER")
        //            {
        //                existing.RegionRecommendation = requestMaster.RegionRecommendation;
        //                existing.RegionRemarks = requestMaster.RegionRemarks;
        //                existing.RegionStatusDate = DateTime.UtcNow;
        //                existing.RegionApproverPfNo = requestMaster.RegionApproverPfNo;
        //                existing.RegionApproverName = requestMaster.RegionApproverName;
        //            }
        //            else if(Role== "ROApprover")
        //            {
        //                existing.RegionApproverRecommendation = requestMaster.RegionApproverRecommendation;
        //                existing.RegionApproverRemarks = requestMaster.RegionApproverRemarks;
        //                existing.RegionForwardTo = requestMaster.RegionForwardTo;
        //                existing.RegionApproverName = requestMaster.RegionApproverName;
        //                existing.RegionApproverDate = DateTime.UtcNow;
        //            }
        //            else if (Role == "ZO")
        //            {
        //                existing.ZoneRecommendation = requestMaster.ZoneRecommendation;
        //                existing.ZoneRemarks = requestMaster.ZoneRemarks;
        //                existing.ZoneStatusDate = DateTime.UtcNow;
        //                existing.ZoneApprovedQuantity = requestMaster.ZoneApprovedQuantity;
        //                existing.ZoneForwardTo = requestMaster.ZoneForwardTo;
        //                existing.ZoneApproverPfNo = requestMaster.ZoneApproverPfNo;
        //                existing.ZoneApproverName = requestMaster.ZoneApproverName;
        //            }
        //            else if (Role == "COMaker")
        //            {
        //                existing.CoCheckerRecommendation = requestMaster.CoCheckerRecommendation;
        //                existing.CoCheckerRemarks = requestMaster.CoCheckerRemarks;
        //                existing.CoCheckerName = requestMaster.CoCheckerName;
        //                existing.CoStatusDate = DateTime.UtcNow;
        //            }
        //            else if (Role == "COApprover")
        //            {
        //                existing.CoRecommendation = requestMaster.CoRecommendation;
        //                existing.CoRemarks = requestMaster.CoRemarks;
        //                existing.CoApproverDate = DateTime.UtcNow;
        //                existing.CoApprovedQuantity = requestMaster.CoApprovedQuantity;
        //                existing.CoApproverPfNo = requestMaster.CoApproverPfNo;
        //                existing.CoApproverName = requestMaster.CoApproverName;
        //            }



        //            await _dbContext.SaveChangesAsync();
        //            if (Role == "Maker")
        //            {
        //                var childItems = await _dbContext.REQUEST_ATM
        //                                        .Where(x => x.RequestCode == requestMaster.RequestCode)
        //                                        .ToListAsync();

        //                if (childItems.Count > 0)
        //                {
        //                    foreach (var item in childItems)
        //                    {
        //                        item.IsDeleted = true;
        //                        item.DeletedBy = requestMaster.DeletedBy;
        //                        item.DeletedOn = DateTime.UtcNow;
        //                    }
        //                    await _dbContext.SaveChangesAsync();
        //                }
        //                foreach (RequestATM objRequestATM in requestMaster.RequestATM)
        //                {
        //                    RequestATM SaveRequestATM = new RequestATM();
        //                    SaveRequestATM.AtmCode = objRequestATM.AtmCode;
        //                    SaveRequestATM.RequestCode = requestMaster.RequestCode;
        //                    SaveRequestATM.Quantity = objRequestATM.Quantity;
        //                    SaveRequestATM.CostBeforeTax = objRequestATM.CostBeforeTax;
        //                    SaveRequestATM.Remarks = objRequestATM.Remarks;
        //                    _dbContext.REQUEST_ATM.Add(SaveRequestATM);
        //                    await _dbContext.SaveChangesAsync();

        //                }
        //            }
        //            else if(Role == "ROMAKER" || Role == "ROApprover")
        //            {
        //                foreach (RequestATM objRequestATM in requestMaster.RequestATM)
        //                {
        //                    var childItems = await _dbContext.REQUEST_ATM
        //                                        .Where(x => x.RequestCode == requestMaster.RequestCode 
        //                                        && x.AtmCode== objRequestATM.AtmCode
        //                                        && x.IsDeleted!=true)
        //                                        .FirstOrDefaultAsync();
        //                    if (childItems != null) // 1. Null check to prevent the crash
        //                    {
        //                        if (objRequestATM.CostBeforeTax != null)
        //                        {
        //                            childItems.CostBeforeTax = objRequestATM.CostBeforeTax;
        //                        }
        //                        if (objRequestATM.ApprovedQuantity != null)
        //                        {
        //                            childItems.ApprovedQuantity = objRequestATM.ApprovedQuantity;
        //                        }

        //                    }
        //                }
        //                await _dbContext.SaveChangesAsync();

        //            }
        //            await tx.CommitAsync();
        //            return "Record Updated Successfully.";
        //        }
        //        catch (Exception ex)
        //        {
        //            return $"Update failed (unexpected): {ex.Message}";
        //        }
        //    }
        //    else
        //    {
        //        try
        //        {
        //            var nextSuffix = await _dbContext.ATM_REQUEST_MASTER.CountAsync() + 1;
        //            string RequestCode = $"REQ_{nextSuffix}";
        //            requestMaster.RequestCode = RequestCode;
        //            requestMaster.RaisedOn = DateTime.UtcNow;
        //            _dbContext.ATM_REQUEST_MASTER.Add(requestMaster);
        //            await _dbContext.SaveChangesAsync();
        //            foreach (RequestATM objRequestATM in requestMaster.RequestATM)
        //            {


        //                var existingItem = await _dbContext.REQUEST_ATM
        //                        .FirstOrDefaultAsync(x => x.RequestCode == RequestCode &&
        //                                                   x.Quantity == objRequestATM.Quantity &&
        //                                                   x.CostBeforeTax == objRequestATM.CostBeforeTax &&
        //                                                  x.AtmCode == objRequestATM.AtmCode &&
        //                                                  x.Remarks == objRequestATM.Remarks);

        //                if (existingItem is null)
        //                {
        //                    var newItem = new RequestATM
        //                    {
        //                        RequestCode = RequestCode,
        //                        AtmCode = objRequestATM.AtmCode,
        //                        Quantity = objRequestATM.Quantity,
        //                        CostBeforeTax = objRequestATM.CostBeforeTax,
        //                        Remarks = objRequestATM.Remarks
        //                    };
        //                    _dbContext.REQUEST_ATM.Add(newItem);
        //                }

        //                await _dbContext.SaveChangesAsync();

        //            }
        //            await tx.CommitAsync();
        //            return "Record Saved Successfully.";
        //        }
        //        catch (Exception ex)
        //        {
        //            await tx.RollbackAsync();
        //            return $"Insert failed (unexpected): {ex.Message}";
        //        }
        //    }
        //}

        public async Task<string> InsertPODetails(ATMRequestMaster objATMRequestMaster)
        {
            try
            {
                var existing = await _dbContext.ATM_REQUEST_MASTER
                                                 .Where(x => x.RequestCode == objATMRequestMaster.RequestCode && x.ISDELETED != true)
                                                 .FirstOrDefaultAsync();
                if (existing != null)
                {
                    existing.IsPoIssued = objATMRequestMaster.IsPoIssued;
                    existing.PoIssuedBy = objATMRequestMaster.PoIssuedBy;
                    existing.PoIssuedOn = objATMRequestMaster.PoIssuedOn;
                    existing.IsPoAccepted = objATMRequestMaster.IsPoAccepted;
                    existing.PoAcceptedBy = objATMRequestMaster.PoAcceptedBy;
                    existing.PoAcceptedOn = objATMRequestMaster.PoAcceptedOn;
                    existing.PoRefno = objATMRequestMaster.PoRefno;
                    if (objATMRequestMaster.PoAcceptedDoc != null)
                    {
                        existing.PoAcceptedDoc = objATMRequestMaster.PoAcceptedDoc;
                        existing.PoAcceptedDocName = objATMRequestMaster.PoAcceptedDocName;
                    }
                    await _dbContext.SaveChangesAsync();

                    return "Success";
                }
                return "No data found";
            }
            catch (Exception ex)
            {
                return $"Update failed (unexpected): {ex.Message}";
            }
        }


        public async Task<string> InsertFeedbackDetails(ATMRequestMaster objATMRequestMaster)
        {
            try
            {
                var existing = await _dbContext.ATM_REQUEST_MASTER
                                                 .Where(x => x.RequestCode == objATMRequestMaster.RequestCode && x.ISDELETED != true)
                                                 .FirstOrDefaultAsync();
                if (existing != null)
                {
                    existing.Feedback = objATMRequestMaster.Feedback;
                    existing.FeedbackBy = objATMRequestMaster.FeedbackBy;
                    existing.FeedbackDate = objATMRequestMaster.FeedbackDate;
                    await _dbContext.SaveChangesAsync();

                    return "Success";
                }
                return "No data found";
            }
            catch (Exception ex)
            {
                return $"Update failed (unexpected): {ex.Message}";
            }
        }

        public async Task<string> UpdateRequestATM(RequestATM requestATM)
        {
            try
            {
                var existing = await _dbContext.REQUEST_ATM
                                                 .Where(x => x.RequestCode == requestATM.RequestCode && x.AtmCode == requestATM.AtmCode && x.IsDeleted != true)
                                                 .FirstOrDefaultAsync();
                existing.IsPOIssued = requestATM.IsPOIssued;
                existing.POIssuedBy = requestATM.POIssuedBy;
                existing.POIssuedOn = requestATM.POIssuedOn;
                if (requestATM.PoGeneratedDoc != null && requestATM.PoGeneratedDoc.Length > 0)
                {
                    existing.PoGeneratedDoc = requestATM.PoGeneratedDoc;
                    existing.PoGeneratedDocName = requestATM.PoGeneratedDocName;
                }
                existing.IsPoAccepted = requestATM.IsPoAccepted;
                existing.PoAcceptedBy = requestATM.PoAcceptedBy;
                existing.PoAcceptedOn = requestATM.PoAcceptedOn;
                if (requestATM.PoAcceptedDoc != null && requestATM.PoAcceptedDoc.Length > 0)
                {
                    existing.PoAcceptedDoc = requestATM.PoAcceptedDoc;
                    existing.PoAcceptedDocName = requestATM.PoAcceptedDocName;
                }
                existing.OrderedBy = requestATM.OrderedBy;
                existing.OrderedOn = requestATM.OrderedOn;
                existing.PreBookedAmt = requestATM.PreBookedAmt;
                if (requestATM.PreBookedDoc != null && requestATM.PreBookedDoc.Length > 0)
                {
                    existing.PreBookedDoc = requestATM.PreBookedDoc;
                    existing.PreBookedDocName = requestATM.PreBookedDocName;
                }
                existing.ReceivedBy = requestATM.ReceivedBy;
                existing.DeliveredOn = requestATM.DeliveredOn;
                existing.PostDeliveredAmt = requestATM.PostDeliveredAmt;
                if (requestATM.OrderRecivedDoc != null && requestATM.OrderRecivedDoc.Length > 0)
                {
                    existing.OrderRecivedDoc = requestATM.OrderRecivedDoc;
                    existing.OrderRecivedDocName = requestATM.OrderRecivedDocName;
                }
                existing.InstalledOn = requestATM.InstalledOn;
                existing.PostInstalledAmt = requestATM.PostInstalledAmt;
                if (requestATM.PostInstallationDoc != null && requestATM.PostInstallationDoc.Length > 0)
                {
                    existing.PostInstallationDoc = requestATM.PostInstallationDoc;
                    existing.PostInstallationDocName = requestATM.PostInstallationDocName;
                }
                existing.PaymentDate = requestATM.PaymentDate;
                existing.InvoiceNumber = requestATM.InvoiceNumber;
                existing.PaymentBy = requestATM.PaymentBy;
                if (requestATM.InvoiceDoc != null && requestATM.InvoiceDoc.Length > 0)
                {
                    existing.InvoiceDoc = requestATM.InvoiceDoc;
                    existing.InvoiceDocName = requestATM.InvoiceDocName;
                }
                await _dbContext.SaveChangesAsync();

                return "Success";
            }
            catch (Exception ex)
            {
                return $"Update failed (unexpected): {ex.Message}";
            }

        }

        public async Task<(bool IsSuccess, string? Msg)> SendPoEmailToVendor(string RequestCode, byte[] poFile, string fileName)
        {
            try
            {
                CancellationToken ct = default;

                // 1. Get Sender Email (RO/User Email)
                // You can fetch this from the database or session
                //string fromAddress = "procurement.support@unionbankofindia.bank";
                //        var ccRecipients = new List<string> {
                //    "rh.operations@unionbankofindia.bank",
                //    "zh.operations@unionbankofindia.bank",
                //    "co.ops@unionbankofindia.bank",
                //    "gm.ops@unionbankofindia.bank",
                //    "atm.monitoring@unionbankofindia.bank",
                //    "branches@unionbankofindia.bank"
                //};
                string fromAddress = "aniket.bandewar@unionbankofindia.bank.in";
                string vendorEmail = "lavanyakumar.cse@gmail.com";
                var ccRecipients = new List<string> { "praveen.666660@gmail.com" };



                var subject = $"Purchase Order: {RequestCode} - Replace of spare parts";

                // 5. Body HTML (The professional letter)
                var bodyHtml = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                <p>Dear Team,</p>
                <p>This is an auto-generated email for <strong>Purchase Order No. {RequestCode}</strong> dated <strong>{AppTime.Now:dd-MM-yyyy}</strong> for replacement of spare parts.</p>
                <p>Please find the PO attached for your reference and necessary action.</p>
                <p>Kindly confirm acceptance and delivery schedule at the earliest.</p>
                <br/>
                <p>Thanks and Regards,<br/>
                <strong>Union Bank Of India</strong></p>
            </body>
            </html>";

                // 6. Send the Email with Attachment
                // NOTE: You must update your SendEmail helper method to accept 'cc' and 'attachment'
                bool isSent = await SendEmail(
                    from: fromAddress,
                    to: vendorEmail,
                    cc: string.Join(",", ccRecipients),
                    subject: subject,
                    body: bodyHtml,
                    isHtml: true,
                    attachmentBytes: poFile,
                    attachmentName: fileName,
                    ct: ct);

                return isSent
                    ? (true, "PO Email sent successfully to Vendor and CC'd authorities.")
                    : (false, "SMTP server rejected the email.");
            }
            catch (Exception ex)
            {
                //_logger?.LogError(ex, "Error Sending PO Mail");
                return (false, $"Error Sending Mail: {ex.Message}");
            }
        }
        public async Task<bool> SendEmail(string from, string to, string cc, string subject, string body, bool isHtml, byte[]? attachmentBytes, string? attachmentName, CancellationToken ct)
        {

            using var client = new SmtpClient(_smtp.HOST, _smtp.PORT)
            {
                EnableSsl = _smtp.ENABLESSL,
                UseDefaultCredentials = _smtp.USE_DEFAULT_CREDENTIALS
            };

            if (!_smtp.USE_DEFAULT_CREDENTIALS && !string.IsNullOrWhiteSpace(_smtp.USER))
                client.Credentials = new NetworkCredential(_smtp.USER, _smtp.CREDENTIALS);


            string finalFrom = string.IsNullOrWhiteSpace(_smtp.FROM_EMAIL_ADDR) ? from : _smtp.FROM_EMAIL_ADDR;

            using var msg = new MailMessage
            {
                From = new MailAddress(finalFrom),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            // Add Primary Recipient
            msg.To.Add(to);

            // Add CC Recipients
            if (!string.IsNullOrWhiteSpace(cc))
            {
                msg.CC.Add(cc);
            }

            // Add Attachment if data is provided
            if (attachmentBytes != null && attachmentBytes.Length > 0)
            {
                var ms = new MemoryStream(attachmentBytes);
                var attachment = new Attachment(ms, attachmentName ?? "Document.pdf", "application/pdf");
                msg.Attachments.Add(attachment);
            }

            try
            {
                await client.SendMailAsync(msg, ct);
                return true;
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "SMTP Send Error");
                return false;
            }
        }
		public async Task<ATMVendorMaster?> GetItemByCode(string itemCode)
		{
			if (string.IsNullOrWhiteSpace(itemCode))
				return null;

			return await _dbContext.ATM_VENDOR_MASTER
				.FirstOrDefaultAsync(x => x.ItemCode == itemCode);
		}

		public async Task<List<ATMVendorMaster>> GetAllATMVendorData()
		{
			return await _dbContext.ATM_VENDOR_MASTER
						 .AsNoTracking()
						 .Where(emp => emp.IsDeleted != true)
						.OrderByDescending(emp => emp.SrNo)
						 .Select(e => new ATMVendorMaster
						 {
							 SrNo = e.SrNo,
							 ItemCode = e.ItemCode,
							 ItemName = e.ItemName,
							 ItemCategory = e.ItemCategory,
							 Specification = e.Specification,
							 ItemCost = e.ItemCost,
							 VendorName = e.VendorName,
							 VendorAddress = e.VendorAddress,
							 VendorGst = e.VendorGst,
							 PinCode = e.PinCode,
							 BudgetHead = e.BudgetHead

						 })
						 .ToListAsync();
		}

	}
}

public class SmtpSettings
{
    public string HOST { get; set; } = "";
    public int PORT { get; set; } = 25;
    public bool ENABLESSL { get; set; } = false;
    public string FROM_EMAIL_ADDR { get; set; } = "";
    public bool USE_DEFAULT_CREDENTIALS { get; set; } = true;
    public string? USER { get; set; }
    public string? CREDENTIALS { get; set; }
}
