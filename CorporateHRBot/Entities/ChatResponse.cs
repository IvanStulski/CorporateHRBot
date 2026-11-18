namespace CorporateHRBot.Entities;

public record ChatResponse(string Answer, IEnumerable<SourceReference> References);