namespace vless_proxy.Contracts.Response.Common;

public class ApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }

    public static ApiResponse Ok(string? message = null) => new() { Success = true, Message = message };
    public static ApiResponse Fail(string message) => new() { Success = false, Message = message };
}
