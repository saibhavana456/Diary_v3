namespace MyDiary.Web.Features.FlatManagement.Models.DTO;

public record FlatLocationOption(string Id, string Name);

public record FlatSocietyOption(string Id, string Name);

public record JurisdictionOption(string Code, string DisplayName);

public record OwnedUsageDefaults(string? OwnedStatus, string? UsabilityStatus);

public record AddFlatRequest(
    string LocationId,
    string LocationName,
    string SocietyId,
    string SocietyName,
    string SocietyAddress,
    string FlatNo,
    string FlatArea,
    string BhkDetails,
    string FamilyDetails,
    string OwnedStatus,
    string Usability,
    string PurchaseDate,
    string ManagedByCode,
    string ManagedByName,
    string CreatedBy);

public record AddFlatResult(bool Success, string? Error, string? FlatId);

public record FlatMasterRow(
    string FlatId,
    string? LocationName,
    string? SocietyName,
    string? FlatNo,
    string? FamilyDetails,
    string? BhkDetails,
    string? PurchaseDate,
    string? OccupancyStatus,
    string? ManagedByName);

public record FlatMasterListResult(IReadOnlyList<FlatMasterRow> Rows, int TotalCount);

public record EditFlatDetails(
    string FlatId,
    string? FlatNo,
    string? BhkDetails,
    string? FamilyDetails,
    string? FlatArea,
    string? SocietyId,
    string? LocationId,
    string? Address,
    int OccupantCount,
    string? OwnedStatus,
    string? Usability,
    string? PurchaseDate,
    string? ManagedByCode,
    string? ManagedByName);

public record EditFlatRequest(
    string FlatId,
    string FlatNo,
    string FlatArea,
    string BhkDetails,
    string FamilyDetails,
    string SocietyId,
    string SocietyName,
    string SocietyAddress,
    string LocationId,
    string LocationName,
    string OwnedStatus,
    string Usability,
    string PurchaseDate,
    string ManagedByCode,
    string ManagedByName);
