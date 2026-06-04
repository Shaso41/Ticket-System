using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using TicketSistemi.Data;
using TicketSistemi.Models;
using TicketSistemi.Utils;

namespace TicketSistemi.Controllers
{
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly AppDbContext _context;

        public AccountController(ILogger<AccountController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Ticket");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorMessage = "Kullanıcı adı ve şifre zorunludur!";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            if (user != null && PasswordHelper.VerifyPassword(user.Username, user.PasswordHash, password))
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation("Kullanıcı {Username} giriş yaptı.", user.Username);

                return RedirectToAction("Index", "Ticket");
            }

            _logger.LogWarning("Başarısız giriş denemesi: {Username}", username);

            ViewBag.ErrorMessage = "Kullanıcı adı veya şifre hatalı!";
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Ticket");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string username, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Kayıt başarısız: username veya password boş.");
                ViewBag.ErrorMessage = "Kullanıcı adı ve şifre alanları zorunludur!";
                return View();
            }

            if (password != confirmPassword)
            {
                _logger.LogWarning("Kayıt başarısız: {Username} için şifreler eşleşmiyor.", username);
                ViewBag.ErrorMessage = "Şifreler uyuşmuyor!";
                return View();
            }

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
            {
                _logger.LogWarning("Kayıt başarısız: {Username} kullanıcısı zaten var.", username);
                ViewBag.ErrorMessage = "Bu kullanıcı adı zaten alınmış!";
                return View();
            }

            var newUser = new User
            {
                Username = username.Trim(),
                PasswordHash = PasswordHelper.HashPassword(username.Trim(), password),
                Role = "User" 
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Yeni kullanıcı eklendi: {Username}", newUser.Username);

            TempData["SuccessMessage"] = "Kayıt başarıyla tamamlandı! Şimdi giriş yapabilirsiniz.";
            return RedirectToAction("Login");
        }

        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name;
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!string.IsNullOrEmpty(username))
            {
                _logger.LogInformation("{Username} çıkış yaptı.", username);
            }
            return RedirectToAction("Index", "Ticket");
        }
    }
}