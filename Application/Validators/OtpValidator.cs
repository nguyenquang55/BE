using Application.Contracts.Auth.Request;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Validators
{
    public class OtpValidator : AbstractValidator<VerifyOtpRequest>
    {
        public OtpValidator() 
        {
            RuleFor(x => x.Code)
            .NotEmpty().WithMessage("OTP is required")
            .MinimumLength(6).WithMessage("OTP must have 6 number");
        }
    }
}
