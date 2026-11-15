using System.Text.Json;
using ECommerce.DTOs.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECommerce.Filters;

public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument == null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument));
            if (result.IsValid) continue;

            var errors = result.Errors
                .Select(e => new ApiError(JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName), e.ErrorMessage))
                .ToList();

            context.Result = new BadRequestObjectResult(
                ApiResponse.Fail("Validation failed.", 400, ResponseCodes.BadRequest, errors));
            return;
        }

        await next();
    }
}
