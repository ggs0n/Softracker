using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

public class AuthController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserRegistrationService _userRegistrationService;

    public AuthController(UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IJwtTokenService jwtTokenService,
        IUserRegistrationService userRegistrationService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtTokenService = jwtTokenService;
        _userRegistrationService = userRegistrationService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var jwt = await _jwtTokenService.GenerateTokenAsync(user);
                Response.Cookies.Append("jwt_token", jwt, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(2)
                });
            }

            if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
                return RedirectToAction("Index", "Admin");

            return LocalRedirect(returnUrl ?? Url.Action("Index", "TaskBoard")!);
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Register() => RedirectToAction("Employees", "Admin");

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        var registration = await _userRegistrationService.RegisterAsync(model);
        if (registration.Succeeded && registration.User != null)
        {
            var jwt = await _jwtTokenService.GenerateTokenAsync(registration.User);
            Response.Cookies.Append("new_user_jwt_token", jwt, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(2)
            });
            TempData["Success"] = $"{registration.Role} {registration.FullName} registered successfully.";
            return RedirectToAction("Index", "Admin");
        }

        foreach (var error in registration.Errors)
            ModelState.AddModelError(string.Empty, error);

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        DeleteCookie("jwt_token");
        DeleteCookie("new_user_jwt_token");
        TempData["Info"] = "You have been signed out.";
        return RedirectToAction("Login");
    }

    private void DeleteCookie(string cookieName)
    {
        Response.Cookies.Delete(cookieName, new CookieOptions
        {
            Path = "/",
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true
        });
    }

    public IActionResult AccessDenied() => View();
}
