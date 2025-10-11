using CloudinaryDotNet;
using E_Commerce.DtoModels.AccountDtos;
using E_Commerce.DtoModels.TokenDtos;
using E_Commerce.Interfaces;
using E_Commerce.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace E_Commerce.Services.Externallogin
{
    public interface IExtrenalLoginService
    {
        AuthenticationProperties ExternalLogin(string provider = "Google", string returnUrl = "/");
        Task<Result<TokensDto>> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null);
    }

    public class ExtrenalLoginService : IExtrenalLoginService
    {
        private readonly ILogger<ExtrenalLoginService> _logger;
        private readonly SignInManager<Customer> _signInManager;
        private readonly UserManager<Customer> _userManager;
        private readonly ITokenService _tokenService;

        public ExtrenalLoginService(
            ITokenService tokenService,
            ILogger<ExtrenalLoginService> logger,
            SignInManager<Customer> signInManager,
            UserManager<Customer> userManager)
        {
            _tokenService = tokenService;
            _logger = logger;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        public AuthenticationProperties ExternalLogin(string provider = "Google", string returnUrl = "/")
        {
            _logger.LogInformation("Starting external login with provider: {Provider}", provider);
           
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, returnUrl);

            return properties;
        }

        public async Task<Result<TokensDto>> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            _logger.LogInformation("External login callback triggered.");

            if (remoteError != null)
            {
                _logger.LogWarning("External login failed with error: {Error}", remoteError);
                return Result<TokensDto>.Fail($"Error from external provider: {remoteError}");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogError("Failed to retrieve external login info.");
                return Result<TokensDto>.Fail("Error loading external login information.");
            }

            _logger.LogInformation("External login info retrieved successfully for provider: {Provider}", info.LoginProvider);

        
            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
            if (signInResult.Succeeded)
            {

                var userlog = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (userlog == null)
                {
                    _logger.LogError("User not found after successful external login sign-in.");
                    return Result<TokensDto>.Fail("User not found after external login.");
                }
                // ✅ check user status before generating token
                if (userlog.LockoutEnabled && userlog.LockoutEnd.HasValue && userlog.LockoutEnd > DateTimeOffset.UtcNow)
                {
                    _logger.LogWarning("User {Email} account is locked until {LockoutEnd}", userlog.Email, userlog.LockoutEnd);
                    return Result<TokensDto>.Fail("Account is locked. Please try again later.");
                }

                if (!userlog.EmailConfirmed)
                {
                    _logger.LogWarning("User {Email} email not confirmed.", userlog.Email);
                    return Result<TokensDto>.Fail("Please confirm your email before logging in.");
                }

                // optional: if you have IsDeleted or IsActive
                if (userlog is Customer && userlog.DeletedAt!=null)
                {
                    _logger.LogWarning("User {Email} account is deleted or inactive.", userlog.Email);
                    return Result<TokensDto>.Fail("Account is inactive. Contact support.");
                }


                var token = await _tokenService.GenerateTokenAsync(userlog);
                if(!token.Success||token.Data is null)
                {
                    _logger.LogError("Token generation failed: {Message}", token.Message);
                    return Result<TokensDto>.Fail("Token generation failed.");
                }
                var tokensDto = new TokensDto
                {
                    Token = token.Data,
                    Roles= (await _userManager.GetRolesAsync(userlog)).ToList()
                };
                _logger.LogInformation("User successfully signed in with {Provider}.", info.LoginProvider);
                return Result<TokensDto>.Ok(tokensDto, $"Login successful via {info.LoginProvider}");
            }

           
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var name = info.Principal.FindFirstValue(ClaimTypes.Name);
            var phone = info.Principal.FindFirstValue(ClaimTypes.MobilePhone);
            
            

            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Email claim not found for external user.");
                return Result<TokensDto>.Fail("Cannot retrieve email from external provider.");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                _logger.LogInformation("Creating new user with email: {Email}", email);
                user = new Customer
                {
                    UserName = email,
                    PhoneNumber = phone,
                    Email = email,
                    Name = name ?? "",
                    EmailConfirmed = true 

                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create user: {Errors}", errors);
                    return Result<TokensDto>.Fail($"User creation failed: {errors}");
                }

                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (!addLoginResult.Succeeded)
                {
                    var errors = string.Join(", ", addLoginResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to link external login: {Errors}", errors);
                    return Result<TokensDto>.Fail($"External login link failed: {errors}");
                }
                IdentityResult result1 = await _userManager.AddToRoleAsync(user, "User");
                if (!result1.Succeeded)
                {
                    _logger.LogError(result1.Errors.ToString());
                    return Result<TokensDto>.Fail("Errors:Sorry Try Again Later", 500);
                }
                _logger.LogInformation("User {Email} successfully created and linked with {Provider}.", email, info.LoginProvider);
            }

     
            await _signInManager.SignInAsync(user, isPersistent: false);
            _logger.LogInformation("User {Email} signed in via {Provider}.", email, info.LoginProvider);

            // ✅ check user status before generating token
            if (user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("User {Email} account is locked until {LockoutEnd}", user.Email, user.LockoutEnd);
                return Result<TokensDto>.Fail("Account is locked. Please try again later.");
            }

          

         
            if (user is Customer customer && customer.DeletedAt!=null)
            {
                _logger.LogWarning("User {Email} account is deleted or inactive.", user.Email);
                return Result<TokensDto>.Fail("Account is inactive. Contact support.");
            }

            var tokenResult = await _tokenService.GenerateTokenAsync(user);
            if (!tokenResult.Success || tokenResult.Data is null)
            {
                _logger.LogError("Token generation failed: {Message}", tokenResult.Message);
                return Result<TokensDto>.Fail("Token generation failed.");
            }
            var tokens = new TokensDto
            {
                Token = tokenResult.Data,
                Roles = (await _userManager.GetRolesAsync(user)).ToList()
            };

            return Result<TokensDto>.Ok(tokens, $"Login successful via {info.LoginProvider} ({email})",302);
        }
    
    }
}
