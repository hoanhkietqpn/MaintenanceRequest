using Microsoft.AspNetCore.Mvc;
using MaintenanceRequestApp.Data;
using MaintenanceRequestApp.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;

namespace MaintenanceRequestApp.Controllers
{
    public class AuthController : Controller
    {
        private readonly MaintenanceDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthController(MaintenanceDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                var role = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
                return role != null ? RedirectBasedOnRole(role) : RedirectToAction("Index", "Staff");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập tên đăng nhập và mật khẩu.");
                return View();
            }

            var requestPayload = new { username = username, password = password };
            var content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient();

            try
            {
                var response = await client.PostAsync("https://smapi.vjaa.edu.vn/api/users/login", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    var jDoc = JsonDocument.Parse(jsonResponse);
                    var root = jDoc.RootElement;

                    string role = "NhanVienKyThuat";
                    if (root.TryGetProperty("data", out JsonElement dataElement))
                    {
                        if (dataElement.TryGetProperty("roles", out JsonElement rolesElement) && rolesElement.ToString().Contains("Admin"))
                        {
                            role = "Admin";
                        }
                    }
                    else
                    {
                        // Fallback logic
                        dataElement = root;
                    }

                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToUpper() == username.ToUpper());
                    if (user == null)
                    {
                        user = new User
                        {
                            UserId = Guid.NewGuid().ToString(),
                            Username = username.ToUpper(),
                            FirstName = dataElement.TryGetProperty("firstName", out var fn) ? fn.GetString() : "N/A",
                            LastName = dataElement.TryGetProperty("lastName", out var ln) ? ln.GetString() : "N/A",
                            Email = dataElement.TryGetProperty("email", out var e) ? e.GetString() : $"{username}@vjaa.edu.vn",
                            Role = role
                        };
                        _context.Users.Add(user);
                    }
                    else
                    {
                        user.FirstName = dataElement.TryGetProperty("firstName", out var fn) ? fn.GetString() : user.FirstName;
                        user.LastName = dataElement.TryGetProperty("lastName", out var ln) ? ln.GetString() : user.LastName;
                        user.Email = dataElement.TryGetProperty("email", out var e) ? e.GetString() : user.Email;
                        _context.Users.Update(user);
                    }

                    await _context.SaveChangesAsync();

                    await SetupClaimsAndSignInAsync(user);

                    return RedirectBasedOnRole(user.Role);
                }
            }
            catch (Exception ex)
            {
                // API fails, fallback to hardcoded admin/admin for testing purposes
                /*if (username == "admin" && password == "admin")
                {
                    var fallbackUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
                    if (fallbackUser == null)
                    {
                        fallbackUser = new User
                        {
                            UserId = Guid.NewGuid().ToString(),
                            Username = "admin",
                            FirstName = "Quản trị",
                            LastName = "Hệ thống",
                            Email = "admin@vjaa.edu.vn",
                            Role = "Admin"
                        };
                        _context.Users.Add(fallbackUser);
                        await _context.SaveChangesAsync();
                    }
                    await SetupClaimsAndSignInAsync(fallbackUser);
                    return RedirectBasedOnRole(fallbackUser.Role);
                }*/
                ModelState.AddModelError("", "Hệ thống đăng nhập hiện không hoạt động. Vui lòng thử lại sau.");
            }

            ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không chính xác.");
            return View();
        }

        private async Task SetupClaimsAndSignInAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.UserId),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(3) });
        }

        private IActionResult RedirectBasedOnRole(string role)
        {
            if (role == "Admin") return RedirectToAction("Index", "Admin");
            if (role == "QuanLyKyThuat") return RedirectToAction("Index", "Manager");
            return RedirectToAction("Index", "Staff");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}
