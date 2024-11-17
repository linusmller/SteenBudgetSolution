﻿using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using MimeKit;
using Org.BouncyCastle.Cms;
using System.Net.Mail;

namespace Backend.Infrastructure.Email
{
    public class EmailPreparationService : IEmailPreparationService
    {
        private readonly IConfiguration _configuration;

        public EmailPreparationService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<EmailMessageModel> PrepareVerificationEmailAsync(EmailMessageModel emailMessageModel)
        {
            // Generate the verification URL
            var verificationUrl = $"{_configuration["AppSettings:BaseUrl"]}/verify-email?token={emailMessageModel.Token}";

            return new EmailMessageModel
            {
                Recipient = emailMessageModel.Recipient,
                Subject = "Email Verification",
                Body = $"Please verify your email by clicking the following link: <a href='{verificationUrl}'>Verify Email</a>",
                Sender = _configuration["Smtp:UsernameNoReply"],    // no-reply@ebudget.se
                FromName = "No Reply",
                ToName = "",
                ReplyTo = ""
            };
        }
        public async Task<EmailMessageModel> PrepareContactUsEmailAsync(EmailMessageModel emailMessageModel)
        {
            return new EmailMessageModel
            {
                Recipient = _configuration["Smtp:UsernameInfoUser"],     // info@mail.ebudget.se
                Subject = emailMessageModel.Subject,
                Body = emailMessageModel.Subject,
                Sender = _configuration["Smtp:UsernameNoReply"],         // no-reply@ebudget.se
                FromName = emailMessageModel.FromName,
                ToName = "eBudget Support",
                ReplyTo = emailMessageModel.FromName
            };
        }
    }
}