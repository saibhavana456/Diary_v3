namespace MyDiary.Web.Core.Extensions
{
    /// <summary>
    /// Central source of truth for user-facing error text.
    ///
    /// Never surface raw exception details (e.g. Oracle "ORA-xxxxx" messages, SQL
    /// errors, stack traces) to end users — they are not user-friendly and can leak
    /// sensitive technical/implementation details. Show <see cref="Generic"/> in the
    /// UI and always log the full exception via AppLogger for troubleshooting.
    /// </summary>
    public static class UserFacingError
    {
        /// <summary>
        /// The approved generic message shown to users whenever a database exception
        /// or any other unexpected exception occurs.
        /// </summary>
        public const string Generic =
            "An unexpected error has occurred while processing your request. " +
            "Please try again later. If the issue persists, contact the System " +
            "Administrator for assistance.";
    }
}
