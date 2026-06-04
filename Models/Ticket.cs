using System.ComponentModel.DataAnnotations;

namespace TicketSistemi.Models
{
    public enum TicketCategory
    {
        HataBildirimi = 1,
        FaturaOdeme = 2,
        SistemSunucu = 3,
        GenelSorular = 4
    }

    public enum TicketPriority
    {
        Dusuk = 1,
        Orta = 2,
        Yuksek = 3
    }

    public static class EnumHelper
    {
        public static string GetCategoryName(TicketCategory category)
        {
            return category switch
            {
                TicketCategory.HataBildirimi => "Hata Bildirimi",
                TicketCategory.FaturaOdeme => "Fatura & Ödeme",
                TicketCategory.SistemSunucu => "Sistem & Sunucu",
                TicketCategory.GenelSorular => "Genel Sorular",
                _ => "Genel Sorular"
            };
        }

        public static string GetPriorityName(TicketPriority priority)
        {
            return priority switch
            {
                TicketPriority.Dusuk => "Düşük",
                TicketPriority.Orta => "Orta",
                TicketPriority.Yuksek => "Yüksek",
                _ => "Orta"
            };
        }
    }

    public class Ticket
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Lütfen bir başlık giriniz.")]
        [StringLength(100, ErrorMessage = "Başlık 100 karakterden uzun olamaz.")]
        public string Title { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Sorununuzu detaylıca açıklamanız gerekmektedir.")]
        public string Description { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Adınızı girmeniz zorunludur.")]
        public string CustomerName { get; set; } = string.Empty; 
        
        public int UserId { get; set; }
        public User? User { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public TicketStatus Status { get; set; } = TicketStatus.Acik;
        public string? AssignedAgent { get; set; }
        
        public TicketCategory Category { get; set; } = TicketCategory.GenelSorular;
        public TicketPriority Priority { get; set; } = TicketPriority.Orta;
        public string? AttachmentPath { get; set; }
        public string? AttachmentFileName { get; set; }

        // Mesaj geçmişi
        public List<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    }

    public class TicketMessage
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public Ticket? Ticket { get; set; }

        public string Sender { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // "Admin" or "User"
        public string Message { get; set; } = string.Empty;
        public DateTime SentDate { get; set; } = DateTime.Now;
        public string? AttachmentPath { get; set; }
        public string? AttachmentFileName { get; set; }
    }
}