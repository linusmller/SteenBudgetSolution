﻿using Backend.Application.DTO;
using Backend.Application.Models;
using Backend.Domain.Shared;

namespace Backend.Application.Interfaces.UserServices
{
    public interface IUserAuthenticationService
    {
        Task<ValidationResult> ValidateCredentialsAsync(string email, string password);
        Task<LoginResultDto> AuthenticateAndGenerateTokenAsync(string email);
        Task<bool> CheckLoginAttemptsAsync(string username);
        Task RecordFailedLoginAsync(string email, string ipAddress);
        Task<bool> ShouldLockUserAsync(string email);
        Task LockUserAsync(string email, TimeSpan lockoutDuration);
        Task<bool> SendResetPasswordEmailAsync(string email);
        Task<OperationResult> UpdatePasswordAsync(Guid token, string password);
    }
}
