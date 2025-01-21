﻿using Backend.Domain.Entities;
using Backend.Infrastructure.Data.Sql.Interfaces;
using Backend.Application.Interfaces.UserServices;
using Backend.Application.DTO;
using System.Security.Claims;

namespace Backend.Application.Services.UserServices
{
    public class UserManagementService : IUserManagementService
    {
        private readonly IUserSQLProvider _userSQLProvider;
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(IUserSQLProvider userSQLProvider, ILogger<UserManagementService> logger)
        {
            _userSQLProvider = userSQLProvider;
            _logger = logger;
        }
        public AuthStatusDto CheckAuthStatus(ClaimsPrincipal user)
        {
            if (user == null || user.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("User is null or not authenticated.");
                return new AuthStatusDto { Authenticated = false };
            }

            // Extract claims
            var email = user.FindFirst(ClaimTypes.Email)?.Value;
            var role = user.FindFirst(ClaimTypes.Role)?.Value;
            
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("User authenticated but email claim is missing.");
            }

            _logger.LogInformation("Authenticated user. Email: {Email}, Role: {Role}", email, role);
            return new AuthStatusDto
            {
                Authenticated = !string.IsNullOrEmpty(email), // User is authenticated only if email exists
                Email = email,
                Role = role
            };
        }
        public async Task<bool> CheckIfUserExistsAsync(string email) =>
            await _userSQLProvider.UserSqlExecutor.IsUserExistInDatabaseAsync(email);

        public async Task<bool> CreateUserAsync(UserModel user) =>
            await _userSQLProvider.UserSqlExecutor.InsertNewUserDatabaseAsync(user);

        public async Task<UserModel?> GetUserByEmailAsync(string email) =>
            await _userSQLProvider.UserSqlExecutor.GetUserModelAsync(email: email);

        public async Task<int> UpdateEmailConfirmationAsync(Guid persoid) => 
            await _userSQLProvider.UserSqlExecutor.UpdateEmailConfirmationAsync(persoid);
        public async Task<bool> IsEmailAlreadyConfirmedAsync(Guid persoid) =>
            await _userSQLProvider.UserSqlExecutor.IsEmailAlreadyConfirmedAsync(persoid);

    }

}
