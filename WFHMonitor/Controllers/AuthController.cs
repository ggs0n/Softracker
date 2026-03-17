using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
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
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        // Defense-in-depth: explicitly clear all Identity auth schemes.
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorRememberMeScheme);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);

        // Clear any session state if session middleware is enabled.
        HttpContext.Features.Get<ISessionFeature>()?.Session?.Clear();

        ExpireCookie("jwt_token");
        ExpireCookie("new_user_jwt_token");
        ExpireCookie(IdentityConstants.ApplicationScheme);
        ExpireCookie(IdentityConstants.ExternalScheme);
        ExpireCookie(IdentityConstants.TwoFactorRememberMeScheme);
        ExpireCookie(IdentityConstants.TwoFactorUserIdScheme);

        Response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Clear-Site-Data"] = "\"cookies\", \"storage\", \"cache\"";

        return RedirectToAction("Login");
    }

    private void ExpireCookie(string cookieName)
    {
        if (string.IsNullOrWhiteSpace(cookieName))
            return;

        Response.Cookies.Delete(cookieName, new CookieOptions { Path = "/" });
        Response.Cookies.Append(cookieName, string.Empty, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UnixEpoch
        });
    }

    public IActionResult AccessDenied() => View();
}
