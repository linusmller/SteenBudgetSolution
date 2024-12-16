﻿using Backend.Application.DTO;
using Backend.Application.Services.EmailServices;
using Backend.Domain.Entities;
using Backend.Infrastructure.Data.Sql.Interfaces;
using Backend.Infrastructure.Interfaces;
using Backend.Infrastructure.Security;
using Backend.Infrastructure.Helpers.Validators;
namespace Backend.Application.Services.UserServices
{
    public class UserServices
    {
        private readonly IUserSqlExecutor _userSqlExecutor;
        private readonly ITokenSqlExecutor _tokenSqlExecutor;
        private readonly string? _jwtSecretKey;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _iEmailService;
        private readonly EmailVerificationService _emailVerificationService;
        private readonly ILogger<UserServices> _logger;
        private readonly TokenService _tokenService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEnvironmentService _environmentService;

        public UserServices(IUserSqlExecutor userSqlExecutor, ITokenSqlExecutor tokenSqlExecutor, IConfiguration configuration, IEmailService IemailService, ILogger<UserServices> logger, EmailVerificationService emailVerificationService, TokenService tokenService, IHttpContextAccessor httpContextAccessor, IEnvironmentService environmentService)
        {
            _userSqlExecutor = userSqlExecutor;
            _tokenSqlExecutor = tokenSqlExecutor;
            _configuration = configuration;
            _iEmailService = IemailService;
            _emailVerificationService = emailVerificationService;
            _logger = logger;
            _tokenService = tokenService;
            _httpContextAccessor = httpContextAccessor;
            _environmentService = environmentService;
        }
        // Method for registering a new user
        public async Task<bool> RegisterUserAsync(UserCreationDto userCreationDto)
        {
            // Step 1: Check if user already exists
            if (await CheckIfUserExistsAsync(userCreationDto.Email))
            {
                _logger.LogWarning("Registration attempt with already taken email: {Email}", userCreationDto.Email);
                return false;
            }
            else if (userCreationDto.Email == null)
            {
                _logger.LogWarning("Registration attempt with null email");
                return false;
            }

            // Step 2: Create Password Hash and Persoid and assign roles
            _logger.LogInformation("Hashing password for user: {Email}", userCreationDto.Email);
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(userCreationDto.Password);
            var userPersoId = Guid.NewGuid();
            var roles = "1"; // Assigning default role TODO: this should be configurable somehow

            var user = new UserModel
            {
                PersoId = userPersoId,
                Roles = roles,
                FirstName = userCreationDto.FirstName,
                LastName = userCreationDto.LastName,
                Email = userCreationDto.Email,
                Password = hashedPassword,
            };
            var isSuccessfulRegistration = await CreateNewRegisteredUserAsync(user);
            if (!isSuccessfulRegistration)
            {
                _logger.LogError("Failed to register user for email: {Email}", user.Email);
                return false;
            }

            _logger.LogInformation("User registered successfully for email: {Email}", user.Email);
            return true;
        }
        // Method responsible for the flow of getting token and sending verification email with token
        public async Task<bool> SendVerificationEmailWithTokenAsync(string email)
        {
            // Step 1: Get user model
            UserModel user = await GetUserModelAsync(email: email);
            if (user == null)
            {
                _logger.LogError("Failed to get user for email: {Email}", email);
                return false;
            }

            // Step 2: Create token for user
            var tokenModel = await CreateEmailTokenAsync(user.PersoId);

            // Step 3: Insert token into database
            bool insertationSuccess = await InsertUserTokenAsync(tokenModel);
            if (!insertationSuccess)
            {
                _logger.LogError("Failed to insert token for email: {Email}", user.Email);
                return false;
            }

            // Step 4: Send verification email
            EmailMessageModel emailMessageModel = new EmailMessageModel
            {
                Recipient = user.Email,
                Token = tokenModel.Token,
                EmailType = EmailType.Verification
            };
            bool emailSent = await _iEmailService.ProcessAndSendEmailAsync(emailMessageModel);
            if (!emailSent)
            {
                _logger.LogError("Failed to send verification email for email: {Email}", user.Email);
                return false;
            }
            _logger.LogInformation("Verification email and token generated successfully for email: {Email}", email);
            return true;
        }

        // Method for logging in a user
        public async Task<LoginResultDto> LoginAsync(UserLoginDto userLoginDto)
        {

            // Step 1: Validate user credentials
            var validUser = await ValidateUserAsync(userLoginDto.Email, userLoginDto.Password);
            if (!validUser.IsValid)
            {
                _logger.LogWarning("Invalid credentials for email: {Email}", userLoginDto.Email);
                return new LoginResultDto { Success = false, Message = validUser.ErrorMessage };
            }

            // Step 2: Generate JWT Token and set as HTTP-only cookie
            // Arrange
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
            
            var verifiedUser = await GetUserModelAsync(email: userLoginDto.Email);
            var token = _tokenService.GenerateJwtToken(verifiedUser.PersoId.ToString(), email: userLoginDto.Email);
            //var isSecure = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
            
            var isSecure = _environmentService.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";

            _httpContextAccessor.HttpContext.Response.Cookies.Append("auth_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddHours(1)
            });

            
            return new LoginResultDto
            {
                Success = true,
                Message = "Login successful",
                UserName = verifiedUser.Email 
            };
        }

        // Validate user credentials
        private async Task<ValidationResult> ValidateUserAsync(string email, string password)
        {
            // Step 1: Get user model
            var user = await _userSqlExecutor.GetUserModelAsync(email: email);
            if (user == null)
            {
                _logger.LogWarning("User not found for email: {Email}", email);
                return new ValidationResult(false, "Felaktiga uppgifter");
            }
            // Step 2: Check if user email is confirmed
            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("User email not confirmed for email: {Email}", email);
                return new ValidationResult(false, "E-post ej bekräftad");
            }
            // Step 3: Verify password
            if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                _logger.LogWarning("Invalid password for email: {Email}", email);
                return new ValidationResult(false, "Felaktiga uppgifter");
            }

            return new ValidationResult(true);
        }
        private async Task<bool> CreateNewRegisteredUserAsync(UserModel user)
        {
            return await _userSqlExecutor.InsertNewUserDatabaseAsync(user);
        }
        public async Task<bool> CheckIfUserExistsAsync(string email)
        {
            return await _userSqlExecutor.IsUserExistInDatabaseAsync(email);
        }
        public async Task<UserModel> GetUserModelAsync(Guid? persoid = null, string? email = null)
        {
            if (persoid == null && email == null)
            {
                throw new ArgumentException("Either persoid or email must be provided.");
            }

            return persoid != null
                ? await _userSqlExecutor.GetUserModelAsync(persoid: persoid)
                : await _userSqlExecutor.GetUserModelAsync(email: email);
        }
        public async Task<bool> UpdateEmailConfirmationStatusAsync(Guid persoid)
        {
            return await _userSqlExecutor.UpdateEmailConfirmationStatusAsync(persoid);
        }
        public async Task<UserTokenModel?> GetUserVerificationTokenByPersoIdAsync(Guid persoid)
        {
            return await _tokenSqlExecutor.GetUserVerificationTokenByPersoIdAsync(persoid);
        }
        public async Task<UserTokenModel?> GetUserVerificationTokenByTokenAsync(Guid token)
        {
            return await _tokenSqlExecutor.GetUserVerificationTokenByTokenAsync(token);
        }
        public async Task<(bool IsSuccess, int StatusCode, string Message)> ResendVerificationEmailAsync(string email)
        {
            return await _emailVerificationService.ResendVerificationEmailAsync(email); 
        }
        public async Task<UserTokenModel> CreateEmailTokenAsync(Guid persoid)
        {
            // Generate token for user
            var tokenModel = await _tokenSqlExecutor.GenerateUserTokenAsync(persoid);
            return tokenModel;
        }
        public async Task<bool> InsertUserTokenAsync(UserTokenModel tokenModel)
        {
            bool insertationSuccess = await _tokenSqlExecutor.InsertUserTokenAsync(tokenModel);
            return insertationSuccess;
        }
        public async Task<bool> VerifyEmailTokenAsync(Guid token)
        {
            // step 1: Get token data
            var tokenData = await GetUserVerificationTokenByTokenAsync(token);

            // Step 2: Check if token is valid
            if (tokenData == null)
            {
                _logger.LogWarning("Token not found or invalid for token: {Token}", token);
                return false;
            }
            if (tokenData.TokenExpiryDate < DateTime.UtcNow)
            {
                _logger.LogWarning("Token expired for PersoId: {PersoId}", tokenData.PersoId);
                return false;
            }

            // step 3: Update user email confirmation status
            bool updateSuccesful = await UpdateEmailConfirmationStatusAsync(tokenData.PersoId);
            if (!updateSuccesful)
            {
                _logger.LogError("Failed to update email confirmation status for Persoid: {Persoid}", tokenData.PersoId);
                return false;
            }
            return true;
        }
        public async Task<bool> DeleteUserByEmailAsync(string email)
        {
            var rowsAffected = await _userSqlExecutor.DeleteUserByEmailAsync(email);
            return rowsAffected > 0;
        }
        public async Task<bool> DeleteUserTokenByEmailAsync(Guid persoid)
        {
            var rowsAffected = await _tokenSqlExecutor.DeleteUserTokenByPersoidAsync(persoid);
            return rowsAffected > 0;
        }
    }
}
