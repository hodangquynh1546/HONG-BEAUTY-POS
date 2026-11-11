using HongBeautyPOS.Domain;

namespace HongBeautyPOS.Api.Contracts;

internal sealed record LoginRequest(string Email, string Password);
internal sealed record RefreshRequest(string? RefreshToken);
internal sealed record UpdateProfileRequest(string FullName, string? AvatarData);
internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
internal sealed record DailyReportPoint(DateOnly Date, decimal GrossSales, decimal ReturnedSales, decimal NetRevenue,
    decimal CashIn, decimal CashOut, decimal NetCashFlow);
internal sealed record CreateEmployeeRequest(string Email, string FullName, string Password, string[]? Permissions, string? AvatarData);
internal sealed record ReplacePermissionsRequest(string[]? Permissions);
internal sealed record SetEmployeeStatusRequest(bool Active);
internal sealed record ResetEmployeePasswordRequest(string Password);
internal sealed record ActiveStatusRequest(bool Active);
internal sealed record CreateCategoryRequest(string Name, Guid? ParentId);
internal sealed record CreateAttributeRequest(string Name, string[]? Values);
internal sealed record CreateCustomerGroupRequest(string Name);
internal sealed record CreateCustomerRequest(string FullName, string? Phone, string? Email, string? Phone2 = null, string? Gender = null,
    string? Address = null, string? Contact = null, string? Notes = null, Guid? GroupId = null, string? AvatarData = null);
internal sealed record CreateSupplierRequest(string Code, string Name, string? Phone, string? Email);
internal sealed record CountStocktakeItemRequest(decimal ActualQuantity);
internal sealed record CreateCashEntryRequest(Guid IdempotencyKey, CashEntryKind Kind, PaymentMethod Method, decimal Amount, string Description);
