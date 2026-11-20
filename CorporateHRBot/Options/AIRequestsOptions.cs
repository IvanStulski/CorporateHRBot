namespace CorporateHRBot.Options;

public class AIRequestsOptions
{
    public string QuestionPromptTemplate { get; set; } = string.Empty;
    public string ComplexityEvaluationPrompt { get; set; } = string.Empty;
    public string ComplexityFormatEvaluationPrompt { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public string BaseEvaluateModel { get; set; } = string.Empty;
    public string LowComplexityModel { get; set; } = string.Empty;
    public string MiddleComplexityModel { get; set; } = string.Empty;
    public string HighComplexityModel { get; set; } = string.Empty;
}
