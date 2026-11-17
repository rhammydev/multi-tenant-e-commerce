using FluentValidation;

namespace ECommerce.Validators;

public static class ValidationRules
{
    public const string PasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{10,100}$";
    public const string PhonePattern = @"^\+?\d{10,15}$";
    public const string NamePattern = @"^[\p{L}\s'-]+$";

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("A valid email address is required.");

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Password is required.")
            .Matches(PasswordPattern)
            .WithMessage("{PropertyName} must be 10-100 characters and contain an uppercase letter, a lowercase letter, a number and a symbol.");

    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("{PropertyName} is required.")
            .MaximumLength(100).WithMessage("{PropertyName} must not exceed 100 characters.")
            .Matches(NamePattern).WithMessage("{PropertyName} contains invalid characters.");

    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Phone number is required.")
            .Matches(PhonePattern).WithMessage("Phone number must contain 10-15 digits and may start with '+'.");
}
