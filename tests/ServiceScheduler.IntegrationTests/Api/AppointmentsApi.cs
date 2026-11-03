using System.Net;
using System.Text;
using System.Text.Json;

using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// Literal property names on purpose: these tests pin the wire format, so they do not reference the Api contract types.
internal static class AppointmentsApi
{
    public const string ProblemMediaType = "application/problem+json";

    // At the test's own dealership, for the vehicle's owner; the oil change unless another service type is given.
    public static Task<HttpResponseMessage> PostAppointmentAsync(
        HttpClient client, SchedulingData data, Guid vehicleId, string requestedStart, Guid? serviceTypeId = null) =>
        PostAppointmentAsync(
            client, data.OwnerOf(vehicleId), vehicleId, serviceTypeId ?? data.ServiceTypeId, data.DealershipId, requestedStart);

    // Every reference stated, for the requests that name a reference that does not exist or does not fit.
    public static async Task<HttpResponseMessage> PostAppointmentAsync(
        HttpClient client, Guid customerId, Guid vehicleId, Guid serviceTypeId, Guid dealershipId, string requestedStart)
    {
        using var request = new StringContent(
            $$"""
            {
              "customerId": "{{customerId}}",
              "vehicleId": "{{vehicleId}}",
              "serviceTypeId": "{{serviceTypeId}}",
              "dealershipId": "{{dealershipId}}",
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
        Assert.Equal(ProblemMediaType, response.Content.Headers.ContentType?.MediaType);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    public static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(ProblemMediaType, response.Content.Headers.ContentType?.MediaType);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    // The technician and bay a 201 names.
    public static async Task<(Guid TechnicianId, Guid ServiceBayId)> ReadAllocationAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement;

        return (body.GetProperty("technician").GetProperty("id").GetGuid(),
                body.GetProperty("serviceBay").GetProperty("id").GetGuid());
    }
}
