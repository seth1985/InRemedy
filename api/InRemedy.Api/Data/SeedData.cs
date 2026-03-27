using System.Text.Json;
using InRemedy.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace InRemedy.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(InRemedyDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Remediations.AnyAsync(cancellationToken))
        {
            return;
        }

        var remediations = new[]
        {
            new Remediation
            {
                RemediationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                RemediationName = "Fix Windows Update Service State",
                Category = "Windows Update",
                Description = "Ensures the Windows Update service is enabled and healthy.",
                Platform = "Windows",
                ActiveFlag = true,
                DetectionScriptVersion = "1.8.0",
                RemediationScriptVersion = "1.4.0"
            },
            new Remediation
            {
                RemediationId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                RemediationName = "Repair Time Zone Drift",
                Category = "Configuration",
                Description = "Corrects unsupported time zone assignments.",
                Platform = "Windows",
                ActiveFlag = true,
                DetectionScriptVersion = "2.1.0",
                RemediationScriptVersion = "2.0.2"
            },
            new Remediation
            {
                RemediationId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                RemediationName = "Recover Office Activation",
                Category = "Microsoft 365",
                Description = "Re-applies licensing and activation repair steps.",
                Platform = "Windows",
                ActiveFlag = true,
                DetectionScriptVersion = "3.0.1",
                RemediationScriptVersion = "2.7.9"
            },
            new Remediation
            {
                RemediationId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                RemediationName = "Clear Pending Reboot State",
                Category = "Servicing",
                Description = "Identifies stale reboot markers after patching.",
                Platform = "Windows",
                ActiveFlag = true,
                DetectionScriptVersion = "1.3.2",
                RemediationScriptVersion = "1.1.0"
            }
        };

        var devices = new[]
        {
            new Device
            {
                DeviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                DeviceName = "LON-W11-2042",
                PrimaryUser = "Adele Varga",
                Manufacturer = "Lenovo",
                Model = "ThinkPad T14 Gen 3",
                OsVersion = "Windows 11 23H2",
                OsBuild = "22631.3447",
                Region = "UK",
                UpdateRing = "Ring 0",
                LastSyncDateTimeUtc = DateTime.SpecifyKind(DateTime.Parse("2026-03-24T08:15:00Z"), DateTimeKind.Utc)
            },
            new Device
            {
                DeviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                DeviceName = "BUD-W11-1188",
                PrimaryUser = "Mate Farkas",
                Manufacturer = "Dell",
                Model = "Latitude 7440",
                OsVersion = "Windows 11 23H2",
                OsBuild = "22631.3447",
                Region = "HU",
                UpdateRing = "Ring 1",
                LastSyncDateTimeUtc = DateTime.SpecifyKind(DateTime.Parse("2026-03-24T07:42:00Z"), DateTimeKind.Utc)
            },
            new Device
            {
                DeviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
                DeviceName = "NYC-W10-7712",
                PrimaryUser = "Liam Chen",
                Manufacturer = "HP",
                Model = "EliteBook 840 G8",
                OsVersion = "Windows 10 22H2",
                OsBuild = "19045.5796",
                Region = "US",
                UpdateRing = "Pilot",
                LastSyncDateTimeUtc = DateTime.SpecifyKind(DateTime.Parse("2026-03-23T23:56:00Z"), DateTimeKind.Utc)
            },
            new Device
            {
                DeviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"),
                DeviceName = "LON-W11-0901",
                PrimaryUser = "Priya Nair",
                Manufacturer = "Lenovo",
                Model = "ThinkCentre M90q",
                OsVersion = "Windows 11 24H2",
                OsBuild = "26100.3775",
                Region = "UK",
                UpdateRing = "Ring 0",
                LastSyncDateTimeUtc = DateTime.SpecifyKind(DateTime.Parse("2026-03-24T09:03:00Z"), DateTimeKind.Utc)
            },
            new Device
            {
                DeviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5"),
                DeviceName = "AMS-W11-7002",
                PrimaryUser = "Joris de Wit",
                Manufacturer = "Microsoft",
                Model = "Surface Laptop 6",
                OsVersion = "Windows 11 24H2",
                OsBuild = "26100.3775",
                Region = "NL",
                UpdateRing = "Ring 2",
                LastSyncDateTimeUtc = DateTime.SpecifyKind(DateTime.Parse("2026-03-24T06:31:00Z"), DateTimeKind.Utc)
            }
        };

        var statusCycle = new[] { "Fail", "Remediated", "Pass", "Stale", "Fail" };
        var categories = new[] { "ServiceStopped", "PendingReboot", "AccessDenied", "Unknown", "Healthy" };
        var resultList = new List<RemediationResult>();

        for (var remediationIndex = 0; remediationIndex < remediations.Length; remediationIndex++)
        {
            for (var deviceIndex = 0; deviceIndex < devices.Length; deviceIndex++)
            {
                var remediation = remediations[remediationIndex];
                var device = devices[deviceIndex];
                var status = statusCycle[(remediationIndex + deviceIndex) % statusCycle.Length];
                var category = categories[(remediationIndex * 2 + deviceIndex) % categories.Length];
                var sequence = remediationIndex * devices.Length + deviceIndex + 1;

                resultList.Add(new RemediationResult
                {
                    ResultId = Guid.NewGuid(),
                    RemediationId = remediation.RemediationId,
                    DeviceId = device.DeviceId,
                    RunTimestampUtc = DateTime.SpecifyKind(DateTime.Parse($"2026-03-{20 + ((sequence + remediationIndex) % 5):00}T{8 + ((sequence * 3) % 10):00}:00:00Z"), DateTimeKind.Utc),
                    Status = status,
                    RemediationAttemptedFlag = status is "Fail" or "Remediated",
                    RemediationSucceededFlag = status == "Remediated",
                    DetectionOutputRaw = $"{category} detected during validation on {device.DeviceName}.",
                    RemediationOutputRaw = status switch
                    {
                        "Pass" => "No action required.",
                        "Remediated" => "Recovery actions completed successfully.",
                        _ => "Corrective action attempted. Review service state and local policy."
                    },
                    ErrorCode = status == "Fail" ? "0x87D1" : null,
                    ErrorSummary = status == "Fail" ? "Remediation script exited with a non-zero code." : null,
                    OutputCategory = category,
                    ScriptVersion = remediation.RemediationScriptVersion,
                    IngestionTimestampUtc = DateTime.SpecifyKind(DateTime.Parse("2026-03-24T09:00:00Z"), DateTimeKind.Utc)
                });
            }
        }

        var defaultView = new
        {
            schemaVersion = 1,
            pageType = "dashboard",
            filters = new
            {
                search = "",
                statuses = Array.Empty<string>(),
                models = Array.Empty<string>(),
                selectedRemediationIds = Array.Empty<string>(),
                selectedDeviceIds = Array.Empty<string>()
            },
            gridState = new
            {
                visibleColumns = new[]
                {
                    "deviceName", "primaryUser", "model", "osBuild", "remediationName", "status", "outputCategory", "runTimestampUtc"
                },
                sortKey = "runTimestampUtc",
                sortDirection = "desc"
            }
        };

        dbContext.Remediations.AddRange(remediations);
        dbContext.Devices.AddRange(devices);
        dbContext.RemediationResults.AddRange(resultList);
        dbContext.SavedViews.Add(new SavedView
        {
            SavedViewId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            OwnerUserId = "system",
            PageType = "dashboard",
            Name = "System Default",
            IsDefault = true,
            IsSystemDefault = true,
            CreatedUtc = DateTime.SpecifyKind(DateTime.Parse("2026-03-24T09:00:00Z"), DateTimeKind.Utc),
            ModifiedUtc = DateTime.SpecifyKind(DateTime.Parse("2026-03-24T09:00:00Z"), DateTimeKind.Utc),
            ViewDefinitionJson = JsonSerializer.Serialize(defaultView)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
