using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IAdminFlatAllocationService
{
    Task<FlatAllocationFlatInfo?> GetFlatInfoAsync(string flatId);
    Task<List<FlatOccupantRow>> GetCurrentOccupantsAsync(string flatId);
    Task<FlatAllocationResult> UpdateOccupantAsync(FlatOccupantEditRequest req);

    Task<StaffLookupInfo?> GetStaffByPfAsync(string pfNo);
    Task<string?> CheckExistingAllocationAsync(string pfNo);
    Task<FlatAllocationResult> AllocateAsync(FlatAllocationRequest req);

    Task<List<string>> GetAllocatablePfListAsync(string flatId);
    Task<DeallocationCandidate?> GetOccupantByPfAsync(string pfNo);
    Task<FlatAllocationResult> DeallocateAsync(FlatDeallocationRequest req);

    Task<List<PreviousOccupantRow>> GetPreviousOccupantsAsync(string flatId);
}
