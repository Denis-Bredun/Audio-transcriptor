using System.Globalization;

namespace ShortNotes
{
    public interface ISpeechToText
    {
        Task<bool> RequestPermissionsAsync();
        Task<string> ListenAsync(CultureInfo culture,
            IProgress<string> recognitionResult,
            CancellationToken cancellationToken);
    }
}
