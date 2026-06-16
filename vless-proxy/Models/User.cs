using System.ComponentModel.DataAnnotations;

namespace vless_proxy.Models;

public class User
{
    public Guid Id { get; set; } =  Guid.NewGuid();
    
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string HashPassword { get; set; } = string.Empty;
    
    public string TOTPSecret { get; set; } = string.Empty;
}