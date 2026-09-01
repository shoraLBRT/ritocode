using Microsoft.AspNetCore.Http;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;

namespace Ritocode.Shared.Tests.Http;

public sealed class ApiProblemTests
{
    private static DefaultHttpContext ContextWith(string path, string? requestId = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (requestId is not null)
        {
            context.Items[RequestId.ItemsKey] = requestId;
        }

        return context;
    }

    [Fact]
    public void Create_ProducesRfc9457BodyWithRitocodeExtensions()
    {
        var error = AppError.NotFound("workspace_not_found", "Workspace does not exist.");

        var problem = ApiProblem.Create(error, ContextWith("/api/v1/workspaces/42", "req-1"));

        Assert.Equal(404, problem.Status);
        Assert.Equal("Not found", problem.Title);
        Assert.Equal("Workspace does not exist.", problem.Detail);
        Assert.Equal("/api/v1/workspaces/42", problem.Instance);
        Assert.Equal(ApiProblem.TypeUriPrefix + "workspace_not_found", problem.Type);
        Assert.Equal("workspace_not_found", problem.Extensions["code"]);
        Assert.Equal("req-1", problem.Extensions["requestId"]);
    }

    [Fact]
    public void Create_WithoutFieldErrors_OmitsErrorsMember()
    {
        var problem = ApiProblem.Create(AppError.Conflict("already_submitted", "Already submitted."), ContextWith("/x"));

        Assert.False(problem.Extensions.ContainsKey("errors"));
    }

    [Fact]
    public void Create_WithFieldErrors_IncludesErrorsMember()
    {
        var fields = new Dictionary<string, string[]> { ["title"] = ["Required."] };

        var problem = ApiProblem.Create(AppError.Validation("Invalid.", fields), ContextWith("/x"));

        Assert.Equal(400, problem.Status);
        Assert.Equal(fields, problem.Extensions["errors"]);
    }

    [Fact]
    public void Create_WithoutCorrelationMiddleware_FallsBackToTraceIdentifier()
    {
        var context = ContextWith("/x");
        context.TraceIdentifier = "trace-9";

        var problem = ApiProblem.Create(AppError.Unauthenticated(), context);

        Assert.Equal("trace-9", problem.Extensions["requestId"]);
    }
}
