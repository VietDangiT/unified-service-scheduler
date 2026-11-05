using System.Net;
using System.Text.Json;

using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// The client layer is represented by the OpenAPI contract the API generates (system design section 9), so the
// document must be served and must describe the booking endpoint with every status it can return.
public sealed class OpenApiDocumentTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>
{
    private const string DocumentPath = "/openapi/v1.json";

    [Fact]
    public async Task GetOpenApiDocument_WhenRequested_ReturnsJsonDocument()
    {
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri(DocumentPath, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetOpenApiDocument_WhenRequested_DescribesPostAppointmentsWithEveryResponseStatus()
    {
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri(DocumentPath, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/appointments", out JsonElement appointments), "The path /api/appointments is missing.");
        Assert.True(appointments.TryGetProperty("post", out JsonElement post), "The post operation on /api/appointments is missing.");

        string[] statuses = [.. post.GetProperty("responses").EnumerateObject().Select(status => status.Name).Order(StringComparer.Ordinal)];
        Assert.Equal(["201", "400", "404", "409", "422"], statuses);
    }

    // EC-97: only the request body is strict. A response or problem schema closed with additionalProperties: false
    // would make a generated strict client reject our own bodies (a problem carries traceId and code).
    [Fact]
    public async Task GetOpenApiDocument_WhenRequested_ClosesOnlyTheBookingRequestToUnknownProperties()
    {
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri(DocumentPath, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(ForbidsAdditionalProperties(schemas, "BookAppointmentRequest"), "BookAppointmentRequest must forbid additional properties.");
        Assert.False(ForbidsAdditionalProperties(schemas, "AppointmentResponse"), "AppointmentResponse must not forbid additional properties.");
        Assert.False(ForbidsAdditionalProperties(schemas, "ProblemDetails"), "ProblemDetails must not forbid additional properties.");
    }

    private static bool ForbidsAdditionalProperties(JsonElement schemas, string schemaName)
    {
        Assert.True(schemas.TryGetProperty(schemaName, out JsonElement schema), $"The schema {schemaName} is missing.");
        return schema.TryGetProperty("additionalProperties", out JsonElement additional)
            && additional.ValueKind == JsonValueKind.False;
    }
}
