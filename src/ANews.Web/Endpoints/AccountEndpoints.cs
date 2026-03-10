using ANews.Domain.Enums;
using ANews.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace ANews.Web.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapPost("/account/login", async (HttpContext http, SignInManager<ApplicationUser> signInMgr) =>
        {
            var form = await http.Request.ReadFormAsync();
            var email = form["email"].ToString();
            var password = form["password"].ToString();
            var rememberMe = form["rememberMe"] == "true";
            var returnUrl = form["returnUrl"].ToString();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return Results.Redirect("/login?error=invalid");

            var result = await signInMgr.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
                return Results.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
            if (result.IsLockedOut)
                return Results.Redirect("/login?error=locked");
            return Results.Redirect("/login?error=invalid");
        }).RequireRateLimiting("auth").DisableAntiforgery();

        app.MapGet("/account/logout", async (HttpContext http, SignInManager<ApplicationUser> signInMgr) =>
        {
            await signInMgr.SignOutAsync();
            return Results.Redirect("/");
        }).DisableAntiforgery();

        app.MapPost("/account/register", async (
            HttpContext http,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInMgr,
            IEmailSender<ApplicationUser> emailSender,
            IConfiguration config) =>
        {
            var form = await http.Request.ReadFormAsync();
            var displayName = form["displayName"].ToString().Trim();
            var email = form["email"].ToString().Trim();
            var password = form["password"].ToString();
            var confirmPassword = form["confirmPassword"].ToString();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return Results.Redirect("/register?error=required");

            if (password != confirmPassword)
                return Results.Redirect("/register?error=mismatch");

            var smtpConfigured = !string.IsNullOrEmpty(config["Smtp:Host"]) && !string.IsNullOrEmpty(config["Smtp:User"]);

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Split('@')[0] : displayName,
                Role = UserRole.User,
                IsActive = true,
                EmailConfirmed = !smtpConfigured
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var error = Uri.EscapeDataString(result.Errors.First().Description);
                return Results.Redirect($"/register?error={error}");
            }

            await userManager.AddToRoleAsync(user, nameof(UserRole.User));

            if (smtpConfigured)
            {
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = Uri.EscapeDataString(token);
                var appUrl = config["AppUrl"] ?? "https://news.websoftware.es";
                var confirmLink = $"{appUrl}/account/confirm-email?userId={user.Id}&token={encodedToken}";
                await emailSender.SendConfirmationLinkAsync(user, email, confirmLink);
                return Results.Redirect("/register?success=check-email");
            }

            await signInMgr.SignInAsync(user, isPersistent: false);
            return Results.Redirect("/user");
        }).DisableAntiforgery();

        app.MapGet("/account/confirm-email", async (
            int userId,
            string token,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInMgr) =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user == null) return Results.Redirect("/?confirmed=error");

            var result = await userManager.ConfirmEmailAsync(user, Uri.UnescapeDataString(token));
            if (!result.Succeeded) return Results.Redirect("/?confirmed=error");

            await signInMgr.SignInAsync(user, isPersistent: false);
            return Results.Redirect("/?confirmed=1");
        }).DisableAntiforgery();

        app.MapPost("/account/forgot-password", async (
            HttpContext http,
            UserManager<ApplicationUser> userManager,
            IEmailSender<ApplicationUser> emailSender,
            IConfiguration config) =>
        {
            var form = await http.Request.ReadFormAsync();
            var email = form["email"].ToString().Trim();

            if (string.IsNullOrWhiteSpace(email))
                return Results.Redirect("/forgot-password");

            var user = await userManager.FindByEmailAsync(email);
            if (user != null && await userManager.IsEmailConfirmedAsync(user))
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = Uri.EscapeDataString(token);
                var appUrl = config["AppUrl"] ?? "https://news.websoftware.es";
                var resetLink = $"{appUrl}/reset-password?userId={user.Id}&token={encodedToken}";
                await emailSender.SendPasswordResetLinkAsync(user, email, resetLink);
            }

            return Results.Redirect("/forgot-password?sent=1");
        }).DisableAntiforgery();

        app.MapPost("/account/reset-password", async (
            HttpContext http,
            UserManager<ApplicationUser> userManager) =>
        {
            var form = await http.Request.ReadFormAsync();
            var userIdStr = form["userId"].ToString();
            var token = Uri.UnescapeDataString(form["token"].ToString());
            var password = form["password"].ToString();
            var confirmPassword = form["confirmPassword"].ToString();

            var encodedToken = Uri.EscapeDataString(token);
            var redirectBase = $"/reset-password?userId={userIdStr}&token={encodedToken}";

            if (password != confirmPassword)
                return Results.Redirect($"{redirectBase}&error=mismatch");

            var user = await userManager.FindByIdAsync(userIdStr);
            if (user == null)
                return Results.Redirect($"{redirectBase}&error=invalid");

            var result = await userManager.ResetPasswordAsync(user, token, password);
            if (!result.Succeeded)
            {
                var error = Uri.EscapeDataString(result.Errors.First().Description);
                return Results.Redirect($"{redirectBase}&error={error}");
            }

            return Results.Redirect($"/reset-password?userId={userIdStr}&token={encodedToken}&success=1");
        }).DisableAntiforgery();
    }
}
