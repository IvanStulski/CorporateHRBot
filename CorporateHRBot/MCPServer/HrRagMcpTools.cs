using CorporateHRBot.Entities;
using CorporateHRBot.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CorporateHRBot.MCPServer;

[McpServerToolType]
public sealed class HrRagMcpTools
{
    private readonly IHrRagService _hrRagService;

    public HrRagMcpTools(IHrRagService hrRagService)
    {
        _hrRagService = hrRagService;
    }

    [McpServerTool]
    [Description("""
        Searches corporate HR policies and answers employee questions.
        Use this tool only for questions related to company HR policies,
        vacations, sick leave, remote work, benefits, insurance, and corporate rules.
        """)]
    public async Task<HrMcpAnswer> AskHrAssistantAsync(
        [Description("Employee question about HR policies.")] string question,
        CancellationToken cancellationToken)
    {
        var response = await _hrRagService.GetAnswerAsync(question);

        return new HrMcpAnswer(
            response.Answer,
            response.References
                .Select(x => new HrMcpSource(
                    x.FileName,
                    x.ChunkIndex))
                .ToList());
    }
}

public sealed record HrMcpAnswer(
    string Answer,
    IReadOnlyList<HrMcpSource> Sources);

public sealed record HrMcpSource(
    string FileName,
    int ChunkId);
