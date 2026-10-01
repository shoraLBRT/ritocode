using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Ritocode.Shared.Http;

namespace Ritocode.Shared.Tests.Http;

/// <summary>
/// A request the framework could not read keeps a client status. The test host does not enforce the
/// body cap, so the 413 the server raises past it is driven through the handler directly.
/// </summary>
public sealed class AppExceptionHandlerTests
{
    [Theory]
    [InlineData(400, 400, "request_invalid")]
    [InlineData(408, 400, "request_invalid")]
    [InlineData(413, 413, "request_too_large")]
    [InlineData(415, 415, "unsupported_media_type")]
    public async Task BadHttpRequest_KeepsAClientStatus_AndAStableCode(int thrown, int status, string code)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new AppExceptionHandler(NullLogger<AppExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context, new BadHttpRequestException("unreadable", thrown), TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("unreadable", document.RootElement.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }
}
