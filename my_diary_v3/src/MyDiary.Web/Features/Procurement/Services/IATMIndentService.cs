using MyDiary.Web.Features.Procurement.Models.TABLES;
using MyDiary.Web.Features.Procurement.Models.VIEWS;
using static MyDiary.Web.Features.Procurement.Models.DTO.MiscClasses;

namespace MyDiary.Web.Features.Procurement.Interfaces
{
    public interface IATMIndentService
    {
        Task<string> DeleteATMIndentMaster(DeleteRequestDto requestMaster);
        Task<List<ATMRequestMaster>> GetAllATMIndentData(string code, string userRole);
        Task<dynamic> GetAllATMIndentDataById(string id);
        Task<string> InsertOrUpdateATMIndent(ATMRequestMaster requestMaster, string Role);

        Task<string> UpdateRequestATM(RequestATM requestAtM);
        Task<string> InsertPODetails(ATMRequestMaster atmRequest);
        Task<string> InsertFeedbackDetails(ATMRequestMaster atmRequest);
        Task<AtmMaster> GetAtmMasterById(string id);
        Task<List<AtmMaster>> GetAllATMMasterData();

        Task<(bool IsSuccess, string? Msg)> SendPoEmailToVendor(string RequestCode, byte[] poFile, string fileName);

        Task<UserMasterView> GetUserDetailsViewByUserId(string userId);

		Task<List<ATMVendorMaster>> GetAllATMVendorData();

		Task<ATMVendorMaster?> GetItemByCode(string itemCode);
	}
}
