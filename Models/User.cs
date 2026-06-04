using System;
using System.ComponentModel.DataAnnotations;

namespace TicketSistemi.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Kullanıcı adı alanı zorunludur.")]
        [StringLength(50, ErrorMessage = "Kullanıcı adı en fazla 50 karakter olabilir.")]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Şifre alanı zorunludur.")]
        public required string PasswordHash { get; set; }

        [Required]
        public string Role { get; set; } = "User"; // "Admin" or "User"

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public List<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}
