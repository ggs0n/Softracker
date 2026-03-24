using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
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

    public AuthController(UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtTokenService = jwtTokenService;
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
        if (!ModelState.IsValid)
            return View(model);

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

            return LocalRedirect(returnUrl ?? Url.Action("Index", "ChangeRequest")!);
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View(new SelfRegisterViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Register(SelfRegisterViewModel model, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        model.Email = (model.Email ?? string.Empty).Trim();
        model.CompanyName = BuildDefaultCompanyName(model.CompanyName, model.Email);

        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email is already registered.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = BuildDefaultFullName(model.Email),
            CompanyName = model.CompanyName,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        if (!await _roleManager.RoleExistsAsync("Admin"))
            await _roleManager.CreateAsync(new IdentityRole("Admin"));

        var roleResult = await _userManager.AddToRoleAsync(user, "Admin");
        if (!roleResult.Succeeded)
        {
            foreach (var error in roleResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            await _userManager.DeleteAsync(user);
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        var jwt = await _jwtTokenService.GenerateTokenAsync(user);
        Response.Cookies.Append("jwt_token", jwt, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(2)
        });

        TempData["Success"] = "Registration successful. Welcome to adib.";
        var defaultReturnUrl = Url.Action("Index", "Admin")!;
        return RedirectToAction(
            "Welcome",
            "Onboarding",
            new { returnUrl = returnUrl ?? defaultReturnUrl });
    }

    private static string BuildDefaultFullName(string? email)
    {
        var value = (email ?? string.Empty).Trim();
        var at = value.IndexOf('@');
        var localPart = at > 0 ? value[..at] : value;
        if (string.IsNullOrWhiteSpace(localPart))
            return "User";

        var spaced = Regex.Replace(localPart, @"[\.\-_]+", " ").Trim();
        if (string.IsNullOrWhiteSpace(spaced))
            return "User";

        var parts = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            parts[i] = part.Length == 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part[1..];
        }

        return string.Join(" ", parts);
    }

    private static string BuildDefaultCompanyName(string? companyName, string? email)
    {
        var normalized = (companyName ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
            return normalized;

        var value = (email ?? string.Empty).Trim();
        var at = value.IndexOf('@');
        if (at >= 0 && at < value.Length - 1)
        {
            var domain = value[(at + 1)..];
            if (!string.IsNullOrWhiteSpace(domain))
                return domain;
        }

        return "Personal";
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
