namespace MyDiary.Web.Features.FlatManagement.Models.DTO;

public record AdminUserFormData(
    string PfNo,
    string? Name,
    string? Designation,
    string? Department,
    string? Phone,
    string? Email,
    string? RoCode,
    string? RoName,
    string? ZoCode,
    string? ZoName,
    string? ScopeType,
    string? ScopeCode,
    string? ScopeName);

public record AdminUserSaveRequest(
    string EmpId,
    string? Name,
    string Status,
    string EnteredBy,
    string Scope,
    string ScopeCode,
    string? ScopeName,
    string? Phone,
    string? Email,
    string? Designation);

public record AdminUserSaveResult(bool Success, string? Error);
