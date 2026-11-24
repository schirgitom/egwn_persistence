using System.Security.Cryptography;
using EGWNPersistence.Worker.Provisioning;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace EGWNPersistence.Worker.Controllers;

[ApiController]
[Route("internal/grafana/customers")]
public sealed class GrafanaCustomersController(CustomerProvisioner provisioner) : ControllerBase
{
    [HttpPost("{customerId:guid}/credentials")]
    public async Task<IActionResult> CreateCredentials(Guid customerId, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
            return BadRequest("A non-empty customer UUID is required.");

        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var username = await provisioner.ProvisionAsync(customerId, password, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return Ok(new { username, schema = username, password });
    }

    [HttpPut("{customerId:guid}/entitlements")]
    public async Task<IActionResult> ReplaceEntitlements(
        Guid customerId, [FromBody] CustomerEntitlements? request, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty || request?.Communities is null || request.Meters is null ||
            request.Communities.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > 100) ||
            request.Meters.Any(m => m is null || string.IsNullOrWhiteSpace(m.CommunityId) ||
                m.CommunityId.Length > 100 || string.IsNullOrWhiteSpace(m.MeterPointNumber) ||
                m.MeterPointNumber.Length > 100 || !request.Communities.Contains(m.CommunityId)))
            return BadRequest("Provide valid communities and community/meter pairs.");

        try
        {
            await provisioner.ReplaceEntitlementsAsync(customerId, request.Communities, request.Meters, cancellationToken);
            return NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.NoDataFound)
        {
            return NotFound("Customer is not provisioned.");
        }
    }

    [HttpPost("{customerId:guid}/disable")]
    public async Task<IActionResult> Disable(Guid customerId, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
            return BadRequest("A non-empty customer UUID is required.");

        try
        {
            await provisioner.DisableAsync(customerId, cancellationToken);
            return NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.NoDataFound)
        {
            return NotFound("Customer is not provisioned.");
        }
    }
}
