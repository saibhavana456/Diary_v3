namespace MyDiary.Web.Features.FlatManagement.Models.DTO;

// Ports Pages_User/ViewAllocatedFlat.aspx.cs's FlatAllocationInfo + the two ad-hoc result
// shapes it built from VW_FLAT_DETAILS and the PS_UBI_RQ_APP_PROC/STAFF_DETAILS join.
public record AllocatedFlatDetails(
    string? FlatNo,
    string? FlatType,
    string? CarpetArea,
    string? BuildingName,
    string? Address);

public record CurrentOccupantDetails(
    string? EmplId,
    string? Name,
    string? JobDesc,
    string? ContactNo,
    string? EmailId,
    DateTime? BeginDt,
    DateTime? DeptJoinDt,
    DateTime? ExpectedRetirementDate,
    string? LocationDesc,
    string? RegionName,
    string? ZoneName);

public record ViewAllocatedFlatResult(
    bool HasFlat,
    AllocatedFlatDetails? Flat,
    CurrentOccupantDetails? CurrentOccupant,
    string? Error);
