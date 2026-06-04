using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TicketSistemi.Data;
using TicketSistemi.Models;
using Microsoft.EntityFrameworkCore;

namespace TicketSistemi.Jobs
{
    public class AutoCloseTicketsJob : BackgroundService
    {
        private readonly ILogger<AutoCloseTicketsJob> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(12);

        public AutoCloseTicketsJob(ILogger<AutoCloseTicketsJob> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Biletleri otomatik kapatma arka plan servisi başlatıldı.");

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Otomatik bilet kapatma kontrolü çalıştırılıyor...");
                    await AutoCloseSolvedTicketsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Otomatik bilet kapatma işlemi sırasında bir hata oluştu.");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Biletleri otomatik kapatma arka plan servisi durduruldu.");
        }

        private async Task AutoCloseSolvedTicketsAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.Now;

            var solvedTickets = await context.Tickets
                .Include(t => t.Messages)
                .Where(t => t.Status == TicketStatus.Cozuldu)
                .ToListAsync(stoppingToken);

            bool isAnyUpdated = false;

            foreach (var ticket in solvedTickets)
            {
                var lastActivity = ticket.Messages != null && ticket.Messages.Any()
                    ? ticket.Messages.Max(m => m.SentDate)
                    : ticket.CreatedDate;

                if (now - lastActivity >= TimeSpan.FromDays(3))
                {
                    ticket.Status = TicketStatus.Kapandi;
                    
                    if (ticket.Messages == null)
                    {
                        ticket.Messages = new System.Collections.Generic.List<TicketMessage>();
                    }

                    ticket.Messages.Add(new TicketMessage
                    {
                        Sender = "Sistem",
                        Role = "Admin",
                        Message = "Çözüldü olarak işaretlenen bu bilet, 3 gün boyunca işlem yapılmadığı için sistem tarafından otomatik olarak kapatılmıştır.",
                        SentDate = now
                    });

                    _logger.LogInformation("Bilet otomatik olarak kapatıldı. ID: {Id}, Başlık: {Title}, Son Aktivite: {LastActivity}", ticket.Id, ticket.Title, lastActivity);
                    isAnyUpdated = true;
                }
            }

            if (isAnyUpdated)
            {
                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Otomatik kapatılan biletler kaydedildi.");
            }
            else
            {
                _logger.LogInformation("Otomatik kapatılması gereken herhangi bir bilet bulunamadı.");
            }
        }
    }
}
