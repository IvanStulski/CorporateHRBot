using CorporateHRBot.Services;
using Microsoft.AspNetCore.Mvc;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents")
                       .WithTags("Documents");

        group.MapGet("/search", SearchInfoInDocumentsAsync)
             .WithName("SearchInfoInDocuments")
             .Produces(StatusCodes.Status201Created)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/split", SplitDocumentByChunksAsync)
             .WithName("SplitDocumentByChunks")
             .Accepts<IFormFile>("multipart/form-data")
             .DisableAntiforgery()
             .Produces(StatusCodes.Status201Created)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> SplitDocumentByChunksAsync(
        IFormFile file,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        if (file == null)
        {
            return Results.BadRequest("No file to insert.");
        }

        try
        {
            await documentService.InsertFile(file, cancellationToken);

            return Results.StatusCode(StatusCodes.Status201Created);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> SearchInfoInDocumentsAsync(
        [FromQuery] string userQuery,
        IHrRagService hrRagService,
        CancellationToken cancellationToken)
    {
        if (userQuery == null)
        {
            return Results.BadRequest("No text to split.");
        }

        try
        {
            var info = await hrRagService.GetAnswerAsync(userQuery);

            return Results.Ok(info);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }
}