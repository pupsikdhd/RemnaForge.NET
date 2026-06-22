using System.ComponentModel.DataAnnotations;

namespace vless_proxy.Models;

public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string Key { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
