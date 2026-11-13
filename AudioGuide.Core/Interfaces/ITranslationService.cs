namespace AudioGuide.Core.Interfaces;

// Cầu nối giao tiếp với dịch vụ dịch thuật tự động đa ngôn ngữ
public interface ITranslationService
{
    // Dịch đồng thời tiêu đề và bài thuyết minh sang ngôn ngữ đích
    Task<(string Title, string Description)> TranslatePoiContentAsync(
        string title,
        string description,
        string targetLanguage,
        CancellationToken cancellationToken = default);

    // Dịch một đoạn văn bản bất kỳ từ tiếng Việt sang ngôn ngữ đích
    Task<string> TranslateAsync(
        string text,
        string targetLanguage,
        string sourceLanguage = "vi",
        CancellationToken cancellationToken = default);
}