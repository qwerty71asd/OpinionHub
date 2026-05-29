using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender<ApplicationUser> _emailSender;

    public ForgotPasswordModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender<ApplicationUser> emailSender)
    {
        _userManager = userManager;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Это обязательное поле")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);

        // Чтобы не раскрывать наличие email в системе — всегда показываем экран «проверьте почту».
        if (user is not null && await _userManager.IsEmailConfirmedAsync(user))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code = encoded, email = Input.Email },
                protocol: Request.Scheme);

            await _emailSender.SendPasswordResetLinkAsync(
                user,
                Input.Email,
                HtmlEncoder.Default.Encode(callbackUrl!));
        }

        return RedirectToPage("./ForgotPasswordConfirmation");
    }
}
