using System.Net;
using System.Text;
using System.Text.Json;

using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.IntegrationTests.Api;

// Literal property names on purpose, as in PostAppointmentTests: these tests pin the wire format.
internal static class AppointmentsApi
{
    // An oil change for the seeded customer at the seeded dealership; only the vehicle and the start vary.
    public static async Task<HttpResponseMessage> PostAppointmentAsync(HttpClient client, Guid vehicleId, string requestedStart)
    {
        using var request = new StringContent(
            $$"""
            {
              "customerId": "{{SeedData.CustomerId}}",
              "vehicleId": "{{vehicleId}}",
              "serviceTypeId": "{{SeedData.OilChangeServiceTypeId}}",
              "dealershipId": "{{SeedData.DealershipId}}",
              "requestedStart": "{{requestedStart}}"
            }
            """,
            Encoding.UTF8,
            "application/json");

        return await client.PostAsync(new Uri("/api/appointments", UriKind.Relative), request);
    }

    public static async Task AssertConflictAsync(HttpResponseMessage response, string expectedCode)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }
}
