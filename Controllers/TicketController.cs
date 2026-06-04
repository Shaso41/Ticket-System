using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketSistemi.Models;
using TicketSistemi.Data;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using TicketSistemi.Hubs;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Collections.Generic;

namespace TicketSistemi.Controllers
{
    [Authorize]
    public class TicketController : Controller
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<TicketController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _context;

        public TicketController(IHubContext<NotificationHub> hubContext, ILogger<TicketController> logger, IWebHostEnvironment env, AppDbContext context)
        {
            _hubContext = hubContext;
            _logger = logger;
            _env = env;
            _context = context;
        }

        private async Task<(string? path, string? fileName)> SaveAttachmentAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return (null, null);
            }

            // 10MB limit
            const long maxFileSize = 10485760;
            if (file.Length > maxFileSize)
            {
                throw new ArgumentException("Yüklenen dosya boyutu 10MB'tan büyük olamaz.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".txt", ".zip", ".rar", ".docx", ".xlsx" };
            if (!allowedExtensions.Contains(extension))
            {
                throw new ArgumentException("İzin verilmeyen dosya formatı. Desteklenen formatlar: PDF, JPG, JPEG, PNG, GIF, WEBP, TXT, ZIP, RAR, DOCX, XLSX");
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return ("/uploads/" + uniqueFileName, file.FileName);
        }

        public async Task<IActionResult> Index(TicketStatus? status)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Logout", "Account");
            }

            var isAdmin = User.IsInRole("Admin");
            var query = _context.Tickets.AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(t => t.UserId == userId);
            }

            var allTickets = await query.ToListAsync();

            ViewBag.TotalCount = allTickets.Count;
            ViewBag.OpenCount = allTickets.Count(t => t.Status == TicketStatus.Acik);
            ViewBag.SolvedCount = allTickets.Count(t => t.Status == TicketStatus.Cozuldu);
            ViewBag.ClosedCount = allTickets.Count(t => t.Status == TicketStatus.Kapandi);

            if (status.HasValue)
            {
                allTickets = allTickets.Where(t => t.Status == status.Value).ToList();
            }

            var sortedTickets = allTickets.OrderByDescending(t => t.CreatedDate).ToList();
            ViewBag.CurrentStatus = status;
            
            return View(sortedTickets);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Ticket newTicket, IFormFile? attachment)
        {
            var username = User.Identity?.Name;
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            newTicket.CustomerName = username;
            newTicket.UserId = userId;
            ModelState.Remove("CustomerName");

            if (ModelState.IsValid)
            {
                string? attachmentPath = null;
                string? attachmentFileName = null;

                try
                {
                    (attachmentPath, attachmentFileName) = await SaveAttachmentAsync(attachment);
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError("attachment", ex.Message);
                    return View(newTicket);
                }

                newTicket.AttachmentPath = attachmentPath;
                newTicket.AttachmentFileName = attachmentFileName;
                newTicket.Description = TicketSistemi.Utils.HtmlSanitizer.Sanitize(newTicket.Description);
                newTicket.CreatedDate = DateTime.Now;

                _context.Tickets.Add(newTicket);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Yeni ticket eklendi (ID: {Id}, Başlık: {Title}) Müşteri: {CustomerName}", newTicket.Id, newTicket.Title, username);

                await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"Yeni bir destek talebi oluşturuldu! Konu: {newTicket.Title}", "Admin");
                
                return RedirectToAction("Index");
            }

            return View(newTicket);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Reply(int id)
        {
            return RedirectToAction("Details", new { id = id });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Claim(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();
            
            var agentName = User.Identity?.Name ?? "Destek Elemanı";
            ticket.AssignedAgent = agentName;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ticket {Id} admin {AdminName} tarafından üstlenildi.", id, agentName);
            
            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ticket = await _context.Tickets.Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();

            if (!string.IsNullOrEmpty(ticket.AttachmentPath))
            {
                var fullPath = Path.Combine(_env.WebRootPath, ticket.AttachmentPath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    try { System.IO.File.Delete(fullPath); } catch { /* fail silently */ }
                }
            }

            if (ticket.Messages != null)
            {
                foreach (var msg in ticket.Messages)
                {
                    if (!string.IsNullOrEmpty(msg.AttachmentPath))
                    {
                        var msgFullPath = Path.Combine(_env.WebRootPath, msg.AttachmentPath.TrimStart('/'));
                        if (System.IO.File.Exists(msgFullPath))
                        {
                            try { System.IO.File.Delete(msgFullPath); } catch { /* fail silently */ }
                        }
                    }
                }
            }
            
            _context.Tickets.Remove(ticket);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ticket {Id} ({Title}) silindi. Sileyen: {AdminName}", id, ticket.Title, User.Identity?.Name);
            
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var ticket = await _context.Tickets.Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();

            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && ticket.UserId != userId)
            {
                return Forbid();
            }

            // İlk açılış mesajını otomatik olarak gösterelim (eğer mesaj yoksa bile View'da bunu işleyebiliriz,
            // ama fiziksel olarak ilk mesajı yaratmak için bir mantık yazmışsınız, bunu DB'de tutmayıp sadece
            // gösterim anında da yapabiliriz veya ilk Ticket açılırken TicketMessage yaratılabilir.
            // Şimdilik Details View'ının çalışması için mesaj listesi boş olmasın diye oluşturabiliriz.
            if (ticket.Messages == null)
            {
                ticket.Messages = new List<TicketMessage>();
            }

            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(int id, string message, TicketStatus? status, IFormFile? attachment, TicketCategory? category, TicketPriority? priority)
        {
            var username = User.Identity?.Name;
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var ticket = await _context.Tickets.Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();

            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && ticket.UserId != userId)
            {
                return Forbid();
            }

            var oldCategory = ticket.Category;
            var oldPriority = ticket.Priority;
            var oldStatus = ticket.Status;

            if (isAdmin)
            {
                if (category.HasValue) ticket.Category = category.Value;
                if (priority.HasValue) ticket.Priority = priority.Value;
                if (string.IsNullOrEmpty(ticket.AssignedAgent)) ticket.AssignedAgent = username;
            }

            if (!string.IsNullOrWhiteSpace(message) || (attachment != null && attachment.Length > 0))
            {
                string? attachmentPath = null;
                string? attachmentFileName = null;

                try
                {
                    (attachmentPath, attachmentFileName) = await SaveAttachmentAsync(attachment);
                }
                catch (ArgumentException ex)
                {
                    TempData["ErrorMessage"] = ex.Message;
                    return RedirectToAction("Details", new { id = id });
                }

                var newMessage = new TicketMessage
                {
                    TicketId = ticket.Id,
                    Sender = username,
                    Role = isAdmin ? "Admin" : "User",
                    Message = !string.IsNullOrWhiteSpace(message) ? TicketSistemi.Utils.HtmlSanitizer.Sanitize(message.Trim()) : "",
                    SentDate = DateTime.Now,
                    AttachmentPath = attachmentPath,
                    AttachmentFileName = attachmentFileName
                };
                
                _context.TicketMessages.Add(newMessage);

                if (isAdmin && string.IsNullOrEmpty(ticket.AssignedAgent))
                {
                    ticket.AssignedAgent = username;
                }
                else if (!isAdmin && (ticket.Status == TicketStatus.Cozuldu || ticket.Status == TicketStatus.Kapandi))
                {
                    ticket.Status = TicketStatus.Acik;
                }

                _logger.LogInformation("Ticket {Id}'ye yanıt yazıldı. Yazan: {Username} ({Role})", id, username, isAdmin ? "Admin" : "User");
            }

            if (status.HasValue && (isAdmin || status.Value == TicketStatus.Kapandi || status.Value == TicketStatus.Cozuldu || status.Value == TicketStatus.Acik))
            {
                ticket.Status = status.Value;
            }

            // Sistem mesajları (Kategori, Öncelik, Durum değişimi)
            void AddSystemMessage(string msg)
            {
                _context.TicketMessages.Add(new TicketMessage
                {
                    TicketId = ticket.Id,
                    Sender = "Sistem",
                    Role = "Admin",
                    Message = msg,
                    SentDate = DateTime.Now
                });
            }

            if (isAdmin && oldCategory != ticket.Category)
            {
                AddSystemMessage($"Kategori '{TicketSistemi.Models.EnumHelper.GetCategoryName(oldCategory)}' değerinden '{TicketSistemi.Models.EnumHelper.GetCategoryName(ticket.Category)}' değerine güncellendi.");
            }

            if (isAdmin && oldPriority != ticket.Priority)
            {
                AddSystemMessage($"Öncelik seviyesi '{TicketSistemi.Models.EnumHelper.GetPriorityName(oldPriority)}' değerinden '{TicketSistemi.Models.EnumHelper.GetPriorityName(ticket.Priority)}' değerine güncellendi.");
            }

            if (oldStatus != ticket.Status)
            {
                string oldStatusName = oldStatus == TicketStatus.Acik ? "Açık" : oldStatus == TicketStatus.Cozuldu ? "Çözüldü" : "Kapalı";
                string newStatusName = ticket.Status == TicketStatus.Acik ? "Açık" : ticket.Status == TicketStatus.Cozuldu ? "Çözüldü" : "Kapalı";
                AddSystemMessage($"Bilet durumu '{oldStatusName}' değerinden '{newStatusName}' değerine güncellendi.");
                _logger.LogInformation("Ticket {Id} durumu {OldStatus} -> {NewStatus} yapıldı. Yapan: {Username}", id, oldStatus, ticket.Status, username);
            }

            await _context.SaveChangesAsync();

            // Bildirimler
            if (!string.IsNullOrWhiteSpace(message) || (attachment != null && attachment.Length > 0))
            {
                if (isAdmin)
                    await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"Talebinize yeni bir yanıt eklendi! Konu: {ticket.Title}", "User");
                else
                    await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"Talebe müşteri tarafından yeni yanıt yazıldı! Konu: {ticket.Title}", "Admin");
            }
            else if (status.HasValue && oldStatus != ticket.Status)
            {
                string statusName = status.Value == TicketStatus.Acik ? "Açık" : status.Value == TicketStatus.Cozuldu ? "Çözüldü" : "Kapalı";
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"Talep durumu güncellendi ({statusName}): {ticket.Title}", isAdmin ? "User" : "Admin");
            }

            return RedirectToAction("Details", new { id = id });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Dashboard()
        {
            var tickets = await _context.Tickets.Include(t => t.Messages).ToListAsync();

            int totalTickets = tickets.Count;
            int openCount = tickets.Count(t => t.Status == TicketStatus.Acik);
            int solvedCount = tickets.Count(t => t.Status == TicketStatus.Cozuldu);
            int closedCount = tickets.Count(t => t.Status == TicketStatus.Kapandi);

            var categoryStats = tickets.GroupBy(t => t.Category)
                                       .ToDictionary(g => EnumHelper.GetCategoryName(g.Key), g => g.Count());
            
            foreach (TicketCategory cat in Enum.GetValues(typeof(TicketCategory)))
            {
                var catName = EnumHelper.GetCategoryName(cat);
                if (!categoryStats.ContainsKey(catName))
                {
                    categoryStats[catName] = 0;
                }
            }

            var priorityStats = tickets.GroupBy(t => t.Priority)
                                       .ToDictionary(g => EnumHelper.GetPriorityName(g.Key), g => g.Count());
            
            foreach (TicketPriority pri in Enum.GetValues(typeof(TicketPriority)))
            {
                var priName = EnumHelper.GetPriorityName(pri);
                if (!priorityStats.ContainsKey(priName))
                {
                    priorityStats[priName] = 0;
                }
            }

            var trendStats = new Dictionary<string, int>();
            var today = DateTime.Today;
            for (int i = 6; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var dateStr = date.ToString("dd.MM.yyyy");
                trendStats[dateStr] = tickets.Count(t => t.CreatedDate.Date == date);
            }

            double avgReplies = 0;
            if (totalTickets > 0)
            {
                avgReplies = tickets.Average(t => t.Messages != null ? Math.Max(0, t.Messages.Count) : 0);
            }

            ViewBag.TotalTickets = totalTickets;
            ViewBag.OpenCount = openCount;
            ViewBag.SolvedCount = solvedCount;
            ViewBag.ClosedCount = closedCount;
            ViewBag.CategoryStats = categoryStats;
            ViewBag.PriorityStats = priorityStats;
            ViewBag.TrendStats = trendStats;
            ViewBag.AvgReplies = Math.Round(avgReplies, 1);

            return View();
        }
    }
}