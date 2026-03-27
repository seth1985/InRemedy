using InRemedy.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace InRemedy.Api.Services;

public sealed class ImportWorker(IServiceScopeFactory scopeFactory, ImportQueue importQueue) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var batchId = await importQueue.DequeueAsync(stoppingToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<InRemedyDbContext>();
            var csvImportService = scope.ServiceProvider.GetRequiredService<CsvImportService>();

            var batch = await dbContext.ImportBatches.FirstOrDefaultAsync(x => x.ImportBatchId == batchId, stoppingToken);
            if (batch is null)
            {
                continue;
            }

            try
            {
                await csvImportService.ProcessQueuedImportAsync(batchId, stoppingToken);
            }
            catch (Exception ex)
            {
                batch.Status = "Failed";
                batch.Message = $"Background import failed: {ex.Message}";
                batch.CompletedUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
