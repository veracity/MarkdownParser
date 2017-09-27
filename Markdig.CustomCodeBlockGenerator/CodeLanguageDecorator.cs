using System.Collections.Generic;

namespace Markdig.CustomCodeBlockGenerator
{
    public class CodeLanguageDecorator
    {
        static readonly Dictionary<string, string> LanguageMapping = new Dictionary<string, string>
        {
            {"csharp", "CSharp"},
            {"charp", "CSharp"},
            {"python", "Python"},
            {"javascript", "JavaScript"},
            {"js", "JavaScript"},
            {"nodejs", "NodeJS"},
            {"java", "Java"},
            {"powershell", "PowerShell"},
            {"batch", "Batch"}
        };
        public static string NormalizeLanguage(string language)
        {
            return LanguageMapping.ContainsKey(language) ? LanguageMapping[language] : "UNKNOWN";
        }
    }
}
