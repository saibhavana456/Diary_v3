namespace MyDiary.Web.Features.FlatManagement.Models.DTO;

public record FlatAllocationFlatInfo(
    string FlatId,
    string? FlatNo,
    string? FlatArea,
    string? BhkDetails,
    string? SocietyId,
    string? SocietyName,
    string? Address,
    string? LocationId,
    string? LocationName,
    string? FamilyDetails,
    string? OccupancyStatus,
    int OccupantCount,
    string? ManagedByCode,
    string? ManagedByName);

public record FlatOccupantRow(
    string EmployeePf,
    string? EmployeeName,
    string? EmployeeDesignation,
    string? EmployeeDepartment,
    string? Email,
    string? Phone,
    string? OccupyDate);

public record FlatOccupantEditRequest(
    string FlatId,
    string EmployeePf,
    string EmployeeName,
    string EmployeeDesignation,
    string? EmployeeDepartment,
    string? Email,
    string? Phone);

public record StaffLookupInfo(
    string PfNo,
    string? Name,
    string? Designation,
    string? Department,
    string? RoCode,
    string? RoName,
    string? ZoCode,
    string? ZoName,
    string? PostingDate,
    string? RetirementDate,
    string? Phone,
    string? Email);

public record FlatAllocationRequest(
    string FlatId,
    string EmployeePf,
    string EmployeeName,
    string EmployeeDesignation,
    string? EmployeeDepartment,
    string OccupyDate,
    string? AllotmentLetterNo,
    string? TransferLetterNo,
    string KeysGiven,
    string? VehicleDetails,
    string? Remark,
    string? Email,
    string? Phone,
    string? RoCode,
    string? RoName,
    string? ZoCode,
    string? ZoName,
    string? PostingDate,
    string? RetirementDate,
    string EnteredBy);

public record FlatAllocationResult(bool Success, string? Error);

public record DeallocationCandidate(
    string EmployeePf,
    string? EmployeeName,
    string? EmployeeDesignation,
    string? OccupyDate,
    string? Email,
    string? Phone);

public record FlatDeallocationRequest(
    string FlatId,
    string EmployeePf,
    string ExitDate,
    string KeysBack,
    string? KeysBackTo,
    string? Remark,
    string EnteredBy);

public record PreviousOccupantRow(
    string? EmployeePf,
    string? EmployeeName,
    string? EmployeeDesignation,
    string? EmployeeDepartment,
    string? OccupyDate,
    string? ExitDate,
    string? OccupyStatus,
    string? Email,
    string? Phone);
