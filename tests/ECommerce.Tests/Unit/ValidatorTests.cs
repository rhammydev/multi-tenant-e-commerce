using ECommerce.DTOs.Auth;
using ECommerce.Helpers;
using ECommerce.Validators;

namespace ECommerce.Tests.Unit;

public class ValidatorTests
{
    private readonly RegisterRequestValidator _register = new();

    private static RegisterRequest Valid() => new()
    {
        FirstName = "Ada", LastName = "Okoro", Email = "ada@shop.test",
        Phone = "+2348012345678", Password = "Password@123!"
    };

    [Fact]
    public void A_valid_registration_passes() => Assert.True(_register.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("Short1!")]            // too short
    [InlineData("alllowercase123!")]   // no uppercase
    [InlineData("ALLUPPERCASE123!")]   // no lowercase
    [InlineData("NoNumbersHere!!")]    // no digit
    [InlineData("NoSymbolsHere123")]   // no symbol
    public void Weak_passwords_are_rejected(string password)
    {
        var request = Valid();
        request.Password = password;
        Assert.False(_register.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("+2348012345678", true)]
    [InlineData("08012345678", true)]
    [InlineData("12345", false)]
    [InlineData("+23480abc45678", false)]
    public void Phone_numbers_are_checked(string phone, bool valid)
    {
        var request = Valid();
        request.Phone = phone;
        Assert.Equal(valid, _register.Validate(request).IsValid);
    }

    [Fact]
    public void Change_password_rejects_reusing_the_current_password()
    {
        var result = new ChangePasswordRequestValidator().Validate(new ChangePasswordRequest
        {
            CurrentPassword = "Password@123!", NewPassword = "Password@123!", ConfirmNewPassword = "Password@123!"
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Tokens_are_random_and_hashing_is_stable()
    {
        var a = TokenHelper.GenerateToken();
        var b = TokenHelper.GenerateToken();

        Assert.NotEqual(a, b);
        Assert.Equal(64, a.Length);
        Assert.Equal(TokenHelper.Hash(a), TokenHelper.Hash(a));
        Assert.NotEqual(a, TokenHelper.Hash(a));
    }
}
