namespace MyDiary.Web.Features.RedirectingPages.Services
{
    public interface ICodePolicyService
    {
        /// <summary>
        /// Returns all Code &amp; Policy documents ordered by Data_Date descending,
        /// matching the ORDER BY in the original Recovery.jsp query.
        /// </summary>
        Task<List<CodePolicyDocument>> GetAllDocumentsAsync();
    }

    public class CodePolicyDocument
    {
        public string DataId    { get; set; } = "";
        public DateTime? Date   { get; set; }
        public string FileName  { get; set; } = "";
        /// <summary>Filename only — concatenated with BaseUrl to form the full download URL.</summary>
        public string FilePath  { get; set; } = "";
        /// <summary>Download base URL stored in the database per record.</summary>
        public string BaseUrl   { get; set; } = "";

        /// <summary>Full download URL — BaseUrl + FilePath, built once at load time.</summary>
        public string DownloadUrl =>
            string.IsNullOrWhiteSpace(BaseUrl)
                ? FilePath
                : BaseUrl.TrimEnd('/') + "/" + FilePath.TrimStart('/');
    }
}
