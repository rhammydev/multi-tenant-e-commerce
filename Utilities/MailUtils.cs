namespace ECommerce.Utilities;

public static class MailUtils
{
    public static string VerifyEmail(string firstName, string link, string token, int hours) =>
        $"""
        Hi {firstName},

        Welcome! Please verify your email address using the link below (valid for {hours} hours):

        {link}

        Token: {token}

        If you did not create this account, ignore this email.
        """;

    public static string PasswordReset(string firstName, string link, string token, int minutes) =>
        $"""
        Hi {firstName},

        A password reset was requested for your account. Use the link below (valid for {minutes} minutes):

        {link}

        Token: {token}

        If you did not request this, you can ignore this email; your password has not changed.
        """;
}
