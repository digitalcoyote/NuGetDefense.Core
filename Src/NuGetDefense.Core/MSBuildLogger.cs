namespace NuGetDefense.Core
{
    /// <summary>
    ///     Class for creating MSBuild Messages:
    ///     https://docs.microsoft.com/en-us/archive/blogs/MSBuild/MSBuild-visual-studio-aware-error-messages-and-message-formats
    /// </summary>
    public static class MsBuild
    {
        /// <summary>
        /// Fallback file name to use if provided file is null.
        /// </summary>
        private const string FallbackFileName = "NuGetDefense";
        public enum Category
        {
            Warning,
            Error
        }

        /// <summary>
        ///     Generates an MSBuild Message
        /// </summary>
        /// <param name="file">file the problem is in</param>
        /// <param name="code">Code to use to identify the problem</param>
        /// <param name="lineNumber">line number the problem is on</param>
        /// <param name="linePosition">1 based index to use to reference where in the file the problem is</param>
        /// <param name="category">Warning or Error</param>
        /// <param name="text">message for the error</param>
        public static string Log(string? file, Category category, string code, int? lineNumber, int? linePosition, string text)
        {
            file ??= FallbackFileName;
            return
                $"{file}({lineNumber},{linePosition}) : {category.ToString()} : {code} : {text}";
        }

        /// <summary>
        ///     Generates an MSBuild Message
        /// </summary>
        /// <param name="file">file the problem is in</param>
        /// <param name="code">Code to use to identify the problem</param>
        /// <param name="category">Warning or Error</param>
        /// <param name="text">message for the error</param>
        public static string Log(string? file, Category category, string code, string text)
        {
            file ??= FallbackFileName;
            return
                $"{file}: {category.ToString()} : {code} : {text}";
        }

        /// <summary>
        ///     Generates an MSBuild Message
        /// </summary>
        /// <param name="file">file the problem is in</param>
        /// <param name="category">Warning or Error</param>
        /// <param name="text">message for the error</param>
        public static string Log(string? file, Category category, string text)
        {
            file ??= FallbackFileName;
            return
                $"{file}: {category.ToString()} : {text}";
        }

        /// <summary>
        ///     Generates an MSBuild Message
        /// </summary>
        /// <param name="file">file the problem is in</param>
        /// <param name="lineNumber">line number the problem is on</param>
        /// <param name="linePosition">1 based index to use to reference where in the file the problem is</param>
        /// <param name="category">Warning or Error</param>
        /// <param name="text">message for the error</param>
        public static string Log(string? file, Category category, int? lineNumber, int? linePosition, string text)
        {
            file ??= FallbackFileName;
            return
                $"{file}({lineNumber},{linePosition}) : {category.ToString()} : {text}";
        }
    }
}